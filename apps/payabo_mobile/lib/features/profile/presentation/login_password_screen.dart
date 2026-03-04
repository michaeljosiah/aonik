import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../../shared/theme/payabo_colors.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_button.dart';
import '../../../shared/widgets/payabo_text_field.dart';
import 'profile_scaffold.dart';

class LoginPasswordScreen extends StatefulWidget {
  const LoginPasswordScreen({super.key});

  @override
  State<LoginPasswordScreen> createState() => _LoginPasswordScreenState();
}

class _LoginPasswordScreenState extends State<LoginPasswordScreen> {
  final TextEditingController _currentPasswordController =
      TextEditingController();
  final TextEditingController _newPasswordController = TextEditingController();
  bool _hideCurrent = true;
  bool _hideNew = true;

  @override
  void dispose() {
    _currentPasswordController.dispose();
    _newPasswordController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final password = _newPasswordController.text;
    final canSubmit = _isPasswordValid(password) &&
        _currentPasswordController.text.isNotEmpty;

    return ProfileScaffold(
      title: 'Password',
      backRoute: '/profile/login-details',
      footer: PayaboButton(
        label: 'Save changes',
        onPressed:
            canSubmit ? () => context.go('/profile/login-details') : null,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          PayaboTextField(
            label: 'Current password',
            variant: PayaboInputVariant.floating,
            controller: _currentPasswordController,
            obscureText: _hideCurrent,
            suffixIcon: IconButton(
              onPressed: () => setState(() => _hideCurrent = !_hideCurrent),
              icon: Icon(_hideCurrent
                  ? Icons.visibility_outlined
                  : Icons.visibility_off_outlined),
            ),
            onChanged: (_) => setState(() {}),
          ),
          const SizedBox(height: PayaboSpacing.md),
          PayaboTextField(
            label: 'New password',
            variant: PayaboInputVariant.floating,
            controller: _newPasswordController,
            obscureText: _hideNew,
            suffixIcon: IconButton(
              onPressed: () => setState(() => _hideNew = !_hideNew),
              icon: Icon(_hideNew
                  ? Icons.visibility_outlined
                  : Icons.visibility_off_outlined),
            ),
            onChanged: (_) => setState(() {}),
          ),
          const SizedBox(height: PayaboSpacing.md),
          Text(
            'Your password must contain at least:',
            style: Theme.of(context).textTheme.titleSmall,
          ),
          const SizedBox(height: PayaboSpacing.xs),
          _RuleLine(label: '8 characters', valid: password.length >= 8),
          _RuleLine(
              label: '1 lowercase letter',
              valid: RegExp(r'[a-z]').hasMatch(password)),
          _RuleLine(
              label: '1 uppercase letter',
              valid: RegExp(r'[A-Z]').hasMatch(password)),
          _RuleLine(
              label: '1 number', valid: RegExp(r'[0-9]').hasMatch(password)),
        ],
      ),
    );
  }

  bool _isPasswordValid(String value) {
    return value.length >= 8 &&
        RegExp(r'[a-z]').hasMatch(value) &&
        RegExp(r'[A-Z]').hasMatch(value) &&
        RegExp(r'[0-9]').hasMatch(value);
  }
}

class _RuleLine extends StatelessWidget {
  const _RuleLine({required this.label, required this.valid});

  final String label;
  final bool valid;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 2),
      child: Row(
        children: <Widget>[
          Icon(valid ? Icons.check_circle : Icons.radio_button_unchecked,
              size: 16,
              color: valid ? PayaboColors.success : PayaboColors.muted),
          const SizedBox(width: 8),
          Text(label),
        ],
      ),
    );
  }
}
