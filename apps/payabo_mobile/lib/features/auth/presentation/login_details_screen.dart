import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../app/auth/auth_controller.dart';
import '../../../data/api/api_exception.dart';
import '../../../data/repositories/auth_repository.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_button.dart';
import '../../../shared/widgets/payabo_password_requirements.dart';
import '../../../shared/widgets/payabo_text_field.dart';
import 'auth_flow_scaffold.dart';
import 'onboarding_flow_state.dart';

class LoginDetailsScreen extends ConsumerStatefulWidget {
  const LoginDetailsScreen({
    super.key,
    this.isDisabledState = false,
  });

  final bool isDisabledState;

  @override
  ConsumerState<LoginDetailsScreen> createState() => _LoginDetailsScreenState();
}

class _LoginDetailsScreenState extends ConsumerState<LoginDetailsScreen> {
  late final TextEditingController _emailController;
  late final TextEditingController _passwordController;
  bool _isPasswordVisible = false;

  @override
  void initState() {
    super.initState();
    final onboarding = ref.read(onboardingControllerProvider);
    _emailController = TextEditingController(text: onboarding.email);
    _passwordController = TextEditingController(text: onboarding.password);
  }

  @override
  void dispose() {
    _emailController.dispose();
    _passwordController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final onboarding = ref.watch(onboardingControllerProvider);
    final authState = ref.watch(authControllerProvider);
    final isLocked = widget.isDisabledState;
    final canSubmit = !isLocked && _canRegister && !authState.isBusy;

    return AuthFlowScaffold(
      title: 'Login details',
      onBack: () => context.go('/auth/register/phone-code'),
      useWarmBackground: true,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          PayaboTextField(
            label: 'Email',
            variant: PayaboInputVariant.floating,
            enabled: !isLocked,
            controller: _emailController,
            keyboardType: TextInputType.emailAddress,
            onChanged: (value) {
              ref.read(onboardingControllerProvider.notifier).setEmail(value);
              setState(() {});
            },
          ),
          const SizedBox(height: PayaboSpacing.md),
          PayaboTextField(
            label: 'Password',
            variant: PayaboInputVariant.floating,
            enabled: !isLocked,
            controller: _passwordController,
            obscureText: !_isPasswordVisible,
            onChanged: (value) {
              ref
                  .read(onboardingControllerProvider.notifier)
                  .setPassword(value);
              setState(() {});
            },
            suffixIcon: IconButton(
              onPressed: isLocked
                  ? null
                  : () {
                      setState(() {
                        _isPasswordVisible = !_isPasswordVisible;
                      });
                    },
              icon: Icon(_isPasswordVisible
                  ? Icons.visibility_off_outlined
                  : Icons.visibility_outlined),
            ),
          ),
          const SizedBox(height: PayaboSpacing.lg),
          PayaboPasswordRequirements(
            password: _passwordController.text,
            disabled: isLocked,
            titleStyle: Theme.of(context).textTheme.bodyLarge,
          ),
          const SizedBox(height: PayaboSpacing.xl),
          PayaboButton(
            label: 'Register Account',
            onPressed: canSubmit
                ? () async {
                    final request = RegisterIndividualRequest(
                      firstName: onboarding.firstName.trim(),
                      lastName: onboarding.lastName.trim(),
                      email: _emailController.text.trim(),
                      phone: _resolvePhoneNumber(onboarding),
                      password: _passwordController.text,
                      registrationCountry: onboarding.registrationCountryCode,
                    );

                    try {
                      await ref
                          .read(authControllerProvider.notifier)
                          .registerIndividual(request);

                      ref.read(onboardingControllerProvider.notifier).reset();

                      if (!context.mounted) {
                        return;
                      }

                      context.go('/dashboard');
                    } catch (error) {
                      if (!context.mounted) {
                        return;
                      }

                      final message = error is ApiException
                          ? error.message
                          : 'Unable to complete registration right now.';

                      ScaffoldMessenger.of(context).showSnackBar(
                        SnackBar(content: Text(message)),
                      );
                    }
                  }
                : null,
          ),
        ],
      ),
    );
  }

  bool get _canRegister {
    return isValidEmail(_emailController.text) &&
        meetsPasswordRequirements(_passwordController.text);
  }

  String? _resolvePhoneNumber(OnboardingState onboarding) {
    final digits = onboarding.mobileNumber.replaceAll(RegExp(r'\D'), '');
    if (digits.isEmpty) {
      return null;
    }

    final dialCode = onboarding.phoneCountry.dialCode.trim();
    return '$dialCode$digits';
  }
}
