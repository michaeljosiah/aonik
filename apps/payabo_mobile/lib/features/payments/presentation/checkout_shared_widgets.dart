import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../data/repositories/order_repository.dart';
import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_card.dart';
import 'payment_flow_state.dart';

class CheckoutSectionTitle extends StatelessWidget {
  const CheckoutSectionTitle({super.key, required this.label});

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

class CheckoutSummaryCard extends StatelessWidget {
  const CheckoutSummaryCard({super.key, required this.children});

  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    return PayaboCard(
      child: Column(children: children),
    );
  }
}

class CheckoutSummaryRow extends StatelessWidget {
  const CheckoutSummaryRow({
    super.key,
    required this.label,
    required this.value,
    this.isLast = false,
  });

  final String label;
  final String value;
  final bool isLast;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Container(
      padding: const EdgeInsets.symmetric(vertical: PayaboSpacing.md),
      decoration: BoxDecoration(
        border: isLast
            ? null
            : Border(
                bottom: BorderSide(color: c.border, width: 1),
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

class CheckoutPricingBreakdown extends ConsumerWidget {
  const CheckoutPricingBreakdown({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.colors;
    final breakdownAsync = ref.watch(paymentPricingBreakdownProvider);

    return breakdownAsync.when(
      loading: () => PayaboCard(
        child: Padding(
          padding: const EdgeInsets.all(PayaboSpacing.lg),
          child: Center(
            child: SizedBox(
              width: 20,
              height: 20,
              child: CircularProgressIndicator(strokeWidth: 2, color: c.muted),
            ),
          ),
        ),
      ),
      error: (_, __) => PayaboCard(
        child: Padding(
          padding: const EdgeInsets.all(PayaboSpacing.md),
          child: Text(
            'Unable to load pricing details.',
            style: Theme.of(context).textTheme.bodySmall?.copyWith(
                  color: c.danger,
                ),
          ),
        ),
      ),
      data: (breakdown) {
        final lines = breakdown.lines;
        return PayaboCard(
          child: Column(
            children: <Widget>[
              for (final line in lines) ...<Widget>[
                if (line.isDivider) Divider(color: c.border, height: 28),
                CheckoutPriceLine(
                  label: line.label,
                  value: line.value,
                  bold: line.bold,
                  subtle: line.subtle,
                  accent: line.accent,
                ),
              ],
            ],
          ),
        );
      },
    );
  }
}

class CheckoutPriceLine extends StatelessWidget {
  const CheckoutPriceLine({
    super.key,
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
    final c = context.colors;
    final textStyle = Theme.of(context).textTheme.bodyMedium?.copyWith(
          color: subtle ? c.muted : c.ink,
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
              color: accent ? c.primary : textStyle.color,
            ),
          ),
        ],
      ),
    );
  }
}
