import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../data/api/api_exception.dart';
import '../../../shared/theme/payabo_colors.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_button.dart';
import '../../../shared/widgets/payabo_text_field.dart';
import 'profile_scaffold.dart';
import 'profile_state.dart';

class EditContactScreen extends ConsumerStatefulWidget {
  const EditContactScreen({super.key});

  @override
  ConsumerState<EditContactScreen> createState() => _EditContactScreenState();
}

class _EditContactScreenState extends ConsumerState<EditContactScreen> {
  late final TextEditingController _phoneController;
  String? _error;
  bool _saving = false;

  @override
  void initState() {
    super.initState();
    _phoneController =
        TextEditingController(text: ref.read(profileControllerProvider).phone);
  }

  @override
  void dispose() {
    _phoneController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return ProfileScaffold(
      title: 'Contact number',
      backRoute: '/profile/personal-details',
      footer: PayaboButton(
        label: _saving ? 'Saving...' : 'Verify new number',
        onPressed: _saving ? null : _submit,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          PayaboTextField(
            label: 'Mobile number',
            variant: PayaboInputVariant.floating,
            controller: _phoneController,
            keyboardType: TextInputType.phone,
            hintText: '123 456 789',
          ),
          const SizedBox(height: PayaboSpacing.md),
          Text(
            "We'll send you a text message with a verification code. Message and data rates may apply.",
            style: Theme.of(context).textTheme.bodySmall,
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
    final phone = _phoneController.text.trim();
    if (phone.isEmpty) {
      setState(() {
        _error = 'Mobile number is required.';
      });
      return;
    }

    setState(() {
      _saving = true;
      _error = null;
    });

    try {
      await ref.read(profileControllerProvider.notifier).updatePhone(phone);

      if (mounted) {
        context.go('/profile/personal-details');
      }
    } catch (error) {
      setState(() {
        _error = error is ApiException
            ? error.message
            : 'Unable to update your phone number right now.';
        _saving = false;
      });
    }
  }
}
