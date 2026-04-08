import 'package:flutter/material.dart';

import '../../../../shared/theme/payabo_spacing.dart';

enum SpendingSection {
  overview,
  transactions,
  budgets,
  accounts,
  bills,
}

extension SpendingSectionLabel on SpendingSection {
  String get label {
    switch (this) {
      case SpendingSection.overview:
        return 'Overview';
      case SpendingSection.transactions:
        return 'Transactions';
      case SpendingSection.budgets:
        return 'Budgets';
      case SpendingSection.accounts:
        return 'Accounts';
      case SpendingSection.bills:
        return 'Bills';
    }
  }
}

/// Starling-style horizontal tab bar with an underline indicator.
///
/// By default the colours derive from the current [Theme].  Pass
/// [selectedColor], [unselectedColor] and [indicatorColor] to override
/// — useful when the pills are rendered on a dark-gradient background
/// where the standard theme values would be illegible.
class SpendingSectionPills extends StatelessWidget {
  const SpendingSectionPills({
    super.key,
    required this.selectedSection,
    required this.onSelected,
    this.sections,
    this.selectedColor,
    this.unselectedColor,
    this.indicatorColor,
  });

  final SpendingSection selectedSection;
  final ValueChanged<SpendingSection> onSelected;
  final List<SpendingSection>? sections;

  /// Override colour for the selected tab label.
  final Color? selectedColor;

  /// Override colour for unselected tab labels.
  final Color? unselectedColor;

  /// Override colour for the underline indicator.
  final Color? indicatorColor;

  @override
  Widget build(BuildContext context) {
    final List<SpendingSection> visibleSections =
        sections ?? SpendingSection.values;
    final theme = Theme.of(context);
    final Color resolvedSelected =
        selectedColor ?? theme.colorScheme.onSurface;
    final Color resolvedUnselected = unselectedColor ??
        theme.textTheme.bodySmall?.color?.withValues(alpha: 0.6) ??
        theme.colorScheme.onSurface.withValues(alpha: 0.5);
    final Color resolvedIndicator =
        indicatorColor ?? theme.colorScheme.primary;

    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      child: Row(
        children: visibleSections
            .map(
              (SpendingSection section) {
                final bool selected = section == selectedSection;
                return Padding(
                  padding: const EdgeInsets.only(right: PayaboSpacing.xl),
                  child: GestureDetector(
                    onTap: () => onSelected(section),
                    behavior: HitTestBehavior.opaque,
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      children: <Widget>[
                        Padding(
                          padding: const EdgeInsets.symmetric(
                            vertical: PayaboSpacing.sm,
                          ),
                          child: Text(
                            section.label,
                            style: theme.textTheme.titleSmall?.copyWith(
                              color: selected
                                  ? resolvedSelected
                                  : resolvedUnselected,
                              fontWeight:
                                  selected ? FontWeight.w700 : FontWeight.w500,
                            ),
                          ),
                        ),
                        AnimatedContainer(
                          duration: const Duration(milliseconds: 200),
                          curve: Curves.easeOut,
                          height: 2.5,
                          width: selected ? 40 : 0,
                          decoration: BoxDecoration(
                            color: selected
                                ? resolvedIndicator
                                : Colors.transparent,
                            borderRadius:
                                const BorderRadius.all(Radius.circular(2)),
                          ),
                        ),
                      ],
                    ),
                  ),
                );
              },
            )
            .toList(growable: false),
      ),
    );
  }
}
