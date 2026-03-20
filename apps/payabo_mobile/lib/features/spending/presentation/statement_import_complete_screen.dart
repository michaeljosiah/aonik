import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_radii.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_button.dart';
import '../../../shared/widgets/payabo_screen_title_bar.dart';
import '../../../shared/widgets/payabo_warm_scaffold.dart';

class StatementImportCompleteScreen extends StatelessWidget {
  const StatementImportCompleteScreen({
    super.key,
    required this.importId,
    required this.rowsImported,
    required this.rowsDuplicate,
    required this.rowsFailed,
    required this.status,
    required this.fileName,
  });

  final String importId;
  final int rowsImported;
  final int rowsDuplicate;
  final int rowsFailed;
  final String status;
  final String fileName;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return PayaboWarmScaffold(
      body: Column(
        children: <Widget>[
          PayaboScreenTitleBar(
            title: 'Import complete',
            onBack: () => context.go('/spending/accounts'),
          ),
          Expanded(
            child: ListView(
              padding: const EdgeInsets.symmetric(
                horizontal: PayaboSpacing.xl,
              ),
              children: <Widget>[
                const SizedBox(height: PayaboSpacing.xl),

                // ── Success hero ───────────────────────────
                Center(
                  child: Container(
                    width: 72,
                    height: 72,
                    decoration: BoxDecoration(
                      color: c.success.withValues(alpha: 0.14),
                      borderRadius: BorderRadius.circular(24),
                    ),
                    child: Icon(
                      Icons.check_circle_outline,
                      color: c.success,
                      size: 40,
                    ),
                  ),
                ),
                const SizedBox(height: PayaboSpacing.lg),
                Center(
                  child: Text(
                    'Import successful',
                    style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                          color: c.accentBrown,
                          fontWeight: FontWeight.w700,
                        ),
                  ),
                ),
                const SizedBox(height: PayaboSpacing.sm),
                Center(
                  child: Text(
                    fileName,
                    style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                          color: c.accentBrownMuted,
                        ),
                  ),
                ),

                const SizedBox(height: PayaboSpacing.x3),

                // ── Results card ───────────────────────────
                Container(
                  decoration: BoxDecoration(
                    color: c.spendingCardWarmElevated,
                    borderRadius: PayaboRadii.radiusSm,
                    border: Border.all(color: c.spendingQuickActionBorder),
                  ),
                  padding: const EdgeInsets.all(PayaboSpacing.xl),
                  child: Column(
                    children: <Widget>[
                      _ResultRow(
                        icon: Icons.check_circle_outline,
                        iconColor: c.success,
                        label: 'Imported',
                        value: '$rowsImported',
                      ),
                      const SizedBox(height: PayaboSpacing.lg),
                      _ResultRow(
                        icon: Icons.content_copy,
                        iconColor: c.warning,
                        label: 'Skipped (duplicate)',
                        value: '$rowsDuplicate',
                      ),
                      const SizedBox(height: PayaboSpacing.lg),
                      _ResultRow(
                        icon: Icons.error_outline,
                        iconColor: c.danger,
                        label: 'Failed',
                        value: '$rowsFailed',
                      ),
                    ],
                  ),
                ),

                if (rowsImported > 0) ...<Widget>[
                  const SizedBox(height: PayaboSpacing.lg),
                  Text(
                    'Imported transactions have been added with a "Pending" review status. '
                    'You can review and categorise them from your account transaction list.',
                    textAlign: TextAlign.center,
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                          color: c.accentBrownMuted,
                          height: 1.45,
                        ),
                  ),
                ],

                const SizedBox(height: PayaboSpacing.x3),

                // ── Actions ────────────────────────────────
                SizedBox(
                  width: double.infinity,
                  child: PayaboButton(
                    label: 'View accounts',
                    leading: const Icon(Icons.account_balance_outlined, size: 18),
                    onPressed: () => context.go('/spending/accounts'),
                  ),
                ),
                const SizedBox(height: PayaboSpacing.md),
                SizedBox(
                  width: double.infinity,
                  child: PayaboButton(
                    label: 'Upload another statement',
                    variant: PayaboButtonVariant.secondary,
                    leading: const Icon(Icons.upload_file_outlined, size: 18),
                    onPressed: () =>
                        context.go('/spending/accounts/upload-statement'),
                  ),
                ),
                const SizedBox(height: PayaboSpacing.md),
                SizedBox(
                  width: double.infinity,
                  child: PayaboButton(
                    label: 'Back to Spend',
                    variant: PayaboButtonVariant.link,
                    onPressed: () => context.go('/spending'),
                  ),
                ),
                const SizedBox(height: PayaboSpacing.x4),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────────
//  Result Row
// ─────────────────────────────────────────────────────────────

class _ResultRow extends StatelessWidget {
  const _ResultRow({
    required this.icon,
    required this.iconColor,
    required this.label,
    required this.value,
  });

  final IconData icon;
  final Color iconColor;
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Row(
      children: <Widget>[
        Container(
          width: 36,
          height: 36,
          decoration: BoxDecoration(
            color: iconColor.withValues(alpha: 0.12),
            borderRadius: BorderRadius.circular(10),
          ),
          child: Icon(icon, color: iconColor, size: 20),
        ),
        const SizedBox(width: PayaboSpacing.md),
        Expanded(
          child: Text(
            label,
            style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                  color: c.accentBrownMuted,
                ),
          ),
        ),
        Text(
          value,
          style: Theme.of(context).textTheme.titleMedium?.copyWith(
                color: c.accentBrown,
                fontWeight: FontWeight.w700,
              ),
        ),
      ],
    );
  }
}
