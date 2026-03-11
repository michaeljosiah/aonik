import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../app/auth/auth_controller.dart';
import '../../../data/api/api_exception.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_button.dart';
import '../../../shared/widgets/payabo_text_field.dart';
import 'auth_flow_scaffold.dart';
import 'onboarding_flow_state.dart';

class LoginScreen extends ConsumerStatefulWidget {
  const LoginScreen({super.key});

  @override
  ConsumerState<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends ConsumerState<LoginScreen> {
  final TextEditingController _emailController = TextEditingController();
  final TextEditingController _passwordController = TextEditingController();
  bool _isPasswordVisible = false;

  @override
  void dispose() {
    _emailController.dispose();
    _passwordController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final authState = ref.watch(authControllerProvider);
    final canSubmit = isValidEmail(_emailController.text) &&
        _passwordController.text.isNotEmpty &&
        !authState.isBusy;

    return AuthFlowScaffold(
      title: 'Nice to see you again.',
      onClose: () => context.go('/intro'),
      useWarmBackground: true,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          PayaboTextField(
            controller: _emailController,
            variant: PayaboInputVariant.floating,
            label: 'Email',
            keyboardType: TextInputType.emailAddress,
            onChanged: (_) => setState(() {}),
          ),
          const SizedBox(height: PayaboSpacing.md),
          PayaboTextField(
            controller: _passwordController,
            variant: PayaboInputVariant.floating,
            label: 'Password',
            obscureText: !_isPasswordVisible,
            onChanged: (_) => setState(() {}),
            suffixIcon: IconButton(
              onPressed: () {
                setState(() {
                  _isPasswordVisible = !_isPasswordVisible;
                });
              },
              icon: Icon(_isPasswordVisible
                  ? Icons.visibility_off_outlined
                  : Icons.visibility_outlined),
            ),
          ),
          const SizedBox(height: PayaboSpacing.xl),
          PayaboButton(
            label: 'Login',
            onPressed: canSubmit
                ? () async {
                    try {
                      await ref
                          .read(authControllerProvider.notifier)
                          .signInWithPassword(
                            email: _emailController.text.trim(),
                            password: _passwordController.text,
                          );

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
                          : 'Unable to sign in right now. Please try again.';

                      ScaffoldMessenger.of(context)
                        ..hideCurrentSnackBar()
                        ..showSnackBar(
                          SnackBar(content: Text(message)),
                        );
                    }
                  }
                : null,
          ),
          const SizedBox(height: PayaboSpacing.lg),
          TextButton(
            onPressed: () => context.go('/auth/forgot-password'),
            child: const Text('Forgot your password?'),
          ),
        ],
      ),
    );
  }
}
