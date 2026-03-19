import 'package:flutter/material.dart';

import '../../../../shared/theme/payabo_color_resolver.dart';
import '../../../../shared/theme/payabo_radii.dart';
import '../../../../shared/theme/payabo_spacing.dart';
import '../spending_budget_data.dart';

/// Result of the budget-category creation picker.
///
/// When [categoryId] is non-null, the user chose a predefined template.
/// When [categoryId] is null, the user chose "Custom budget".
class BudgetCategoryPickerResult {
  const BudgetCategoryPickerResult({this.categoryId});

  final String? categoryId;
}

/// Shows a bottom-sheet that lets the user pick a predefined budget category
/// template (or fall back to a custom budget).
///
/// [existingCategoryIds] is used to filter out templates the user has already
/// created, preventing duplicates.
///
/// Returns a [BudgetCategoryPickerResult] if a selection was made, or `null`
/// if the sheet was dismissed.
Future<BudgetCategoryPickerResult?> showBudgetCategoryPicker({
  required BuildContext context,
  required Set<String> existingCategoryIds,
}) {
  return showModalBottomSheet<BudgetCategoryPickerResult>(
    context: context,
    backgroundColor: context.colors.surfaceWarmElevated,
    shape: const RoundedRectangleBorder(
      borderRadius: BorderRadius.vertical(
        top: Radius.circular(PayaboRadii.xl),
      ),
    ),
    isScrollControlled: true,
    builder: (BuildContext context) {
      return _BudgetCategoryPickerSheet(
        existingCategoryIds: existingCategoryIds,
      );
    },
  );
}

class _BudgetCategoryPickerSheet extends StatelessWidget {
  const _BudgetCategoryPickerSheet({
    required this.existingCategoryIds,
  });

  final Set<String> existingCategoryIds;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    final List<SpendingBudgetCategory> availableTemplates =
        allBudgetCategoryTemplates
            .where(
              (SpendingBudgetCategory cat) =>
                  !existingCategoryIds.contains(cat.id),
            )
            .toList();

    return SafeArea(
      top: false,
      child: ConstrainedBox(
        constraints: BoxConstraints(
          maxHeight: MediaQuery.of(context).size.height * 0.85,
        ),
        child: SingleChildScrollView(
          padding: const EdgeInsets.fromLTRB(
            PayaboSpacing.xl,
            PayaboSpacing.lg,
            PayaboSpacing.xl,
            PayaboSpacing.xl,
          ),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
            Center(
              child: Container(
                width: 56,
                height: 4,
                decoration: BoxDecoration(
                  color: c.borderWarm,
                  borderRadius: BorderRadius.circular(PayaboRadii.pill),
                ),
              ),
            ),
            const SizedBox(height: PayaboSpacing.lg),
            Text(
              'Choose a budget',
              style: Theme.of(context).textTheme.titleLarge?.copyWith(
                    color: c.accentBrown,
                    fontWeight: FontWeight.w700,
                  ),
            ),
            const SizedBox(height: PayaboSpacing.xs),
            Text(
              'Pick a template to get started quickly, or create a blank custom budget.',
              style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                    color: c.accentBrownMuted,
                  ),
            ),
            const SizedBox(height: PayaboSpacing.lg),
            ...availableTemplates.map(
              (SpendingBudgetCategory template) => Padding(
                padding: const EdgeInsets.only(bottom: PayaboSpacing.sm),
                child: _BudgetTemplateTile(
                  template: template,
                  onTap: () => Navigator.of(context).pop(
                    BudgetCategoryPickerResult(categoryId: template.id),
                  ),
                ),
              ),
            ),
            if (availableTemplates.isEmpty)
              Padding(
                padding: const EdgeInsets.only(bottom: PayaboSpacing.sm),
                child: Text(
                  'All predefined budgets have been created.',
                  style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                        color: c.accentBrownMuted,
                      ),
                ),
              ),
            const SizedBox(height: PayaboSpacing.sm),
            _CustomBudgetTile(
              onTap: () => Navigator.of(context).pop(
                const BudgetCategoryPickerResult(),
              ),
            ),
          ],
        ),
        ),
      ),
    );
  }
}

class _BudgetTemplateTile extends StatelessWidget {
  const _BudgetTemplateTile({
    required this.template,
    required this.onTap,
  });

  final SpendingBudgetCategory template;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final Color accentColor = template.accentRole.resolve(c);

    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(PayaboRadii.xl),
        child: Ink(
          decoration: BoxDecoration(
            color: c.spendingCardWarmElevated,
            borderRadius: BorderRadius.circular(PayaboRadii.xl),
            border: Border.all(color: c.spendingQuickActionBorder),
          ),
          padding: const EdgeInsets.all(PayaboSpacing.lg),
          child: Row(
            children: <Widget>[
              Container(
                width: 44,
                height: 44,
                decoration: BoxDecoration(
                  color: accentColor.withValues(alpha: 0.12),
                  shape: BoxShape.circle,
                ),
                child: Icon(template.icon, color: accentColor, size: 24),
              ),
              const SizedBox(width: PayaboSpacing.md),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text(
                      template.name,
                      style:
                          Theme.of(context).textTheme.titleMedium?.copyWith(
                                color: c.accentBrown,
                                fontWeight: FontWeight.w700,
                              ),
                    ),
                    if (template.description != null) ...<Widget>[
                      const SizedBox(height: PayaboSpacing.xs),
                      Text(
                        template.description!,
                        style:
                            Theme.of(context).textTheme.bodySmall?.copyWith(
                                  color: c.accentBrownMuted,
                                ),
                      ),
                    ],
                  ],
                ),
              ),
              const SizedBox(width: PayaboSpacing.sm),
              Icon(
                Icons.arrow_forward_ios_rounded,
                size: 16,
                color: c.accentBrownMuted,
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _CustomBudgetTile extends StatelessWidget {
  const _CustomBudgetTile({required this.onTap});

  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(PayaboRadii.xl),
        child: Ink(
          decoration: BoxDecoration(
            color: c.surfaceBase,
            borderRadius: BorderRadius.circular(PayaboRadii.xl),
            border: Border.all(color: c.spendingQuickActionBorder),
          ),
          padding: const EdgeInsets.all(PayaboSpacing.lg),
          child: Row(
            children: <Widget>[
              Container(
                width: 44,
                height: 44,
                decoration: BoxDecoration(
                  color: c.primary.withValues(alpha: 0.12),
                  shape: BoxShape.circle,
                ),
                child: Icon(Icons.savings_outlined, color: c.primary, size: 24),
              ),
              const SizedBox(width: PayaboSpacing.md),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text(
                      'Custom budget',
                      style:
                          Theme.of(context).textTheme.titleMedium?.copyWith(
                                color: c.accentBrown,
                                fontWeight: FontWeight.w700,
                              ),
                    ),
                    const SizedBox(height: PayaboSpacing.xs),
                    Text(
                      'Start from scratch with your own name and amounts.',
                      style: Theme.of(context).textTheme.bodySmall?.copyWith(
                            color: c.accentBrownMuted,
                          ),
                    ),
                  ],
                ),
              ),
              const SizedBox(width: PayaboSpacing.sm),
              Icon(
                Icons.arrow_forward_ios_rounded,
                size: 16,
                color: c.accentBrownMuted,
              ),
            ],
          ),
        ),
      ),
    );
  }
}
