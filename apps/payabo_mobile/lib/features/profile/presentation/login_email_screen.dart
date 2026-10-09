import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../data/api/api_exception.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/validation/payabo_input_validators.dart';
import '../../../shared/widgets/payabo_button.dart';
import '../../../shared/widgets/payabo_text_field.dart';
import 'profile_scaffold.dart';
import 'profile_state.dart';

void _showError(BuildContext context, String message) {
  ScaffoldMessenger.of(context)
    ..hideCurrentSnackBar()
    ..showSnackBar(SnackBar(content: Text(message)));
}

class LoginEmailScreen extends ConsumerStatefulWidget {
  const LoginEmailScreen({super.key});

  @override
  ConsumerState<LoginEmailScreen> createState() => _LoginEmailScreenState();
}

class _LoginEmailScreenState extends ConsumerState<LoginEmailScreen> {
  final TextEditingController _newEmailController = TextEditingController();
  bool _saving = false;
  bool _requested = false;

  @override
  void dispose() {
    _newEmailController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final canSubmit = isValidPayaboEmailAddress(_newEmailController.text);

    return ProfileScaffold(
      title: 'Email address',
      backRoute: '/profile/login-details',
      footer: PayaboButton(
        label: _saving ? 'Requesting...' : 'Send confirmation link',
        onPressed: canSubmit && !_saving ? _submit : null,
      ),
      child: Column(
        children: <Widget>[
          const Text(
            'Sign out and sign in again before requesting an email change. '
            'Your current email stays unchanged until you confirm the new address.',
          ),
          const SizedBox(height: PayaboSpacing.md),
          if (_requested) ...<Widget>[
            const Text(
              'If the request is eligible, check your new email for a confirmation link. '
              'If no email arrives, sign in again and retry.',
            ),
            const SizedBox(height: PayaboSpacing.md),
          ],
          PayaboTextField(
            label: 'Type your new email address',
            variant: PayaboInputVariant.floating,
            controller: _newEmailController,
            keyboardType: TextInputType.emailAddress,
            onChanged: (_) => setState(() {}),
          ),
        ],
      ),
    );
  }

  Future<void> _submit() async {
    final email = _newEmailController.text.trim();
    if (!isValidPayaboEmailAddress(email)) {
      _showError(context, 'Enter a valid email address.');
      return;
    }

    setState(() {
      _saving = true;
      _requested = false;
    });

    try {
      await ref.read(profileCoreProvider.notifier).requestEmailChange(
            newEmail: email,
          );

      if (mounted) {
        setState(() {
          _requested = true;
          _newEmailController.clear();
        });
      }
    } catch (error) {
      final message = error is ApiException &&
              (error.statusCode == 401 || error.statusCode == 403)
          ? 'Please sign in again before requesting an email change.'
          : error is ApiException
              ? error.message
              : 'Unable to request an email change right now.';
      if (mounted) {
        _showError(context, message);
      }
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }
}
