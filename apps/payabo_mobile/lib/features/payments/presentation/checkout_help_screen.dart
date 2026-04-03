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

class CheckoutHelpScreen extends ConsumerStatefulWidget {
  const CheckoutHelpScreen({super.key});

  @override
  ConsumerState<CheckoutHelpScreen> createState() => _CheckoutHelpScreenState();
}

class _CheckoutHelpScreenState extends ConsumerState<CheckoutHelpScreen> {
  bool _isSubmitting = false;
  String? _error;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final summary = ref.watch(paymentOrderSummaryProvider);
    final friend = ref.watch(selectedPaymentFriendProvider);
    final friendMessage = ref.watch(paymentFriendMessageProvider);
    final orderId = ref.watch(paymentOrderIdProvider);

    return PaymentFlowScaffold(
      title: 'Review your order',
      onBack: () => context.go('/payments/friends/message'),
      onClose: () => context.go('/pay'),
      footer: PayaboButton(
        label: _isSubmitting ? 'Confirming...' : 'Confirm payment',
        onPressed: _isSubmitting || friend == null
            ? null
            : () => _confirmPayment(orderId),
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
          if (friend != null)
            PayaboCard(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Row(
                    children: <Widget>[
                      CircleAvatar(
                        radius: 20,
                        backgroundColor: c.background,
                        child: Text(friend.firstName.substring(0, 1)),
                      ),
                      const SizedBox(width: PayaboSpacing.md),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: <Widget>[
                            Text(friend.displayName,
                                style: Theme.of(context).textTheme.titleSmall),
                            Text(friend.relationship,
                                style: Theme.of(context).textTheme.bodySmall),
                          ],
                        ),
                      ),
                    ],
                  ),
                  if (friendMessage.isNotEmpty) ...<Widget>[
                    const SizedBox(height: PayaboSpacing.md),
                    Container(
                      width: double.infinity,
                      padding: const EdgeInsets.all(PayaboSpacing.md),
                      decoration: BoxDecoration(
                        color: c.background,
                        borderRadius: BorderRadius.circular(8),
                      ),
                      child: Text(friendMessage),
                    ),
                  ],
                ],
              ),
            )
          else
            const PayaboCard(
                child: Text('Please select a friend to continue.')),
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
