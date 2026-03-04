import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_button.dart';
import '../../../shared/widgets/payabo_list_row.dart';
import 'payment_flow_scaffold.dart';
import 'payment_flow_state.dart';

class CardSelectionScreen extends ConsumerWidget {
  const CardSelectionScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final flowState = ref.watch(paymentFlowControllerProvider);

    if (flowState.orderId.isEmpty) {
      return PaymentFlowScaffold(
        title: 'Select card',
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

    return PaymentFlowScaffold(
      title: 'Select card',
      onBack: () => context.go('/payments/payment-selection'),
      onClose: () => context.go('/dashboard'),
      footer: PayaboButton(
        label: 'Use another card',
        variant: PayaboButtonVariant.secondary,
        onPressed: () => context.go('/payments/card-details'),
      ),
      child: Column(
        children: <Widget>[
          ...flowState.savedCards.map((card) {
            return Padding(
              padding: const EdgeInsets.only(bottom: PayaboSpacing.md),
              child: PayaboListRow(
                title: '${card.brand} ending in ${card.last4}',
                subtitle: 'Valid until ${card.expiryLabel}',
                leading: const Icon(Icons.credit_card, size: 24),
                onTap: () {
                  ref
                      .read(paymentFlowControllerProvider.notifier)
                      .selectCard(card.id);
                  context.go('/payments/checkout/card');
                },
              ),
            );
          }),
        ],
      ),
    );
  }
}
