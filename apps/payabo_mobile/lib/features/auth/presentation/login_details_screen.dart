import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../shared/theme/payabo_colors.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_button.dart';
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
    final isLocked = widget.isDisabledState;
    final canSubmit = !isLocked && _canRegister;

    return AuthFlowScaffold(
      title: 'Login details',
      onBack: () => context.go('/auth/register/phone-code'),
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
          Text(
            'Your password must contain at least:',
            style: Theme.of(context).textTheme.bodyLarge,
          ),
          const SizedBox(height: PayaboSpacing.sm),
          _RequirementLine(
            text: '8 characters',
            met: _passwordController.text.length >= 8,
            disabled: isLocked,
          ),
          _RequirementLine(
            text: '1 lowercase letter',
            met: RegExp('[a-z]').hasMatch(_passwordController.text),
            disabled: isLocked,
          ),
          _RequirementLine(
            text: '1 uppercase letter',
            met: RegExp('[A-Z]').hasMatch(_passwordController.text),
            disabled: isLocked,
          ),
          _RequirementLine(
            text: '1 number',
            met: RegExp('[0-9]').hasMatch(_passwordController.text),
            disabled: isLocked,
          ),
          const SizedBox(height: PayaboSpacing.xl),
          PayaboButton(
            label: 'Register Account',
            onPressed: canSubmit ? () => context.go('/dashboard') : null,
          ),
        ],
      ),
    );
  }

  bool get _canRegister {
    return isValidEmail(_emailController.text) &&
        meetsPasswordRequirements(_passwordController.text);
  }
}

class _RequirementLine extends StatelessWidget {
  const _RequirementLine({
    required this.text,
    required this.met,
    required this.disabled,
  });

  final String text;
  final bool met;
  final bool disabled;

  @override
  Widget build(BuildContext context) {
    final Color color = disabled
        ? PayaboColors.muted
        : met
            ? PayaboColors.success
            : PayaboColors.muted;

    return Padding(
      padding: const EdgeInsets.only(bottom: 6),
      child: Row(
        children: <Widget>[
          Icon(
            met ? Icons.check_circle : Icons.radio_button_unchecked,
            size: 16,
            color: color,
          ),
          const SizedBox(width: PayaboSpacing.sm),
          Text(
            text,
            style:
                Theme.of(context).textTheme.bodyMedium?.copyWith(color: color),
          ),
        ],
      ),
    );
  }
}
