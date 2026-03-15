import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../app/auth/auth_controller.dart';
import '../../../app/demo/demo_mode.dart';
import '../../../data/api/api_exception.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_button.dart';
import '../../../shared/widgets/payabo_text_field.dart';
import 'auth_flow_scaffold.dart';
import 'onboarding_flow_state.dart';

class ForgotPasswordScreen extends ConsumerStatefulWidget {
  const ForgotPasswordScreen({super.key});

  @override
  ConsumerState<ForgotPasswordScreen> createState() =>
      _ForgotPasswordScreenState();
}

class _ForgotPasswordScreenState extends ConsumerState<ForgotPasswordScreen> {
  final TextEditingController _emailController = TextEditingController();

  @override
  void dispose() {
    _emailController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final authState = ref.watch(authControllerProvider);
    final isDemo = ref.watch(isDemoProvider);
    final canSubmit =
        !isDemo && isValidEmail(_emailController.text) && !authState.isBusy;

    return AuthFlowScaffold(
      title: 'Forgot password',
      description:
          "Please enter the email address used to register on MyBillAfrica, and we'll send you an email with instructions to recover your password.",
      notice: isDemo
          ? const AuthModeNoticeCard(
              title: 'Password recovery is unavailable',
              message:
                  'Demo mode does not connect to live accounts, so password reset is disabled until the API is reachable again.',
              icon: Icons.lock_reset_rounded,
            )
          : null,
      onClose: () => context.go('/auth/login'),
      useWarmBackground: true,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          PayaboTextField(
            label: 'Email',
            variant: PayaboInputVariant.floating,
            controller: _emailController,
            enabled: !isDemo,
            keyboardType: TextInputType.emailAddress,
            onChanged: (_) => setState(() {}),
          ),
          const SizedBox(height: PayaboSpacing.xl),
          PayaboButton(
            label: 'Recover Password',
            onPressed: canSubmit
                ? () async {
                    try {
                      await ref
                          .read(authControllerProvider.notifier)
                          .sendPasswordResetEmail(_emailController.text);

                      if (!context.mounted) {
                        return;
                      }

                      ScaffoldMessenger.of(context).showSnackBar(
                        const SnackBar(
                          content: Text(
                            'If your account exists, recovery instructions have been sent.',
                          ),
                        ),
                      );
                    } catch (error) {
                      if (!context.mounted) {
                        return;
                      }

                      final message = error is ApiException
                          ? error.message
                          : 'Unable to process your request right now.';

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
}
