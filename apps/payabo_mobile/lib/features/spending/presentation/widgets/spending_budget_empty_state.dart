import 'package:flutter/material.dart';

import '../../../../shared/theme/payabo_color_resolver.dart';
import '../../../../shared/theme/payabo_spacing.dart';
import '../../../../shared/widgets/payabo_button.dart';

class SpendingBudgetEmptyState extends StatelessWidget {
  const SpendingBudgetEmptyState({
    super.key,
    required this.title,
    required this.description,
    required this.actionLabel,
    required this.onPressed,
    this.caption,
    this.busy = false,
  });

  final String title;
  final String description;
  final String actionLabel;
  final VoidCallback? onPressed;
  final String? caption;
  final bool busy;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Center(
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 420),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            const SpendingBudgetEmptyIllustration(),
            const SizedBox(height: PayaboSpacing.x2),
            Text(
              title,
              textAlign: TextAlign.center,
              style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                    color: c.accentBrown,
                    fontWeight: FontWeight.w800,
                  ),
            ),
            const SizedBox(height: PayaboSpacing.sm),
            Text(
              description,
              textAlign: TextAlign.center,
              style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                    color: c.muted,
                    height: 1.5,
                  ),
            ),
            if (caption != null) ...<Widget>[
              const SizedBox(height: PayaboSpacing.md),
              Text(
                caption!,
                textAlign: TextAlign.center,
                style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color: c.accentBrownMuted,
                      height: 1.45,
                    ),
              ),
            ],
            const SizedBox(height: PayaboSpacing.xl),
            SizedBox(
              width: 220,
              child: PayaboButton(
                key: const Key('budget-empty-create'),
                label: busy ? 'Creating...' : actionLabel,
                size: PayaboButtonSize.lg,
                onPressed: busy ? null : onPressed,
                leading: busy
                    ? SizedBox(
                        width: 16,
                        height: 16,
                        child: CircularProgressIndicator(
                          strokeWidth: 2,
                          valueColor:
                              AlwaysStoppedAnimation<Color>(c.surfaceBase),
                        ),
                      )
                    : const Icon(Icons.add_rounded, size: 18),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class SpendingBudgetEmptyIllustration extends StatelessWidget {
  const SpendingBudgetEmptyIllustration({super.key});

  static const String _heroAsset = 'assets/images/budget-hero.png';

  @override
  Widget build(BuildContext context) {
    return ConstrainedBox(
      constraints: const BoxConstraints(maxWidth: 300, maxHeight: 220),
      child: Image.asset(
        _heroAsset,
        fit: BoxFit.contain,
      ),
    );
  }
}
