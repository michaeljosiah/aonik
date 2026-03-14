import 'package:flutter/material.dart';

import '../theme/payabo_color_resolver.dart';
import '../theme/payabo_spacing.dart';

class PayaboStepProgressBar extends StatelessWidget {
  const PayaboStepProgressBar({
    super.key,
    required this.steps,
    required this.currentStep,
  });

  final List<String> steps;
  final int currentStep;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: List<Widget>.generate(steps.length, (index) {
        final isActive = index < currentStep;
        final isCurrent = index == currentStep;

        return Expanded(
          child: _ProgressNode(
            label: steps[index],
            isActive: isActive,
            isCurrent: isCurrent,
            size: 16,
            showConnector: index != steps.length - 1,
            connectorInset: 8,
          ),
        );
      }),
    );
  }
}

class PayaboSmallProgressBar extends StatelessWidget {
  const PayaboSmallProgressBar({
    super.key,
    required this.steps,
    required this.currentStep,
  });

  final List<String> steps;
  final int currentStep;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: List<Widget>.generate(steps.length, (index) {
        final isActive = index < currentStep;
        final isCurrent = index == currentStep;

        return Expanded(
          child: _ProgressNode(
            label: steps[index],
            isActive: isActive,
            isCurrent: isCurrent,
            size: 30,
            showConnector: index != steps.length - 1,
            connectorInset: 15,
          ),
        );
      }),
    );
  }
}

class _ProgressNode extends StatelessWidget {
  const _ProgressNode({
    required this.label,
    required this.isActive,
    required this.isCurrent,
    required this.size,
    required this.showConnector,
    required this.connectorInset,
  });

  final String label;
  final bool isActive;
  final bool isCurrent;
  final double size;
  final bool showConnector;
  final double connectorInset;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final bool highlighted = isActive || isCurrent;

    final Color labelColor = highlighted ? c.ink : c.border;
    final Color circleFill = highlighted ? c.primary : c.background;
    final Color circleBorder = highlighted ? c.primary : c.border;

    return Column(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        Text(
          label,
          textAlign: TextAlign.center,
          style: Theme.of(context).textTheme.bodySmall?.copyWith(
                color: labelColor,
                fontWeight: FontWeight.w700,
              ),
        ),
        const SizedBox(height: PayaboSpacing.sm),
        Stack(
          clipBehavior: Clip.none,
          alignment: Alignment.center,
          children: <Widget>[
            if (showConnector)
              Positioned(
                left: size / 2,
                right: -size / 2,
                child: Divider(
                  color: c.border,
                  height: 2,
                  thickness: 2,
                  indent: connectorInset,
                  endIndent: connectorInset,
                ),
              ),
            Container(
              width: size,
              height: size,
              decoration: BoxDecoration(
                color: circleFill,
                borderRadius: BorderRadius.circular(size / 2),
                border: Border.all(color: circleBorder, width: 2),
              ),
              alignment: Alignment.center,
              child: highlighted
                  ? Icon(
                      Icons.check,
                      size: size * 0.56,
                      color: Colors.white,
                    )
                  : null,
            ),
          ],
        ),
      ],
    );
  }
}
