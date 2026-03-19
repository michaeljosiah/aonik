import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/theme/payabo_theme.dart';
import '../application/setup_processing_controller.dart';
import 'widgets/setup_hero_background.dart';
import 'widgets/setup_processing_step.dart';

/// Post-setup AI processing screen.
///
/// Displays a calm, premium full-screen sequence where Simi analyses
/// the user's onboarding data and prepares their personalised financial
/// assistant. Each step fades in with a subtle slide animation.
///
/// After the final step, the screen navigates to `/dashboard`.
///
/// ## Route
/// `/setup/processing`
///
/// ## Trigger
/// Immediately after the user completes the setup journey.
class SetupProcessingScreen extends ConsumerStatefulWidget {
  const SetupProcessingScreen({super.key});

  @override
  ConsumerState<SetupProcessingScreen> createState() =>
      _SetupProcessingScreenState();
}

class _SetupProcessingScreenState extends ConsumerState<SetupProcessingScreen> {
  bool _hasNavigated = false;

  @override
  void initState() {
    super.initState();
    // Kick off the processing sequence after the first frame.
    WidgetsBinding.instance.addPostFrameCallback((_) {
      ref.read(setupProcessingControllerProvider.notifier).startProcessing();
    });
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(setupProcessingControllerProvider);

    // Navigate to dashboard when processing completes.
    ref.listen<SetupProcessingState>(
      setupProcessingControllerProvider,
      (SetupProcessingState? previous, SetupProcessingState next) {
        if (next.isComplete && !_hasNavigated) {
          _hasNavigated = true;
          context.go('/dashboard');
        }
      },
    );

    // Force dark theme on the entire processing screen subtree so
    // that context.colors always resolves to dark-mode tokens,
    // matching the setup journey's cinematic look.
    return Theme(
      data: buildPayaboDarkTheme(),
      child: Builder(
        builder: (BuildContext context) {
    final c = context.colors;
    final textTheme = Theme.of(context).textTheme;

    return Scaffold(
      body: Stack(
        children: <Widget>[
          // Reuse the setup journey background for visual continuity.
          const Positioned.fill(
            child: SetupHeroBackground(),
          ),

          // Content overlay.
          Positioned.fill(
            child: SafeArea(
              child: Column(
                children: <Widget>[
                  const SizedBox(height: PayaboSpacing.x4),

                  // Top brand mark — consistent with setup journey.
                  _buildTopBar(context, c),

                  // Spacer pushes content to vertical center.
                  const Spacer(flex: 3),

                  // Simi avatar pulse.
                  const SimiPulseAvatar(),
                  const SizedBox(height: PayaboSpacing.x3),

                  // Animated message text.
                  Padding(
                    padding: const EdgeInsets.symmetric(
                      horizontal: PayaboSpacing.x3,
                    ),
                    child: SizedBox(
                      height: 80,
                      child: SetupProcessingStepWidget(
                        message: state.currentStep.message,
                        stepKey: state.currentStep.id,
                      ),
                    ),
                  ),
                  const SizedBox(height: PayaboSpacing.x3),

                  // Step counter.
                  Text(
                    'Step ${state.currentStepIndex + 1} of ${state.totalSteps}',
                    style: textTheme.bodySmall?.copyWith(
                      color: c.textMuted,
                    ),
                  ),

                  const Spacer(flex: 4),

                  // Progress bar at the bottom.
                  SetupProcessingProgressBar(progress: state.progress),
                  const SizedBox(height: PayaboSpacing.x4),
                ],
              ),
            ),
          ),
        ],
      ),
    );
        },
      ),
    );
  }

  Widget _buildTopBar(BuildContext context, PayaboColorResolver c) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: PayaboSpacing.xl),
      child: Row(
        children: <Widget>[
          Container(
            width: 36,
            height: 36,
            decoration: BoxDecoration(
              color: c.surfaceBase.withValues(alpha: 0.8),
              borderRadius: BorderRadius.circular(10),
              border: Border.all(
                color: c.borderWarm.withValues(alpha: 0.5),
              ),
            ),
            child: Icon(
              Icons.auto_awesome_rounded,
              size: 18,
              color: c.primary,
            ),
          ),
          const SizedBox(width: PayaboSpacing.sm),
          Text(
            'Payabo',
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
                  color: c.headerTitle,
                  fontWeight: FontWeight.w700,
                ),
          ),
        ],
      ),
    );
  }
}
