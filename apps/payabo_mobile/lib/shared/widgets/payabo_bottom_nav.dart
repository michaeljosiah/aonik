import 'package:flutter/material.dart';

import '../theme/payabo_color_resolver.dart';
import '../theme/payabo_spacing.dart';

class PayaboBottomNavItem {
  const PayaboBottomNavItem({
    required this.icon,
    required this.label,
  });

  final IconData icon;
  final String label;
}

class PayaboBottomNav extends StatelessWidget {
  const PayaboBottomNav({
    super.key,
    required this.items,
    required this.currentIndex,
    required this.onTap,
    required this.onCenterTap,
    this.centerIcon = Icons.add,
    this.backgroundOverride,
    this.borderOverride,
    this.shadowOverride,
    this.selectedOverride,
    this.unselectedOverride,
    this.fabBackgroundOverride,
    this.fabShadowOverride,
  });

  final List<PayaboBottomNavItem> items;
  final int currentIndex;
  final ValueChanged<int> onTap;
  final VoidCallback onCenterTap;
  final IconData centerIcon;

  /// Optional color overrides — when provided these take precedence over the
  /// resolved theme colors, allowing pages like the chat screen to blend
  /// the nav bar into their own visual language.
  final Color? backgroundOverride;
  final Color? borderOverride;
  final Color? shadowOverride;
  final Color? selectedOverride;
  final Color? unselectedOverride;
  final Color? fabBackgroundOverride;
  final Color? fabShadowOverride;

  @override
  Widget build(BuildContext context) {
    assert(items.length == 4,
        'PayaboBottomNav expects 4 items around center action.');

    final c = context.colors;
    final bgColor = backgroundOverride ?? c.navBackground;
    final brColor = borderOverride ?? c.navBorder;
    final shColor = shadowOverride ?? c.navShadow;
    final fabBg = fabBackgroundOverride ?? c.navFabBackground;
    final fabSh = fabShadowOverride ?? c.navFabShadow;

    return SafeArea(
      top: false,
      child: DecoratedBox(
        decoration: BoxDecoration(
          color: bgColor,
          boxShadow: <BoxShadow>[
            BoxShadow(
              color: shColor,
              offset: const Offset(0, -1),
              blurRadius: 10,
            ),
          ],
          border: Border(
            top: BorderSide(color: brColor),
          ),
        ),
        child: SizedBox(
          height: 74,
          child: Stack(
            clipBehavior: Clip.none,
            alignment: Alignment.center,
            children: <Widget>[
              Row(
                children: <Widget>[
                  Expanded(child: _buildItem(context, index: 0)),
                  Expanded(child: _buildItem(context, index: 1)),
                  const SizedBox(width: 72),
                  Expanded(child: _buildItem(context, index: 2)),
                  Expanded(child: _buildItem(context, index: 3)),
                ],
              ),
              Positioned(
                top: -18,
                child: Material(
                  color: bgColor,
                  shape: const CircleBorder(),
                  child: InkWell(
                    onTap: onCenterTap,
                    customBorder: const CircleBorder(),
                    child: Ink(
                      width: 58,
                      height: 58,
                      decoration: BoxDecoration(
                        color: fabBg,
                        shape: BoxShape.circle,
                        boxShadow: <BoxShadow>[
                          BoxShadow(
                            color: fabSh,
                            offset: const Offset(0, 4),
                            blurRadius: 12,
                          ),
                        ],
                      ),
                      child: Icon(
                        centerIcon,
                        color: c.isDark ? Colors.black : Colors.white,
                      ),
                    ),
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildItem(BuildContext context, {required int index}) {
    final item = items[index];
    final selected = currentIndex == index;
    final c = context.colors;
    final selColor = selectedOverride ?? c.navSelected;
    final unselColor = unselectedOverride ?? c.navUnselected;

    return InkWell(
      onTap: () => onTap(index),
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: 4),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            Icon(
              item.icon,
              color: selected ? selColor : unselColor,
              size: 21,
            ),
            const SizedBox(height: PayaboSpacing.xs),
            Text(
              item.label,
              style: Theme.of(context).textTheme.bodySmall?.copyWith(
                    color: selected ? selColor : unselColor,
                    fontSize: 12,
                  ),
            ),
          ],
        ),
      ),
    );
  }
}
