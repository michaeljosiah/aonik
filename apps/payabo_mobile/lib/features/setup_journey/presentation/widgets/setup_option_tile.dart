import 'package:flutter/material.dart';

import '../../../../shared/theme/payabo_color_resolver.dart';
import '../../../../shared/theme/payabo_spacing.dart';

/// Selectable option tile for the setup journey.
///
/// Supports both single-select and multi-select modes through the
/// [isSelected] and [onTap] parameters. Selection state is animated
/// via [AnimatedContainer] for smooth transitions (200ms, easeOut).
class SetupOptionTile extends StatelessWidget {
  const SetupOptionTile({
    super.key,
    required this.label,
    required this.isSelected,
    required this.onTap,
    this.icon,
    this.isEnabled = true,
    this.showCheckIndicator = true,
  });

  final String label;
  final bool isSelected;
  final VoidCallback onTap;
  final IconData? icon;
  final bool isEnabled;
  final bool showCheckIndicator;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final textTheme = Theme.of(context).textTheme;

    final backgroundColor = isSelected ? c.surfaceWarm : c.surfaceBase;
    final borderColor = isSelected ? c.borderWarm : c.borderDefault;
    final textColor =
        isEnabled ? c.textPrimary : c.textMuted;
    final iconColor =
        isSelected ? c.headerIconAccent : c.textMuted;

    return GestureDetector(
      onTap: isEnabled ? onTap : null,
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 200),
        curve: Curves.easeOut,
        padding: const EdgeInsets.symmetric(
          horizontal: PayaboSpacing.lg,
          vertical: PayaboSpacing.md,
        ),
        decoration: BoxDecoration(
          color: backgroundColor,
          borderRadius: BorderRadius.circular(12),
          border: Border.all(
            color: borderColor,
            width: isSelected ? 1.5 : 1.0,
          ),
        ),
        child: Row(
          children: <Widget>[
            if (icon != null) ...<Widget>[
              AnimatedContainer(
                duration: const Duration(milliseconds: 200),
                curve: Curves.easeOut,
                width: 36,
                height: 36,
                decoration: BoxDecoration(
                  color: isSelected
                      ? c.surfaceWarmAccent
                      : c.surfaceSubtle,
                  borderRadius: BorderRadius.circular(10),
                ),
                child: Icon(
                  icon,
                  size: 18,
                  color: iconColor,
                ),
              ),
              const SizedBox(width: PayaboSpacing.md),
            ],
            Expanded(
              child: Text(
                label,
                style: textTheme.titleMedium?.copyWith(
                  color: textColor,
                  fontWeight:
                      isSelected ? FontWeight.w600 : FontWeight.w500,
                ),
              ),
            ),
            if (showCheckIndicator)
              AnimatedOpacity(
                duration: const Duration(milliseconds: 200),
                opacity: isSelected ? 1.0 : 0.0,
                child: Icon(
                  Icons.check_circle_rounded,
                  size: 20,
                  color: c.primary,
                ),
              ),
          ],
        ),
      ),
    );
  }
}
