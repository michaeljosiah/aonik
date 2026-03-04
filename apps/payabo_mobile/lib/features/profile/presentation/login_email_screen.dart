import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../shared/theme/payabo_colors.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_button.dart';
import '../../../shared/widgets/payabo_text_field.dart';
import 'profile_scaffold.dart';
import 'profile_state.dart';

class LoginEmailScreen extends ConsumerStatefulWidget {
  const LoginEmailScreen({super.key});

  @override
  ConsumerState<LoginEmailScreen> createState() => _LoginEmailScreenState();
}

class _LoginEmailScreenState extends ConsumerState<LoginEmailScreen> {
  late final TextEditingController _currentEmailController;
  final TextEditingController _newEmailController = TextEditingController();
  final TextEditingController _passwordController = TextEditingController();
  bool _hidePassword = true;
  bool _saving = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    _currentEmailController =
        TextEditingController(text: ref.read(profileControllerProvider).email);
  }

  @override
  void dispose() {
    _currentEmailController.dispose();
    _newEmailController.dispose();
    _passwordController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final canSubmit = _newEmailController.text.trim().contains('@') &&
        _passwordController.text.isNotEmpty;

    return ProfileScaffold(
      title: 'Email address',
      backRoute: '/profile/login-details',
      footer: PayaboButton(
        label: _saving ? 'Saving...' : 'Save changes',
        onPressed: canSubmit && !_saving ? _submit : null,
      ),
      child: Column(
        children: <Widget>[
          PayaboTextField(
            label: 'Current email address',
            variant: PayaboInputVariant.floating,
            controller: _currentEmailController,
            keyboardType: TextInputType.emailAddress,
            enabled: false,
          ),
          const SizedBox(height: PayaboSpacing.md),
          PayaboTextField(
            label: 'Type your new email address',
            variant: PayaboInputVariant.floating,
            controller: _newEmailController,
            keyboardType: TextInputType.emailAddress,
            onChanged: (_) => setState(() {}),
          ),
          const SizedBox(height: PayaboSpacing.md),
          PayaboTextField(
            label: 'Type your password',
            variant: PayaboInputVariant.floating,
            controller: _passwordController,
            obscureText: _hidePassword,
            suffixIcon: IconButton(
              onPressed: () => setState(() => _hidePassword = !_hidePassword),
              icon: Icon(_hidePassword
                  ? Icons.visibility_outlined
                  : Icons.visibility_off_outlined),
            ),
            onChanged: (_) => setState(() {}),
          ),
          if (_error != null) ...<Widget>[
            const SizedBox(height: PayaboSpacing.sm),
            Text(
              _error!,
              style: Theme.of(context)
                  .textTheme
                  .bodySmall
                  ?.copyWith(color: PayaboColors.danger),
            ),
          ],
        ],
      ),
    );
  }

  Future<void> _submit() async {
    setState(() {
      _saving = true;
      _error = null;
    });

    final email = _newEmailController.text.trim();
    if (!email.contains('@')) {
      setState(() {
        _error = 'Enter a valid email address.';
        _saving = false;
      });
      return;
    }

    await ref.read(profileControllerProvider.notifier).updateLoginEmail(email);
    if (mounted) {
      context.go('/profile/login-details');
    }
  }
}
