import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../shared/theme/payabo_colors.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_button.dart';
import '../../../shared/widgets/payabo_list_row.dart';
import '../../../shared/widgets/payabo_modal_sheet.dart';
import 'payment_flow_scaffold.dart';
import 'payment_flow_state.dart';

class PaymentSelectionScreen extends ConsumerWidget {
  const PaymentSelectionScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final orderId = ref.watch(paymentOrderIdProvider);
    final summary = ref.watch(paymentOrderSummaryProvider);

    if (orderId.isEmpty) {
      return PaymentFlowScaffold(
        title: 'Select payment method',
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
      title: 'Select payment method',
      onBack: () => context.go('/payments/service-details'),
      onClose: () => context.go('/dashboard'),
      footer: PayaboButton(
        label: 'View order summary',
        variant: PayaboButtonVariant.secondary,
        onPressed: () => _showOrderSummary(context, summary),
      ),
      child: Column(
        children: <Widget>[
          PayaboListRow(
            title: 'Pay with debit or credit card',
            subtitle: 'Use your saved card or a new card at checkout',
            leading: const Icon(Icons.credit_card_outlined,
                color: PayaboColors.muted),
            onTap: () {
              ref
                  .read(paymentFlowControllerProvider.notifier)
                  .setPaymentMethod(PaymentMethodType.card);
              context.go('/payments/card-selection');
            },
          ),
          const SizedBox(height: PayaboSpacing.md),
          PayaboListRow(
            title: 'Request help with payment',
            subtitle: 'Ask your friends and family for help',
            leading: const Icon(Icons.volunteer_activism_outlined,
                color: PayaboColors.muted),
            onTap: () {
              ref
                  .read(paymentFlowControllerProvider.notifier)
                  .setPaymentMethod(PaymentMethodType.friend);
              context.go('/payments/friends');
            },
          ),
        ],
      ),
    );
  }

  Future<void> _showOrderSummary(
      BuildContext context, PaymentOrderSummary summary) async {
    await showPayaboModalSheet<void>(
      context: context,
      title: 'Order summary',
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          _SummaryItem(
            label: 'Destination country',
            value: summary.countryCode,
          ),
          _SummaryItem(
            label: 'Biller',
            value: summary.providerName,
          ),
          _SummaryItem(
            label: 'Service details',
            value: '${summary.serviceType}\nCard ID #${summary.smartCardId}',
          ),
          _SummaryItem(
            label: 'Amount',
            value: summary.amount,
            isLast: true,
          ),
        ],
      ),
    );
  }
}

class _SummaryItem extends StatelessWidget {
  const _SummaryItem({
    required this.label,
    required this.value,
    this.isLast = false,
  });

  final String label;
  final String value;
  final bool isLast;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(vertical: PayaboSpacing.md),
      decoration: BoxDecoration(
        border: isLast
            ? null
            : const Border(
                bottom: BorderSide(color: PayaboColors.border, width: 1),
              ),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Expanded(
            child: Text(
              label,
              style: Theme.of(context).textTheme.bodySmall,
            ),
          ),
          Expanded(
            child: Text(
              value,
              textAlign: TextAlign.right,
              style: Theme.of(context).textTheme.bodyLarge,
            ),
          ),
        ],
      ),
    );
  }
}
