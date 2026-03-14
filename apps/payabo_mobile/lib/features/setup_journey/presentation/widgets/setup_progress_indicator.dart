import 'package:flutter/material.dart';

import '../../../../shared/theme/payabo_color_resolver.dart';

/// Subtle segmented progress indicator for the setup journey.
///
/// Renders one small segment per step. Active/completed segments
/// use the brand primary; inactive segments use the default border.
/// Animated via [AnimatedContainer] for smooth transitions.
class SetupProgressIndicator extends StatelessWidget {
  const SetupProgressIndicator({
    super.key,
    required this.currentStep,
    required this.totalSteps,
  });

  final int currentStep;
  final int totalSteps;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 32, vertical: 12),
      child: Row(
        children: List<Widget>.generate(totalSteps, (int index) {
          final isActive = index == currentStep;
          final isCompleted = index < currentStep;

          return Expanded(
            child: Padding(
              padding: EdgeInsets.only(
                right: index < totalSteps - 1 ? 4 : 0,
              ),
              child: AnimatedContainer(
                duration: const Duration(milliseconds: 300),
                curve: Curves.easeInOut,
                height: 3,
                decoration: BoxDecoration(
                  color: isActive
                      ? c.primary
                      : isCompleted
                          ? c.primary.withValues(alpha: 0.45)
                          : c.borderDefault,
                  borderRadius: BorderRadius.circular(1.5),
                ),
              ),
            ),
          );
        }),
      ),
    );
  }
}
