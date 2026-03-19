import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../shared/theme/payabo_colors.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_button.dart';
import '../../../shared/widgets/payabo_text_field.dart';
import 'payment_flow_scaffold.dart';
import 'payment_flow_state.dart';

class AddFriendScreen extends ConsumerStatefulWidget {
  const AddFriendScreen({super.key});

  @override
  ConsumerState<AddFriendScreen> createState() => _AddFriendScreenState();
}

class _AddFriendScreenState extends ConsumerState<AddFriendScreen> {
  final TextEditingController _firstNameController = TextEditingController();
  final TextEditingController _lastNameController = TextEditingController();
  final TextEditingController _emailController = TextEditingController();
  final TextEditingController _relationshipController = TextEditingController();
  bool _saveFriend = true;
  String? _error;

  @override
  void dispose() {
    _firstNameController.dispose();
    _lastNameController.dispose();
    _emailController.dispose();
    _relationshipController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return PaymentFlowScaffold(
      title: 'Request help with payment',
      onBack: () => context.go('/payments/friends'),
      onClose: () => context.go('/pay'),
      footer: PayaboButton(
        label: 'Request help',
        onPressed: _submit,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Text(
            'Please enter the details of the friend or family member that will be helping to pay this bill.',
            style: Theme.of(context).textTheme.bodyMedium,
          ),
          const SizedBox(height: PayaboSpacing.lg),
          PayaboTextField(
            label: 'Friend first name',
            variant: PayaboInputVariant.floating,
            controller: _firstNameController,
            hintText: 'Enter your friend first name',
          ),
          const SizedBox(height: PayaboSpacing.md),
          PayaboTextField(
            label: 'Friend last name',
            variant: PayaboInputVariant.floating,
            controller: _lastNameController,
            hintText: 'Enter your friend last name',
          ),
          const SizedBox(height: PayaboSpacing.md),
          PayaboTextField(
            label: 'Friend email',
            variant: PayaboInputVariant.floating,
            controller: _emailController,
            keyboardType: TextInputType.emailAddress,
            hintText: 'Enter your friend email',
          ),
          const SizedBox(height: PayaboSpacing.md),
          PayaboTextField(
            label: 'Relationship (optional)',
            variant: PayaboInputVariant.floating,
            controller: _relationshipController,
            hintText: 'Select or enter relationship',
          ),
          const SizedBox(height: PayaboSpacing.md),
          SwitchListTile.adaptive(
            contentPadding: EdgeInsets.zero,
            value: _saveFriend,
            onChanged: (value) {
              setState(() {
                _saveFriend = value;
              });
            },
            title: const Text('Save friend'),
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

  void _submit() {
    final first = _firstNameController.text.trim();
    final last = _lastNameController.text.trim();
    final email = _emailController.text.trim();

    if (first.isEmpty || last.isEmpty || email.isEmpty) {
      setState(() {
        _error = 'First name, last name and email are required.';
      });
      return;
    }

    ref.read(paymentFlowControllerProvider.notifier).addFriend(
          firstName: first,
          lastName: last,
          email: email,
          relationship: _relationshipController.text.trim(),
          saveAsFavorite: _saveFriend,
        );

    context.go('/payments/friends/message');
  }
}
