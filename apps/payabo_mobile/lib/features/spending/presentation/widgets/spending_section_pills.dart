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

/// Horizontal text-label tabs with an animated underline indicator.
///
/// Pass [selectedColor], [unselectedColor], [indicatorColor] to override
/// on dark-gradient surfaces where the default theme tokens would
/// otherwise read as low-contrast.
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

  /// Label colour of the selected tab. Defaults to `colorScheme.onSurface`.
  final Color? selectedColor;

  /// Label colour of unselected tabs. Defaults to a muted onSurface.
  final Color? unselectedColor;

  /// Underline colour under the selected tab. Defaults to `colorScheme.primary`.
  final Color? indicatorColor;

  static const double _indicatorWidth = 64;

  @override
  Widget build(BuildContext context) {
    final List<SpendingSection> visibleSections =
        sections ?? SpendingSection.values;
    final ThemeData theme = Theme.of(context);
    final TextStyle? titleSmall = theme.textTheme.titleSmall;
    final TextStyle? bodySmall = theme.textTheme.bodySmall;

    final Color resolvedSelected = selectedColor ?? theme.colorScheme.onSurface;
    final Color resolvedUnselected = unselectedColor ??
        bodySmall?.color?.withValues(alpha: 0.6) ??
        theme.colorScheme.onSurface.withValues(alpha: 0.5);
    final Color resolvedIndicator =
        indicatorColor ?? theme.colorScheme.primary;

    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      padding: const EdgeInsets.symmetric(vertical: PayaboSpacing.xs),
      child: Row(
        children: <Widget>[
          for (final SpendingSection section in visibleSections)
            Padding(
              padding: const EdgeInsets.only(right: PayaboSpacing.xl),
              child: _SectionPill(
                label: section.label,
                selected: section == selectedSection,
                onTap: () => onSelected(section),
                selectedColor: resolvedSelected,
                unselectedColor: resolvedUnselected,
                indicatorColor: resolvedIndicator,
                titleStyle: titleSmall,
                indicatorWidth: _indicatorWidth,
              ),
            ),
        ],
      ),
    );
  }
}

class _SectionPill extends StatelessWidget {
  const _SectionPill({
    required this.label,
    required this.selected,
    required this.onTap,
    required this.selectedColor,
    required this.unselectedColor,
    required this.indicatorColor,
    required this.titleStyle,
    required this.indicatorWidth,
  });

  final String label;
  final bool selected;
  final VoidCallback onTap;
  final Color selectedColor;
  final Color unselectedColor;
  final Color indicatorColor;
  final TextStyle? titleStyle;
  final double indicatorWidth;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      behavior: HitTestBehavior.opaque,
      onTap: onTap,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.center,
        children: <Widget>[
          Padding(
            padding: const EdgeInsets.symmetric(vertical: PayaboSpacing.sm),
            child: Text(
              label,
              style: titleStyle?.copyWith(
                fontWeight: selected ? FontWeight.w700 : FontWeight.w500,
                color: selected ? selectedColor : unselectedColor,
              ),
            ),
          ),
          AnimatedContainer(
            duration: const Duration(milliseconds: 200),
            curve: Curves.easeOut,
            height: 2.5,
            width: selected ? indicatorWidth : 0,
            decoration: BoxDecoration(
              color: selected ? indicatorColor : Colors.transparent,
              borderRadius: const BorderRadius.all(Radius.circular(2)),
            ),
          ),
        ],
      ),
    );
  }
}
