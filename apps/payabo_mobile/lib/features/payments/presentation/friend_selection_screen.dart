import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../shared/theme/payabo_colors.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_button.dart';
import '../../../shared/widgets/payabo_card.dart';
import 'payment_flow_scaffold.dart';
import 'payment_flow_state.dart';

class FriendSelectionScreen extends ConsumerStatefulWidget {
  const FriendSelectionScreen({super.key});

  @override
  ConsumerState<FriendSelectionScreen> createState() =>
      _FriendSelectionScreenState();
}

class _FriendSelectionScreenState extends ConsumerState<FriendSelectionScreen> {
  final TextEditingController _searchController = TextEditingController();

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final orderId = ref.watch(paymentOrderIdProvider);
    final friendsState = ref.watch(paymentFriendsProvider);

    if (orderId.isEmpty) {
      return PaymentFlowScaffold(
        title: 'Request help with payment',
        onBack: () => context.go('/payments/service-details'),
        onClose: () => context.go('/dashboard'),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            const Text(
              'No draft order found. Please complete service details first.',
            ),
            const SizedBox(height: PayaboSpacing.lg),
            PayaboButton(
              label: 'Back to service details',
              onPressed: () => context.go('/payments/service-details'),
            ),
          ],
        ),
      );
    }

    final query = _searchController.text.trim().toLowerCase();
    final friends = friendsState.where((friend) {
      final haystack =
          '${friend.firstName} ${friend.lastName} ${friend.relationship}'
              .toLowerCase();
      return haystack.contains(query);
    }).toList(growable: false);

    return PaymentFlowScaffold(
      title: 'Request help with payment',
      onBack: () => context.go('/payments/payment-selection'),
      onClose: () => context.go('/dashboard'),
      footer: PayaboButton(
        label: 'Add new friend',
        variant: PayaboButtonVariant.secondary,
        onPressed: () => context.go('/payments/friends/add'),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          TextField(
            controller: _searchController,
            onChanged: (_) => setState(() {}),
            decoration: const InputDecoration(
              hintText: 'Search for a friend',
              prefixIcon: Icon(Icons.search),
            ),
          ),
          const SizedBox(height: PayaboSpacing.lg),
          Text(
            'Please select the friend or family member that will be helping to pay this bill.',
            style: Theme.of(context).textTheme.bodyMedium,
          ),
          const SizedBox(height: PayaboSpacing.md),
          if (friends.isEmpty)
            const _EmptyFriendsState()
          else
            ...friends.map((friend) {
              return Padding(
                padding: const EdgeInsets.only(bottom: PayaboSpacing.sm),
                child: PayaboCard(
                  child: InkWell(
                    onTap: () {
                      ref
                          .read(paymentFlowControllerProvider.notifier)
                          .selectFriend(friend.id);
                      context.go('/payments/friends/message');
                    },
                    child: Row(
                      children: <Widget>[
                        CircleAvatar(
                          radius: 20,
                          backgroundColor: PayaboColors.background,
                          child: Text(
                            friend.firstName.substring(0, 1),
                            style: Theme.of(context).textTheme.titleSmall,
                          ),
                        ),
                        const SizedBox(width: PayaboSpacing.md),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: <Widget>[
                              Row(
                                children: <Widget>[
                                  Expanded(
                                    child: Text(
                                      friend.displayName,
                                      style: Theme.of(context)
                                          .textTheme
                                          .titleSmall,
                                    ),
                                  ),
                                  if (friend.isFavorite)
                                    const Icon(Icons.star,
                                        color: PayaboColors.primary, size: 18),
                                ],
                              ),
                              const SizedBox(height: 2),
                              Text(
                                friend.relationship,
                                style: Theme.of(context).textTheme.bodySmall,
                              ),
                            ],
                          ),
                        ),
                        const Icon(Icons.chevron_right,
                            color: PayaboColors.muted),
                      ],
                    ),
                  ),
                ),
              );
            }),
        ],
      ),
    );
  }
}

class _EmptyFriendsState extends StatelessWidget {
  const _EmptyFriendsState();

  @override
  Widget build(BuildContext context) {
    return const PayaboCard(
      child: Text(
          'No matching friend found. Try another search or add a new friend.'),
    );
  }
}
