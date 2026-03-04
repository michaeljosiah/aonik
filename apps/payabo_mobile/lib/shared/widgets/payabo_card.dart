import 'package:flutter/material.dart';

import '../theme/payabo_borders.dart';
import '../theme/payabo_colors.dart';
import '../theme/payabo_radii.dart';
import '../theme/payabo_shadows.dart';
import '../theme/payabo_spacing.dart';

class PayaboCard extends StatelessWidget {
  const PayaboCard({
    super.key,
    required this.child,
    this.padding = PayaboSpacing.card,
    this.backgroundColor = PayaboColors.white,
  });

  final Widget child;
  final EdgeInsetsGeometry padding;
  final Color backgroundColor;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: const BoxDecoration(
        color: PayaboColors.white,
        borderRadius: PayaboRadii.radiusSm,
        border: Border.fromBorderSide(PayaboBorders.strongBorder),
        boxShadow: PayaboShadows.medium,
      ).copyWith(color: backgroundColor),
      child: Padding(
        padding: padding,
        child: child,
      ),
    );
  }
}
