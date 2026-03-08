import 'package:flutter/material.dart';

import '../../../../shared/theme/payabo_colors.dart';
import '../../../../shared/theme/payabo_radii.dart';
import '../../../../shared/theme/payabo_shadows.dart';
import '../../../../shared/theme/payabo_spacing.dart';

enum SpendingSection {
  overview,
  transactions,
  budgets,
  accounts,
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
    }
  }
}

class SpendingSectionPills extends StatelessWidget {
  const SpendingSectionPills({
    super.key,
    required this.selectedSection,
    required this.onSelected,
  });

  final SpendingSection selectedSection;
  final ValueChanged<SpendingSection> onSelected;

  @override
  Widget build(BuildContext context) {
    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      child: Row(
        children: SpendingSection.values
            .map(
              (SpendingSection section) => Padding(
                padding: const EdgeInsets.only(right: PayaboSpacing.sm),
                child: _SpendingSectionPill(
                  label: section.label,
                  selected: section == selectedSection,
                  onTap: () => onSelected(section),
                ),
              ),
            )
            .toList(growable: false),
      ),
    );
  }
}

class _SpendingSectionPill extends StatelessWidget {
  const _SpendingSectionPill({
    required this.label,
    required this.selected,
    required this.onTap,
  });

  static const Color _selectedColor = PayaboColors.primary;
  static const Color _selectedHover = PayaboColors.primaryHover;
  static const Color _surfaceColor = Color(0xFFFFFAF5);
  static const Color _borderColor = Color(0xFFF0DDCE);
  static const Color _textColor = PayaboColors.accentBrownMuted;

  final String label;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final Color backgroundColor = selected ? _selectedColor : _surfaceColor;
    final Color foregroundColor = selected ? PayaboColors.white : _textColor;

    return AnimatedContainer(
      duration: const Duration(milliseconds: 180),
      curve: Curves.easeOut,
      decoration: BoxDecoration(
        color: backgroundColor,
        borderRadius: PayaboRadii.radiusPill,
        border: Border.all(
          color: selected ? _selectedHover : _borderColor,
        ),
        boxShadow: selected ? PayaboShadows.soft : const <BoxShadow>[],
      ),
      child: Material(
        color: Colors.transparent,
        child: InkWell(
          onTap: onTap,
          borderRadius: PayaboRadii.radiusPill,
          child: Padding(
            padding: const EdgeInsets.symmetric(
              horizontal: PayaboSpacing.lg,
              vertical: PayaboSpacing.md,
            ),
            child: Text(
              label,
              style: Theme.of(context).textTheme.titleSmall?.copyWith(
                    color: foregroundColor,
                    fontWeight: FontWeight.w700,
                  ),
            ),
          ),
        ),
      ),
    );
  }
}
