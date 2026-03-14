import 'package:flutter/material.dart';

import '../theme/payabo_color_resolver.dart';
import '../theme/payabo_radii.dart';
import '../theme/payabo_shadows.dart';
import '../theme/payabo_spacing.dart';

class PayaboListRow extends StatelessWidget {
  const PayaboListRow({
    super.key,
    required this.title,
    this.subtitle,
    this.leading,
    this.trailing,
    this.onTap,
  });

  final String title;
  final String? subtitle;
  final Widget? leading;
  final Widget? trailing;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return DecoratedBox(
      decoration: BoxDecoration(
        color: c.surfaceBase,
        borderRadius: PayaboRadii.radiusSm,
        border: Border.all(color: c.borderStrong),
        boxShadow: c.isDark ? PayaboShadows.soft : PayaboShadows.medium,
      ),
      child: Material(
        color: Colors.transparent,
        child: InkWell(
          borderRadius: PayaboRadii.radiusSm,
          onTap: onTap,
          child: Padding(
            padding: const EdgeInsets.symmetric(
                horizontal: PayaboSpacing.lg, vertical: PayaboSpacing.lg),
            child: Row(
              children: <Widget>[
                if (leading != null) ...<Widget>[
                  leading!,
                  const SizedBox(width: PayaboSpacing.md),
                ],
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Text(
                        title,
                        style: Theme.of(context).textTheme.titleSmall?.copyWith(
                              color: c.textPrimary,
                            ),
                      ),
                      if (subtitle != null) ...<Widget>[
                        const SizedBox(height: PayaboSpacing.xs),
                        Text(
                          subtitle!,
                          style: Theme.of(context).textTheme.bodySmall?.copyWith(
                                color: c.textMuted,
                              ),
                        ),
                      ],
                    ],
                  ),
                ),
                trailing ??
                    Icon(Icons.chevron_right, color: c.muted),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
