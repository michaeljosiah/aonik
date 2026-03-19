import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_button.dart';
import 'payment_flow_scaffold.dart';
import 'payment_flow_state.dart';

class FriendMessageScreen extends ConsumerStatefulWidget {
  const FriendMessageScreen({super.key});

  @override
  ConsumerState<FriendMessageScreen> createState() =>
      _FriendMessageScreenState();
}

class _FriendMessageScreenState extends ConsumerState<FriendMessageScreen> {
  late final TextEditingController _messageController;

  @override
  void initState() {
    super.initState();
    _messageController = TextEditingController(
      text: ref.read(paymentFriendMessageProvider),
    );
  }

  @override
  void dispose() {
    _messageController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final friend = ref.watch(selectedPaymentFriendProvider);
    final message = ref.watch(paymentFriendMessageProvider);

    if (_messageController.text.isEmpty && message.isNotEmpty) {
      _messageController.text = message;
    }

    return PaymentFlowScaffold(
      title: 'Send message to friend',
      onBack: () => context.go('/payments/friends'),
      onClose: () => context.go('/pay'),
      footer: PayaboButton(
        label: 'Send message',
        onPressed: friend == null
            ? null
            : () {
                ref
                    .read(paymentFlowControllerProvider.notifier)
                    .setFriendMessage(
                      _messageController.text.trim(),
                      persist: true,
                    );
                context.go('/payments/checkout/help');
              },
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Text(
            friend == null
                ? 'Select a friend before sending a message.'
                : 'Your message will be sent to ${friend.displayName}.',
            style: Theme.of(context).textTheme.bodyMedium,
          ),
          const SizedBox(height: PayaboSpacing.lg),
          Text(
            'Message',
            style: Theme.of(context).textTheme.titleSmall,
          ),
          const SizedBox(height: PayaboSpacing.xs),
          TextField(
            controller: _messageController,
            maxLines: 5,
            decoration: const InputDecoration(
              hintText: 'Enter the message to send to your friend',
            ),
            onChanged: (value) {
              ref
                  .read(paymentFlowControllerProvider.notifier)
                  .setFriendMessage(value);
            },
          ),
          const SizedBox(height: PayaboSpacing.lg),
          TextButton(
            onPressed: () {
              ref
                  .read(paymentFlowControllerProvider.notifier)
                  .setFriendMessage('', persist: true);
              _messageController.clear();
              context.go('/payments/checkout/help');
            },
            child: const Text('Skip message'),
          ),
          if (friend == null) ...<Widget>[
            const SizedBox(height: PayaboSpacing.sm),
            Text(
              'Please go back and choose a friend first.',
              style: Theme.of(context)
                  .textTheme
                  .bodySmall
                  ?.copyWith(color: c.danger),
            ),
          ],
        ],
      ),
    );
  }
}
