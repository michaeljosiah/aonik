import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../data/repositories/repository_providers.dart';
import '../../../data/repositories/spending_repository.dart';
import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_radii.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_screen_title_bar.dart';
import '../../../shared/widgets/payabo_warm_scaffold.dart';

final _merchantHistoryProvider =
    FutureProvider.autoDispose.family<SpendingMerchantHistory, String>(
  (ref, merchantName) =>
      ref.watch(spendingRepositoryProvider).getMerchantHistory(merchantName),
);

class SpendingMerchantDetailScreen extends ConsumerWidget {
  const SpendingMerchantDetailScreen({
    super.key,
    required this.merchantId,
  });

  final String merchantId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.colors;
    final textTheme = Theme.of(context).textTheme;
    final asyncHistory = ref.watch(_merchantHistoryProvider(merchantId));

    return PayaboWarmScaffold(
      body: Column(
        children: <Widget>[
          PayaboScreenTitleBar(
            title: merchantId,
            onBack: () {
              if (context.canPop()) {
                context.pop();
              } else {
                context.go('/spending');
              }
            },
          ),
          Expanded(
            child: asyncHistory.when(
              loading: () =>
                  const Center(child: CircularProgressIndicator()),
              error: (Object error, _) => Center(
                child: Padding(
                  padding: const EdgeInsets.all(PayaboSpacing.xl),
                  child: Text(
                    'Unable to load merchant history.',
                    style: textTheme.bodyLarge?.copyWith(color: c.muted),
                  ),
                ),
              ),
              data: (SpendingMerchantHistory history) => ListView(
                padding: const EdgeInsets.symmetric(
                  horizontal: PayaboSpacing.xl,
                  vertical: PayaboSpacing.lg,
                ),
                children: <Widget>[
                  _StatCard(
                    label: 'Transactions',
                    value: history.transactionCountLabel,
                  ),
                  const SizedBox(height: PayaboSpacing.md),
                  _StatCard(
                    label: 'Average spend',
                    value: history.averageSpendLabel,
                  ),
                  const SizedBox(height: PayaboSpacing.md),
                  _StatCard(
                    label: 'Total spent',
                    value: history.totalSpentLabel,
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _StatCard extends StatelessWidget {
  const _StatCard({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final textTheme = Theme.of(context).textTheme;

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(PayaboSpacing.xl),
      decoration: BoxDecoration(
        color: c.spendingCardWarmElevated,
        borderRadius: PayaboRadii.radiusSm,
        border: Border.all(color: c.spendingQuickActionBorder),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(
            label,
            style: textTheme.labelMedium?.copyWith(
              color: c.accentBrownMuted,
              fontWeight: FontWeight.w600,
            ),
          ),
          const SizedBox(height: PayaboSpacing.xs),
          Text(
            value,
            style: textTheme.headlineSmall?.copyWith(
              color: c.accentBrown,
              fontWeight: FontWeight.w700,
            ),
          ),
        ],
      ),
    );
  }
}
