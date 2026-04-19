import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_radii.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_primary_app_shell.dart';

class PlanScreen extends StatelessWidget {
  const PlanScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Scaffold(
      backgroundColor: c.surfaceWarm,
      body: const _PlanHoldingState(),
      bottomNavigationBar: const PayaboPrimaryAppShell(
        destination: PayaboPrimaryDestination.plan,
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Full-screen holding state
// ─────────────────────────────────────────────────────────

class _PlanHoldingState extends StatelessWidget {
  const _PlanHoldingState();

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final TextTheme textTheme = Theme.of(context).textTheme;
    final bool isDark = c.isDark;

    final Color heroTextPrimary = isDark ? c.headerTitle : Colors.white;
    final Color heroTextSecondary =
        isDark ? c.textSubtleWarm : Colors.white70;
    final List<Shadow> heroTextShadow = isDark
        ? const <Shadow>[]
        : const <Shadow>[
            Shadow(color: Color(0x66000000), blurRadius: 6),
          ];

    return Stack(
      children: <Widget>[
        // ── Layer 1: Hero background (compass.png) ────────
        const Positioned.fill(child: _PlanHeroBackground()),

        // ── Layer 2: Top bar + intro message ─────────────
        Positioned.fill(
          child: SafeArea(
            bottom: false,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Padding(
                  padding: const EdgeInsets.fromLTRB(
                    PayaboSpacing.xl,
                    PayaboSpacing.md,
                    PayaboSpacing.xl,
                    0,
                  ),
                  child: Row(
                    children: <Widget>[
                      Container(
                        width: 36,
                        height: 36,
                        decoration: BoxDecoration(
                          color: isDark
                              ? c.surfaceBase.withValues(alpha: 0.8)
                              : const Color(0xCC1A1A1A),
                          borderRadius: BorderRadius.circular(10),
                          border: Border.all(
                            color: isDark
                                ? c.borderWarm.withValues(alpha: 0.5)
                                : Colors.white24,
                          ),
                        ),
                        child: Icon(
                          Icons.explore_outlined,
                          size: 18,
                          color: isDark ? c.primary : Colors.white,
                        ),
                      ),
                      const SizedBox(width: PayaboSpacing.sm),
                      Text(
                        'Compass',
                        style: textTheme.titleMedium?.copyWith(
                          color: heroTextPrimary,
                          fontWeight: FontWeight.w700,
                          shadows: heroTextShadow,
                        ),
                      ),
                    ],
                  ),
                ),
                Expanded(
                  child: Padding(
                    padding: const EdgeInsets.only(
                      top: PayaboSpacing.lg,
                      bottom: PayaboSpacing.sm,
                    ),
                    child: Align(
                      alignment: Alignment.topLeft,
                      child: SingleChildScrollView(
                        padding: const EdgeInsets.symmetric(
                          horizontal: PayaboSpacing.x2,
                        ),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: <Widget>[
                            Text(
                              'Your financial\nplan starts here.',
                              style: textTheme.headlineLarge?.copyWith(
                                color: heroTextPrimary,
                                fontWeight: FontWeight.w700,
                                height: 1.15,
                                shadows: heroTextShadow,
                              ),
                            ),
                            const SizedBox(height: PayaboSpacing.md),
                            Text(
                              'Compass is on its way. It will help you '
                              'understand where you stand, set clear goals, '
                              'and move forward with a plan built around your life.',
                              style: textTheme.bodyLarge?.copyWith(
                                fontSize: 17,
                                color: heroTextSecondary,
                                height: 1.5,
                                shadows: heroTextShadow,
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
                  ),
                ),
              ],
            ),
          ),
        ),

        // ── Layer 3: Pinned bottom action panel ───────────
        Positioned(
          left: 0,
          right: 0,
          bottom: 0,
          child: _PlanActionPanel(c: c, textTheme: textTheme),
        ),
      ],
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Hero background — compass.png with optional dark scrim
// ─────────────────────────────────────────────────────────

class _PlanHeroBackground extends StatelessWidget {
  const _PlanHeroBackground();

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final bool isDark = c.isDark;

    return Stack(
      fit: StackFit.expand,
      children: <Widget>[
        Image.asset(
          'assets/images/compass.png',
          fit: BoxFit.cover,
          width: double.infinity,
          height: double.infinity,
          errorBuilder: (_, __, ___) => _HeroFallback(isDark: isDark),
        ),
        if (isDark)
          const DecoratedBox(
            decoration: BoxDecoration(
              gradient: LinearGradient(
                colors: <Color>[
                  Color(0xCC1A1A1A),
                  Color(0x991A1A1A),
                  Color(0x661A1A1A),
                ],
                begin: Alignment.topCenter,
                end: Alignment.bottomCenter,
                stops: <double>[0.0, 0.45, 1.0],
              ),
            ),
            child: SizedBox.expand(),
          ),
      ],
    );
  }
}

class _HeroFallback extends StatelessWidget {
  const _HeroFallback({required this.isDark});

  final bool isDark;

  @override
  Widget build(BuildContext context) {
    return DecoratedBox(
      decoration: BoxDecoration(
        gradient: LinearGradient(
          colors: isDark
              ? const <Color>[Color(0xFF1A1A1A), Color(0xFF121212)]
              : const <Color>[
                  Color(0xFFFFF5EC),
                  Color(0xFFFFEEDD),
                  Color(0xFFF7EBD9),
                ],
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
          stops: isDark ? null : const <double>[0.0, 0.5, 1.0],
        ),
      ),
      child: const SizedBox.expand(),
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Slide-up action panel (mirrors SpendingEmptyActionPanel)
// ─────────────────────────────────────────────────────────

class _PlanActionPanel extends StatefulWidget {
  const _PlanActionPanel({
    required this.c,
    required this.textTheme,
  });

  final PayaboColorResolver c;
  final TextTheme textTheme;

  @override
  State<_PlanActionPanel> createState() => _PlanActionPanelState();
}

class _PlanActionPanelState extends State<_PlanActionPanel>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller;
  late final Animation<Offset> _slide;
  late final Animation<double> _fade;

  @override
  void initState() {
    super.initState();
    _controller = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 500),
    );
    _slide = Tween<Offset>(
      begin: const Offset(0, 0.3),
      end: Offset.zero,
    ).animate(CurvedAnimation(parent: _controller, curve: Curves.easeOutCubic));
    _fade = CurvedAnimation(parent: _controller, curve: Curves.easeOut);
    _controller.forward();
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final c = widget.c;
    final TextTheme textTheme = widget.textTheme;

    return SlideTransition(
      position: _slide,
      child: FadeTransition(
        opacity: _fade,
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
                color: c.isDark ? Colors.black26 : const Color(0x0D000000),
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
                children: <Widget>[
                  Text(
                    'Coming soon',
                    style: textTheme.titleLarge?.copyWith(
                      color: c.accentBrown,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  const SizedBox(height: PayaboSpacing.sm),
                  Text(
                    'Compass will help you build a clear, personalised '
                    'financial plan. In the meantime, Simi is ready to help.',
                    style: textTheme.bodyMedium?.copyWith(
                      color: c.muted,
                      height: 1.4,
                    ),
                  ),
                  const SizedBox(height: PayaboSpacing.x2),
                  _PlanActionTile(
                    icon: Icons.chat_bubble_outline_rounded,
                    title: 'Talk to Simi',
                    subtitle:
                        'Ask questions, explore your finances and get guidance now.',
                    onTap: () => context.go('/chat'),
                    c: c,
                  ),
                  const SizedBox(height: PayaboSpacing.md),
                  _PlanActionTile(
                    icon: Icons.flag_outlined,
                    title: 'Set a goal',
                    subtitle:
                        'Goals, progress tracking and adaptive plans — coming with Compass.',
                    onTap: () {
                      ScaffoldMessenger.of(context)
                        ..hideCurrentSnackBar()
                        ..showSnackBar(
                          const SnackBar(
                            content: Text('Goal setting is coming with Compass.'),
                          ),
                        );
                    },
                    c: c,
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Action tile (mirrors SpendingActionTile)
// ─────────────────────────────────────────────────────────

class _PlanActionTile extends StatelessWidget {
  const _PlanActionTile({
    required this.icon,
    required this.title,
    required this.subtitle,
    required this.onTap,
    required this.c,
  });

  final IconData icon;
  final String title;
  final String subtitle;
  final VoidCallback onTap;
  final PayaboColorResolver c;

  @override
  Widget build(BuildContext context) {
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
                child: Icon(icon, color: c.primary, size: 24),
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
              Icon(Icons.chevron_right_rounded, color: c.muted, size: 22),
            ],
          ),
        ),
      ),
    );
  }
}
