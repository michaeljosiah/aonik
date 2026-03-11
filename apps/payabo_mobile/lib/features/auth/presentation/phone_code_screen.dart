import 'dart:async';

import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../../shared/theme/payabo_colors.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_button.dart';
import '../../../shared/widgets/payabo_otp_field.dart';
import 'auth_flow_scaffold.dart';

class PhoneCodeScreen extends StatefulWidget {
  const PhoneCodeScreen({
    super.key,
    this.initialDisabled = false,
  });

  final bool initialDisabled;

  @override
  State<PhoneCodeScreen> createState() => _PhoneCodeScreenState();
}

class _PhoneCodeScreenState extends State<PhoneCodeScreen> {
  Timer? _timer;
  int _secondsRemaining = 0;
  String _otpCode = '';

  bool get isLocked => _secondsRemaining > 0;

  @override
  void initState() {
    super.initState();

    if (widget.initialDisabled) {
      _startResendCountdown(5);
    }
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final canContinue = _otpCode.length == 6 && !isLocked;

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
                enabled: !isLocked,
                onChanged: (value) {
                  setState(() {
                    _otpCode = value;
                  });
                },
                onCompleted: (value) {
                  setState(() {
                    _otpCode = value;
                  });
                },
              ),
            ),
          ),
          const SizedBox(height: PayaboSpacing.xl),
          PayaboButton(
            label: 'Next',
            onPressed: canContinue
                ? () => context.go('/auth/register/login-details')
                : null,
          ),
          const SizedBox(height: PayaboSpacing.md),
          Center(
            child: isLocked
                ? Text(
                    'Request new code in $_secondsRemaining seconds',
                    style: Theme.of(context)
                        .textTheme
                        .bodyMedium
                        ?.copyWith(color: PayaboColors.muted),
                  )
                : TextButton(
                    onPressed: () => _startResendCountdown(5),
                    child: const Text('Request new code'),
                  ),
          ),
        ],
      ),
    );
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
