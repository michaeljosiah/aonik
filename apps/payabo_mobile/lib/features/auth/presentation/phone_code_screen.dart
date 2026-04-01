import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../app/environment/environment_provider.dart';
import '../../../data/api/api_exception.dart';
import '../../../data/repositories/repository_providers.dart';
import '../../../shared/theme/payabo_colors.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_button.dart';
import '../../../shared/widgets/payabo_otp_field.dart';
import 'auth_flow_scaffold.dart';
import 'onboarding_flow_state.dart';

class PhoneCodeScreen extends ConsumerStatefulWidget {
  const PhoneCodeScreen({super.key});

  @override
  ConsumerState<PhoneCodeScreen> createState() => _PhoneCodeScreenState();
}

class _PhoneCodeScreenState extends ConsumerState<PhoneCodeScreen> {
  Timer? _timer;
  int _secondsRemaining = 0;
  String _otpCode = '';
  bool _isVerifying = false;
  String? _errorMessage;
  late final TextEditingController _otpController;

  bool get isLocked => _secondsRemaining > 0;

  static const int _resendCooldownSeconds = 60;

  @override
  void initState() {
    super.initState();
    _otpController = TextEditingController();
    _startResendCountdown(_resendCooldownSeconds);
  }

  @override
  void dispose() {
    _timer?.cancel();
    _otpController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final canContinue = _otpCode.length == 6 && !_isVerifying;
    final env = ref.watch(appEnvironmentProvider);
    final devCode = ref.watch(onboardingControllerProvider).phoneOtpDevCode;
    final showDevHelper =
        !env.isProduction && devCode != null && devCode.isNotEmpty;

    return AuthFlowScaffold(
      title: 'The code is',
      onBack: () => context.go('/auth/register/contact-details'),
      useWarmBackground: true,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Padding(
            padding: const EdgeInsets.symmetric(vertical: PayaboSpacing.md),
            child: Center(
              child: PayaboOtpField(
                length: 6,
                enabled: !_isVerifying,
                controller: _otpController,
                onChanged: (value) {
                  setState(() {
                    _otpCode = value;
                    _errorMessage = null;
                  });
                },
                onCompleted: (value) {
                  setState(() {
                    _otpCode = value;
                    _errorMessage = null;
                  });
                },
              ),
            ),
          ),
          if (showDevHelper)
            Center(
              child: TextButton(
                onPressed: () {
                  _otpController.text = devCode;
                  setState(() {
                    _otpCode = devCode;
                    _errorMessage = null;
                  });
                },
                child: Text(
                  'Show code (dev)',
                  style: Theme.of(context).textTheme.bodySmall?.copyWith(
                        color: PayaboColors.muted,
                        decoration: TextDecoration.underline,
                      ),
                ),
              ),
            ),
          if (_errorMessage != null) ...[
            const SizedBox(height: PayaboSpacing.sm),
            Center(
              child: Text(
                _errorMessage!,
                style: Theme.of(context)
                    .textTheme
                    .bodyMedium
                    ?.copyWith(color: Theme.of(context).colorScheme.error),
              ),
            ),
          ],
          const SizedBox(height: PayaboSpacing.xl),
          PayaboButton(
            label: _isVerifying ? 'Verifying...' : 'Next',
            onPressed: canContinue ? _verifyOtp : null,
          ),
          const SizedBox(height: PayaboSpacing.md),
          Center(
            child: isLocked
                ? Text(
                    'Request new code in ${_formatCountdown(_secondsRemaining)}',
                    style: Theme.of(context)
                        .textTheme
                        .bodyMedium
                        ?.copyWith(color: PayaboColors.muted),
                  )
                : TextButton(
                    onPressed: _resendOtp,
                    child: const Text('Request new code'),
                  ),
          ),
        ],
      ),
    );
  }

  Future<void> _verifyOtp() async {
    final challengeId =
        ref.read(onboardingControllerProvider).phoneOtpChallengeId;

    if (challengeId == null || challengeId.isEmpty) {
      setState(() =>
          _errorMessage = 'Verification session expired. Request a new code.');
      return;
    }

    setState(() {
      _isVerifying = true;
      _errorMessage = null;
    });

    try {
      final repository = ref.read(authRepositoryProvider);
      final isVerified =
          await repository.verifyRegistrationPhoneOtp(challengeId, _otpCode);

      if (!mounted) return;

      if (isVerified) {
        context.go('/auth/register/login-details');
      } else {
        setState(() => _errorMessage = 'Incorrect code. Please try again.');
      }
    } catch (error) {
      if (!mounted) return;

      final message = error is ApiException
          ? error.message
          : 'Unable to verify code right now.';

      setState(() => _errorMessage = message);
    } finally {
      if (mounted) {
        setState(() => _isVerifying = false);
      }
    }
  }

  Future<void> _resendOtp() async {
    final onboarding = ref.read(onboardingControllerProvider);
    final dialCode = onboarding.phoneCountry.dialCode.trim();
    final digits = onboarding.mobileNumber.trim().replaceAll(RegExp(r'\D'), '');
    final fullPhone = '$dialCode$digits';

    try {
      final repository = ref.read(authRepositoryProvider);
      final result = await repository.sendRegistrationPhoneOtp(fullPhone);

      ref
          .read(onboardingControllerProvider.notifier)
          .setPhoneOtpChallengeId(result.challengeId);

      ref
          .read(onboardingControllerProvider.notifier)
          .setPhoneOtpDevCode(result.devCode);

      _otpController.clear();
      _startResendCountdown(_resendCooldownSeconds);
    } catch (error) {
      if (!mounted) return;

      final message = error is ApiException
          ? error.message
          : 'Unable to send a new code right now.';

      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(message)),
      );
    }
  }

  String _formatCountdown(int totalSeconds) {
    final minutes = totalSeconds ~/ 60;
    final seconds = totalSeconds % 60;
    return '${minutes.toString().padLeft(2, '0')}:${seconds.toString().padLeft(2, '0')}';
  }

  void _startResendCountdown(int seconds) {
    _timer?.cancel();
    setState(() {
      _secondsRemaining = seconds;
      _otpCode = '';
    });

    _timer = Timer.periodic(const Duration(seconds: 1), (timer) {
      if (_secondsRemaining <= 1) {
        timer.cancel();
        setState(() {
          _secondsRemaining = 0;
        });
        return;
      }

      setState(() {
        _secondsRemaining -= 1;
      });
    });
  }
}
