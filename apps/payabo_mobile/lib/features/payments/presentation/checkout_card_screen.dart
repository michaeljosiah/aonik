import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../data/api/api_exception.dart';
import '../../../data/repositories/repository_providers.dart';
import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_button.dart';
import '../../../shared/widgets/payabo_card.dart';
import 'checkout_shared_widgets.dart';
import 'payment_flow_scaffold.dart';
import 'payment_flow_state.dart';

class CheckoutCardScreen extends ConsumerStatefulWidget {
  const CheckoutCardScreen({super.key});

  @override
  ConsumerState<CheckoutCardScreen> createState() => _CheckoutCardScreenState();
}

class _CheckoutCardScreenState extends ConsumerState<CheckoutCardScreen> {
  bool _isSubmitting = false;
  String? _error;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final summary = ref.watch(paymentOrderSummaryProvider);
    final selectedCard = ref.watch(selectedPaymentCardProvider);
    final orderId = ref.watch(paymentOrderIdProvider);

    return PaymentFlowScaffold(
      title: 'Review your order',
      onBack: () => context.go('/payments/card-selection'),
      onClose: () => context.go('/pay'),
      footer: PayaboButton(
        label: _isSubmitting ? 'Confirming...' : 'Confirm payment',
        onPressed: _isSubmitting ? null : () => _confirmPayment(orderId),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          const CheckoutSectionTitle(label: 'Service details'),
          CheckoutSummaryCard(
            children: <Widget>[
              CheckoutSummaryRow(label: 'Biller', value: summary.providerName),
              CheckoutSummaryRow(
                label: 'Service details',
                value:
                    '${summary.serviceType}\nCard ID #${summary.smartCardId}',
              ),
              CheckoutSummaryRow(label: 'Amount', value: summary.amount, isLast: true),
            ],
          ),
          const SizedBox(height: PayaboSpacing.lg),
          const CheckoutSectionTitle(label: 'Payment details'),
          PayaboCard(
            child: Row(
              children: <Widget>[
                Icon(Icons.credit_card, size: 28, color: c.muted),
                const SizedBox(width: PayaboSpacing.md),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Text(
                        selectedCard == null
                            ? 'Card details entered manually'
                            : '${selectedCard.brand} ending in ${selectedCard.last4}',
                        style: Theme.of(context).textTheme.titleSmall,
                      ),
                      const SizedBox(height: 4),
                      Text(
                        selectedCard == null
                            ? 'Saved card not selected'
                            : 'Valid until ${selectedCard.expiryLabel}',
                        style: Theme.of(context).textTheme.bodySmall,
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: PayaboSpacing.lg),
          const CheckoutPricingBreakdown(),
          if (_error != null) ...<Widget>[
            const SizedBox(height: PayaboSpacing.md),
            Text(
              _error!,
              style: Theme.of(context).textTheme.bodySmall?.copyWith(
                    color: c.danger,
                  ),
            ),
          ],
        ],
      ),
    );
  }

  Future<void> _confirmPayment(String orderId) async {
    if (orderId.isEmpty) {
      setState(() {
        _error = 'No order draft found. Please restart payment.';
      });
      return;
    }

    setState(() {
      _isSubmitting = true;
      _error = null;
    });

    try {
      await ref
          .read(paymentFlowControllerProvider.notifier)
          .createPaymentIntent(ref.read(paymentRepositoryProvider));

      if (mounted) {
        context.go('/payments/thank-you');
      }
    } catch (e) {
      if (!mounted) {
        return;
      }

      setState(() {
        _error = e is ApiException
            ? e.message
            : 'Unable to confirm payment right now. Please try again.';
      });
    } finally {
      if (mounted) {
        setState(() {
          _isSubmitting = false;
        });
      }
    }
  }
}
