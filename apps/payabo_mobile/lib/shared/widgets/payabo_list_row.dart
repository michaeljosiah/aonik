import 'package:flutter/material.dart';

import '../theme/payabo_colors.dart';
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
    return DecoratedBox(
      decoration: const BoxDecoration(
        color: PayaboColors.white,
        borderRadius: PayaboRadii.radiusSm,
        boxShadow: PayaboShadows.medium,
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
                        style: Theme.of(context).textTheme.titleSmall,
                      ),
                      if (subtitle != null) ...<Widget>[
                        const SizedBox(height: PayaboSpacing.xs),
                        Text(
                          subtitle!,
                          style: Theme.of(context).textTheme.bodySmall,
                        ),
                      ],
                    ],
                  ),
                ),
                trailing ??
                    const Icon(Icons.chevron_right, color: PayaboColors.muted),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
