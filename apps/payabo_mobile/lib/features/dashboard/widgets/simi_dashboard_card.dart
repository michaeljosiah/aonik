import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_radii.dart';
import '../../../shared/theme/payabo_shadows.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../setup_journey/domain/setup_models.dart';
import '../application/dashboard_providers.dart';

/// A dismissible Simi greeting card shown at the top of the dashboard
/// stats sheet after setup completion.
///
/// Content varies by [DashboardSetupSeed.greetingVariant]:
/// - `diaspora_family` — emphasises family support and remittance
/// - `goal_focused` — emphasises savings goals
/// - `bills_focused` — emphasises bill management
/// - `default` — generic welcome
///
/// Also surfaces the first nudge from the seed when available.
class SimiDashboardCard extends ConsumerWidget {
  const SimiDashboardCard({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final visible = ref.watch(simiCardVisibleProvider);
    if (!visible) return const SizedBox.shrink();

    final seed = ref.watch(dashboardSeedProvider);
    final c = context.colors;

    return Padding(
      padding: const EdgeInsets.only(bottom: PayaboSpacing.lg),
      child: Container(
        width: double.infinity,
        decoration: BoxDecoration(
          gradient: LinearGradient(
            colors: <Color>[
              c.primary.withValues(alpha: 0.08),
              c.surfaceWarm,
            ],
            begin: Alignment.topLeft,
            end: Alignment.bottomRight,
          ),
          borderRadius: PayaboRadii.radiusSm,
          border: Border.all(
            color: c.primary.withValues(alpha: 0.18),
          ),
          boxShadow: PayaboShadows.soft,
        ),
        child: Padding(
          padding: const EdgeInsets.all(PayaboSpacing.lg),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              // Header row — Simi avatar + title + dismiss
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  _SimiAvatar(color: c.primary),
                  const SizedBox(width: PayaboSpacing.md),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: <Widget>[
                        Text(
                          'Simi',
                          style: Theme.of(context)
                              .textTheme
                              .titleSmall
                              ?.copyWith(
                                color: c.primary,
                                fontWeight: FontWeight.w700,
                              ),
                        ),
                        const SizedBox(height: 2),
                        Text(
                          _subtitleForVariant(seed.greetingVariant),
                          style: Theme.of(context)
                              .textTheme
                              .bodySmall
                              ?.copyWith(
                                color: c.textSecondary,
                              ),
                        ),
                      ],
                    ),
                  ),
                  GestureDetector(
                    onTap: () => ref
                        .read(simiCardDismissedProvider.notifier)
                        .state = true,
                    child: Icon(
                      Icons.close_rounded,
                      size: 20,
                      color: c.textSecondary.withValues(alpha: 0.6),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: PayaboSpacing.md),

              // Greeting message
              Text(
                _greetingForVariant(seed.greetingVariant),
                style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                      color: c.textPrimary,
                      height: 1.45,
                    ),
              ),

              // First nudge (if available)
              if (seed.nudges.isNotEmpty) ...[
                const SizedBox(height: PayaboSpacing.sm),
                Container(
                  width: double.infinity,
                  padding: const EdgeInsets.all(PayaboSpacing.md),
                  decoration: BoxDecoration(
                    color: c.primary.withValues(alpha: 0.06),
                    borderRadius: PayaboRadii.radiusSm,
                  ),
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Icon(
                        Icons.lightbulb_outline_rounded,
                        size: 16,
                        color: c.primary,
                      ),
                      const SizedBox(width: PayaboSpacing.sm),
                      Expanded(
                        child: Text(
                          seed.nudges.first,
                          style:
                              Theme.of(context).textTheme.bodySmall?.copyWith(
                                    color: c.textSecondary,
                                    height: 1.4,
                                  ),
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }

  static String _subtitleForVariant(String variant) {
    switch (variant) {
      case 'diaspora_family':
        return 'Your family finance assistant';
      case 'goal_focused':
        return 'Your savings coach';
      case 'bills_focused':
        return 'Your bill manager';
      default:
        return 'Your financial assistant';
    }
  }

  static String _greetingForVariant(String variant) {
    switch (variant) {
      case 'diaspora_family':
        return 'I\'ve set things up with your family commitments in mind. '
            'I\'ll help you stay on top of support payments and make sure '
            'nothing slips through the cracks.';
      case 'goal_focused':
        return 'Great goals! I\'ve organised your dashboard around your '
            'savings targets. Let\'s build momentum together.';
      case 'bills_focused':
        return 'I\'ve got your bills front and centre. I\'ll track due dates '
            'and help you avoid surprises.';
      default:
        return 'Welcome! I\'ve personalised your dashboard based on what you '
            'told me. Tap into any section to explore.';
    }
  }
}

class _SimiAvatar extends StatelessWidget {
  const _SimiAvatar({required this.color});

  final Color color;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 36,
      height: 36,
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        shape: BoxShape.circle,
      ),
      child: Icon(
        Icons.auto_awesome_rounded,
        size: 18,
        color: color,
      ),
    );
  }
}
