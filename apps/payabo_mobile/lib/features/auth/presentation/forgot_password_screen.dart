import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_button.dart';
import '../../../shared/widgets/payabo_text_field.dart';
import 'auth_flow_scaffold.dart';
import 'onboarding_flow_state.dart';

class ForgotPasswordScreen extends StatefulWidget {
  const ForgotPasswordScreen({super.key});

  @override
  State<ForgotPasswordScreen> createState() => _ForgotPasswordScreenState();
}

class _ForgotPasswordScreenState extends State<ForgotPasswordScreen> {
  final TextEditingController _emailController = TextEditingController();

  @override
  void dispose() {
    _emailController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final canSubmit = isValidEmail(_emailController.text);

    return AuthFlowScaffold(
      title: 'Forgot password',
      description:
          "Please enter the email address used to register on MyBillAfrica, and we'll send you an email with instructions to recover your password.",
      onClose: () => context.go('/auth/login'),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          PayaboTextField(
            label: 'Email',
            variant: PayaboInputVariant.floating,
            controller: _emailController,
            keyboardType: TextInputType.emailAddress,
            onChanged: (_) => setState(() {}),
          ),
          const SizedBox(height: PayaboSpacing.xl),
          PayaboButton(
            label: 'Recover Password',
            onPressed: canSubmit
                ? () {
                    ScaffoldMessenger.of(context).showSnackBar(
                      const SnackBar(
                          content: Text('Recovery email sent (mock).')),
                    );
                  }
                : null,
          ),
        ],
      ),
    );
  }
}
