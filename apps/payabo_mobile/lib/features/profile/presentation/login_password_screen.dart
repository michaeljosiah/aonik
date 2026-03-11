import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../data/api/api_exception.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/validation/payabo_input_validators.dart';
import '../../../shared/widgets/payabo_button.dart';
import '../../../shared/widgets/payabo_password_requirements.dart';
import '../../../shared/widgets/payabo_text_field.dart';
import 'profile_scaffold.dart';
import 'profile_state.dart';

void _showError(BuildContext context, String message) {
  ScaffoldMessenger.of(context)
    ..hideCurrentSnackBar()
    ..showSnackBar(SnackBar(content: Text(message)));
}

class LoginPasswordScreen extends ConsumerStatefulWidget {
  const LoginPasswordScreen({super.key});

  @override
  ConsumerState<LoginPasswordScreen> createState() =>
      _LoginPasswordScreenState();
}

class _LoginPasswordScreenState extends ConsumerState<LoginPasswordScreen> {
  final TextEditingController _currentPasswordController =
      TextEditingController();
  final TextEditingController _newPasswordController = TextEditingController();
  bool _hideCurrent = true;
  bool _hideNew = true;
  bool _saving = false;

  @override
  void dispose() {
    _currentPasswordController.dispose();
    _newPasswordController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final password = _newPasswordController.text;
    final canSubmit = validatePayaboPassword(password).isValid &&
        _currentPasswordController.text.isNotEmpty &&
        !_saving;

    return ProfileScaffold(
      title: 'Password',
      backRoute: '/profile/login-details',
      footer: PayaboButton(
        label: _saving ? 'Saving...' : 'Save changes',
        onPressed: canSubmit ? _submit : null,
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
          PayaboPasswordRequirements(
            password: password,
            titleStyle: Theme.of(context).textTheme.titleSmall,
          ),
        ],
      ),
    );
  }

  Future<void> _submit() async {
    setState(() {
      _saving = true;
    });

    try {
      await ref.read(profileCoreProvider.notifier).updatePassword(
            currentPassword: _currentPasswordController.text,
            newPassword: _newPasswordController.text,
          );

      if (!mounted) {
        return;
      }

      context.go('/profile/login-details');
    } catch (error) {
      final message = error is ApiException
          ? error.message
          : 'Unable to update your password right now.';
      if (mounted) {
        _showError(context, message);
      }
      setState(() {
        _saving = false;
      });
    }
  }
}
