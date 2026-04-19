import 'package:flutter/material.dart';

import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_primary_app_shell.dart';

class PlanScreen extends StatelessWidget {
  const PlanScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Scaffold(
      backgroundColor: c.surfaceBase,
      body: Column(
        children: <Widget>[
          Expanded(
            child: SafeArea(
              bottom: false,
              child: SingleChildScrollView(
                padding: const EdgeInsets.symmetric(
                  horizontal: PayaboSpacing.xl,
                  vertical: PayaboSpacing.lg,
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    const SizedBox(height: PayaboSpacing.md),
                    Text(
                      'Plan',
                      style: Theme.of(context).textTheme.headlineMedium?.copyWith(
                            color: c.textPrimary,
                            fontWeight: FontWeight.w700,
                          ),
                    ),
                    const SizedBox(height: PayaboSpacing.xs),
                    Text(
                      'Your financial guidance layer',
                      style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                            color: c.muted,
                          ),
                    ),
                    const SizedBox(height: PayaboSpacing.x3),
                    _CompassComingCard(c: c),
                    const SizedBox(height: PayaboSpacing.xl),
                    _PillarRow(
                      icon: Icons.my_location_outlined,
                      title: 'Where am I now?',
                      body:
                          'A clear picture of your financial position — balances, obligations, and pressure points.',
                      c: c,
                    ),
                    const SizedBox(height: PayaboSpacing.md),
                    _PillarRow(
                      icon: Icons.flag_outlined,
                      title: 'Where do I want to go?',
                      body:
                          'Set goals in plain language. Compass turns them into a realistic path.',
                      c: c,
                    ),
                    const SizedBox(height: PayaboSpacing.md),
                    _PillarRow(
                      icon: Icons.route_outlined,
                      title: 'What should I do next?',
                      body:
                          'Day-by-day guidance that adapts when life changes — not a static budget.',
                      c: c,
                    ),
                  ],
                ),
              ),
            ),
          ),
          const PayaboPrimaryAppShell(destination: PayaboPrimaryDestination.plan),
        ],
      ),
    );
  }
}

class _CompassComingCard extends StatelessWidget {
  const _CompassComingCard({required this.c});

  final PayaboColorResolver c;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(PayaboSpacing.xl),
      decoration: BoxDecoration(
        gradient: LinearGradient(
          colors: <Color>[
            c.primary.withValues(alpha: 0.12),
            c.primary.withValues(alpha: 0.04),
          ],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        borderRadius: BorderRadius.circular(20),
        border: Border.all(
          color: c.primary.withValues(alpha: 0.2),
        ),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Container(
            width: 56,
            height: 56,
            decoration: BoxDecoration(
              color: c.primary.withValues(alpha: 0.15),
              borderRadius: BorderRadius.circular(16),
            ),
            child: Icon(
              Icons.explore_outlined,
              color: c.primary,
              size: 28,
            ),
          ),
          const SizedBox(height: PayaboSpacing.lg),
          Text(
            'Compass is coming',
            style: Theme.of(context).textTheme.titleLarge?.copyWith(
                  color: c.textPrimary,
                  fontWeight: FontWeight.w700,
                ),
          ),
          const SizedBox(height: PayaboSpacing.xs),
          Text(
            'Compass is the intelligence layer that turns your financial activity into direction. '
            'It will help you understand where you stand, decide where you want to go, '
            'and move forward with clear, adaptive support.',
            style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                  color: c.muted,
                  height: 1.5,
                ),
          ),
        ],
      ),
    );
  }
}

class _PillarRow extends StatelessWidget {
  const _PillarRow({
    required this.icon,
    required this.title,
    required this.body,
    required this.c,
  });

  final IconData icon;
  final String title;
  final String body;
  final PayaboColorResolver c;

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Container(
          width: 40,
          height: 40,
          decoration: BoxDecoration(
            color: c.surfaceCard,
            borderRadius: BorderRadius.circular(12),
            border: Border.all(color: c.borderDefault),
          ),
          child: Icon(icon, color: c.textPrimary, size: 20),
        ),
        const SizedBox(width: PayaboSpacing.md),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Text(
                title,
                style: Theme.of(context).textTheme.titleSmall?.copyWith(
                      color: c.textPrimary,
                      fontWeight: FontWeight.w700,
                    ),
              ),
              const SizedBox(height: PayaboSpacing.xxs),
              Text(
                body,
                style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color: c.muted,
                      height: 1.45,
                    ),
              ),
            ],
          ),
        ),
      ],
    );
  }
}
