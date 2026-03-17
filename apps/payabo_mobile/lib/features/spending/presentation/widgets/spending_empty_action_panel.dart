import 'package:flutter/material.dart';

import '../../../../shared/theme/payabo_color_resolver.dart';
import '../../../../shared/theme/payabo_radii.dart';
import '../../../../shared/theme/payabo_spacing.dart';

/// Fixed bottom action panel for the spending empty state.
///
/// Mirrors [SetupActionCard]'s visual style — slides up from the
/// bottom with a 500ms easeOutCubic animation, uses [PayaboRadii.sheetTop]
/// border radius, and pins to the bottom of the screen.
///
/// Contains action tiles for linking/adding accounts, plus an optional
/// helper link (e.g. "Open profile settings" in fresh-demo mode).
class SpendingEmptyActionPanel extends StatefulWidget {
  const SpendingEmptyActionPanel({
    super.key,
    required this.children,
  });

  /// The content to display inside the panel (action tiles, links, etc.).
  final List<Widget> children;

  @override
  State<SpendingEmptyActionPanel> createState() =>
      _SpendingEmptyActionPanelState();
}

class _SpendingEmptyActionPanelState extends State<SpendingEmptyActionPanel>
    with SingleTickerProviderStateMixin {
  late final AnimationController _slideController;
  late final Animation<Offset> _slideAnimation;
  late final Animation<double> _fadeAnimation;

  @override
  void initState() {
    super.initState();
    _slideController = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 500),
    );
    _slideAnimation = Tween<Offset>(
      begin: const Offset(0, 0.3),
      end: Offset.zero,
    ).animate(CurvedAnimation(
      parent: _slideController,
      curve: Curves.easeOutCubic,
    ));
    _fadeAnimation = CurvedAnimation(
      parent: _slideController,
      curve: Curves.easeOut,
    );
    _slideController.forward();
  }

  @override
  void dispose() {
    _slideController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return SlideTransition(
      position: _slideAnimation,
      child: FadeTransition(
        opacity: _fadeAnimation,
        child: Container(
          width: double.infinity,
          decoration: BoxDecoration(
            color: c.surfaceBase,
            borderRadius: PayaboRadii.sheetTop,
            border: Border(
              top: BorderSide(color: c.borderWarm, width: 0.5),
            ),
            boxShadow: <BoxShadow>[
              BoxShadow(
                color: c.isDark
                    ? Colors.black26
                    : const Color(0x0D000000),
                blurRadius: 16,
                offset: const Offset(0, -4),
              ),
            ],
          ),
          child: SafeArea(
            top: false,
            child: Padding(
              padding: const EdgeInsets.fromLTRB(
                PayaboSpacing.xl,
                PayaboSpacing.xl,
                PayaboSpacing.xl,
                PayaboSpacing.lg,
              ),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: widget.children,
              ),
            ),
          ),
        ),
      ),
    );
  }
}

/// A tappable action tile used inside [SpendingEmptyActionPanel].
///
/// Displays an icon, title, subtitle, and a trailing chevron inside
/// a warm elevated card.
class SpendingActionTile extends StatelessWidget {
  const SpendingActionTile({
    super.key,
    required this.icon,
    required this.title,
    required this.subtitle,
    required this.onTap,
  });

  final IconData icon;
  final String title;
  final String subtitle;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        borderRadius: PayaboRadii.radiusLg,
        child: Ink(
          decoration: BoxDecoration(
            color: c.spendingCardWarmElevated,
            borderRadius: PayaboRadii.radiusLg,
            border: Border.all(
              color: c.borderStrong.withValues(alpha: 0.15),
            ),
          ),
          padding: const EdgeInsets.all(PayaboSpacing.lg),
          child: Row(
            children: <Widget>[
              Container(
                width: 48,
                height: 48,
                decoration: BoxDecoration(
                  color: c.primary.withValues(alpha: 0.10),
                  borderRadius: BorderRadius.circular(14),
                ),
                child: Icon(
                  icon,
                  color: c.primary,
                  size: 24,
                ),
              ),
              const SizedBox(width: PayaboSpacing.lg),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text(
                      title,
                      style: Theme.of(context).textTheme.titleSmall?.copyWith(
                            color: c.ink,
                            fontWeight: FontWeight.w700,
                          ),
                    ),
                    const SizedBox(height: PayaboSpacing.xxs),
                    Text(
                      subtitle,
                      style: Theme.of(context).textTheme.bodySmall?.copyWith(
                            color: c.muted,
                            height: 1.4,
                          ),
                    ),
                  ],
                ),
              ),
              const SizedBox(width: PayaboSpacing.sm),
              Icon(
                Icons.chevron_right_rounded,
                color: c.muted,
                size: 22,
              ),
            ],
          ),
        ),
      ),
    );
  }
}
