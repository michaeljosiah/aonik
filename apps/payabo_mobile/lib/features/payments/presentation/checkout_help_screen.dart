import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../data/repositories/repository_providers.dart';
import '../../../shared/theme/payabo_colors.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_button.dart';
import '../../../shared/widgets/payabo_card.dart';
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
    final summary = ref.watch(paymentOrderSummaryProvider);
    final friend = ref.watch(selectedPaymentFriendProvider);
    final friendMessage = ref.watch(paymentFriendMessageProvider);
    final orderId = ref.watch(paymentOrderIdProvider);

    return PaymentFlowScaffold(
      title: 'Review your order',
      onBack: () => context.go('/payments/friends/message'),
      onClose: () => context.go('/dashboard'),
      footer: PayaboButton(
        label: _isSubmitting ? 'Confirming...' : 'Confirm payment',
        onPressed: _isSubmitting || friend == null
            ? null
            : () => _confirmPayment(orderId),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          const _SectionTitle(label: 'Service details'),
          _SummaryCard(
            children: <Widget>[
              _SummaryRow(label: 'Biller', value: summary.providerName),
              _SummaryRow(
                label: 'Service details',
                value:
                    '${summary.serviceType}\nCard ID #${summary.smartCardId}',
              ),
              _SummaryRow(label: 'Amount', value: summary.amount, isLast: true),
            ],
          ),
          const SizedBox(height: PayaboSpacing.lg),
          const _SectionTitle(label: 'Payment details'),
          if (friend != null)
            PayaboCard(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Row(
                    children: <Widget>[
                      CircleAvatar(
                        radius: 20,
                        backgroundColor: PayaboColors.background,
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
                        color: PayaboColors.background,
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
          const _PricingBreakdown(),
          if (_error != null) ...<Widget>[
            const SizedBox(height: PayaboSpacing.md),
            Text(
              _error!,
              style: Theme.of(context).textTheme.bodySmall?.copyWith(
                    color: PayaboColors.danger,
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
    } catch (_) {
      if (!mounted) {
        return;
      }

      setState(() {
        _error = 'Unable to confirm payment right now. Please try again.';
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

class _SectionTitle extends StatelessWidget {
  const _SectionTitle({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: PayaboSpacing.md),
      child: Text(
        label,
        style: Theme.of(context).textTheme.titleSmall?.copyWith(
              fontWeight: FontWeight.w700,
            ),
      ),
    );
  }
}

class _SummaryCard extends StatelessWidget {
  const _SummaryCard({required this.children});

  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    return PayaboCard(
      child: Column(children: children),
    );
  }
}

class _SummaryRow extends StatelessWidget {
  const _SummaryRow({
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

class _PricingBreakdown extends StatelessWidget {
  const _PricingBreakdown();

  @override
  Widget build(BuildContext context) {
    return const PayaboCard(
      child: Column(
        children: <Widget>[
          _PriceLine(label: 'Rate NGN:GBP', value: '303.5770', subtle: true),
          _PriceLine(label: 'Sub-total', value: 'GBP 5.11', bold: true),
          _PriceLine(label: 'Fees', value: 'GBP 1.99'),
          _PriceLine(label: 'VAT', value: 'GBP 0.30'),
          Divider(color: PayaboColors.border, height: 28),
          _PriceLine(label: 'Total', value: 'GBP 7.40', bold: true),
          SizedBox(height: PayaboSpacing.md),
          _PriceLine(
              label: 'You will earn', value: '74 MBA POINTS', accent: true),
        ],
      ),
    );
  }
}

class _PriceLine extends StatelessWidget {
  const _PriceLine({
    required this.label,
    required this.value,
    this.bold = false,
    this.subtle = false,
    this.accent = false,
  });

  final String label;
  final String value;
  final bool bold;
  final bool subtle;
  final bool accent;

  @override
  Widget build(BuildContext context) {
    final textStyle = Theme.of(context).textTheme.bodyMedium?.copyWith(
          color: subtle ? PayaboColors.muted : PayaboColors.ink,
          fontWeight: bold || accent ? FontWeight.w700 : FontWeight.w400,
        );

    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        children: <Widget>[
          Expanded(
            child: Text(label, style: textStyle),
          ),
          Text(
            value,
            style: textStyle?.copyWith(
              color: accent ? PayaboColors.primary : textStyle.color,
            ),
          ),
        ],
      ),
    );
  }
}
