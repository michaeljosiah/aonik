import 'package:flutter/material.dart';

import '../theme/payabo_colors.dart';
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
  });

  final List<PayaboBottomNavItem> items;
  final int currentIndex;
  final ValueChanged<int> onTap;
  final VoidCallback onCenterTap;
  final IconData centerIcon;

  @override
  Widget build(BuildContext context) {
    assert(items.length == 4,
        'PayaboBottomNav expects 4 items around center action.');

    final navColor = Theme.of(context).scaffoldBackgroundColor;
    return SafeArea(
      top: false,
      child: DecoratedBox(
        decoration: const BoxDecoration(
          color: PayaboColors.white,
          boxShadow: <BoxShadow>[
            BoxShadow(
              color: Color(0x12000000),
              offset: Offset(0, -1),
              blurRadius: 10,
            ),
          ],
          border: Border(
            top: BorderSide(color: Color(0xFFF0E7DA)),
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
                  color: navColor,
                  shape: const CircleBorder(),
                  child: InkWell(
                    onTap: onCenterTap,
                    customBorder: const CircleBorder(),
                    child: Ink(
                      width: 58,
                      height: 58,
                      decoration: const BoxDecoration(
                        color: PayaboColors.primary,
                        shape: BoxShape.circle,
                        boxShadow: <BoxShadow>[
                          BoxShadow(
                            color: Color(0x26000000),
                            offset: Offset(0, 4),
                            blurRadius: 12,
                          ),
                        ],
                      ),
                      child: Icon(centerIcon, color: PayaboColors.white),
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
    const selectedColor = Color(0xFFC29752);
    const unselectedColor = Color(0xFF99958F);

    return InkWell(
      onTap: () => onTap(index),
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: 4),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            Icon(
              item.icon,
              color: selected ? selectedColor : unselectedColor,
              size: 21,
            ),
            const SizedBox(height: PayaboSpacing.xs),
            Text(
              item.label,
              style: Theme.of(context).textTheme.bodySmall?.copyWith(
                    color: selected ? selectedColor : unselectedColor,
                    fontSize: 12,
                  ),
            ),
          ],
        ),
      ),
    );
  }
}
