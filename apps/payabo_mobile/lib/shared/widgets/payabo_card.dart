import 'package:flutter/material.dart';

import '../theme/payabo_color_resolver.dart';
import '../theme/payabo_radii.dart';
import '../theme/payabo_shadows.dart';
import '../theme/payabo_spacing.dart';

class PayaboCard extends StatelessWidget {
  const PayaboCard({
    super.key,
    required this.child,
    this.padding = PayaboSpacing.card,
    this.backgroundColor,
  });

  final Widget child;
  final EdgeInsetsGeometry padding;

  /// If null, the card uses the theme-aware surface color.
  final Color? backgroundColor;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final bg = backgroundColor ?? c.surfaceBase;

    return Container(
      decoration: BoxDecoration(
        color: bg,
        borderRadius: PayaboRadii.radiusSm,
        border: Border.all(color: c.borderStrong),
        boxShadow: c.isDark ? PayaboShadows.soft : PayaboShadows.medium,
      ),
      child: Padding(
        padding: padding,
        child: child,
      ),
    );
  }
}
