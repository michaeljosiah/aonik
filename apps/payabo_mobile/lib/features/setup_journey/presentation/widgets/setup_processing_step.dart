import 'package:flutter/material.dart';

import '../../../../shared/theme/payabo_color_resolver.dart';
import '../../../../shared/theme/payabo_spacing.dart';

/// Animated display for a single processing step message.
///
/// Uses [AnimatedSwitcher] to cross-fade between messages and a subtle
/// slide-up transition for each new step. The Simi avatar pulses gently
/// to convey that the system is actively working.
class SetupProcessingStepWidget extends StatelessWidget {
  const SetupProcessingStepWidget({
    super.key,
    required this.message,
    required this.stepKey,
  });

  final String message;
  final String stepKey;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final textTheme = Theme.of(context).textTheme;

    return AnimatedSwitcher(
      duration: const Duration(milliseconds: 500),
      switchInCurve: Curves.easeOut,
      switchOutCurve: Curves.easeIn,
      transitionBuilder: (Widget child, Animation<double> animation) {
        final slide = Tween<Offset>(
          begin: const Offset(0, 0.15),
          end: Offset.zero,
        ).animate(CurvedAnimation(parent: animation, curve: Curves.easeOut));

        return SlideTransition(
          position: slide,
          child: FadeTransition(
            opacity: animation,
            child: child,
          ),
        );
      },
      child: Text(
        message,
        key: ValueKey<String>(stepKey),
        textAlign: TextAlign.center,
        style: textTheme.headlineSmall?.copyWith(
          color: c.headerTitle,
          fontWeight: FontWeight.w500,
          height: 1.4,
        ),
      ),
    );
  }
}

/// A gently pulsing avatar representing Simi during the processing
/// sequence. Uses a simple scale animation to convey activity without
/// being distracting.
class SimiPulseAvatar extends StatefulWidget {
  const SimiPulseAvatar({super.key});

  @override
  State<SimiPulseAvatar> createState() => _SimiPulseAvatarState();
}

class _SimiPulseAvatarState extends State<SimiPulseAvatar>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller;
  late final Animation<double> _scale;

  @override
  void initState() {
    super.initState();
    _controller = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 1600),
    );
    _scale = Tween<double>(begin: 1.0, end: 1.08).animate(
      CurvedAnimation(parent: _controller, curve: Curves.easeInOut),
    );
    _controller.repeat(reverse: true);
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return ScaleTransition(
      scale: _scale,
      child: Container(
        width: 72,
        height: 72,
        decoration: BoxDecoration(
          shape: BoxShape.circle,
          color: c.surfaceWarmElevated,
          border: Border.all(
            color: c.primary.withValues(alpha: 0.3),
            width: 2,
          ),
          boxShadow: <BoxShadow>[
            BoxShadow(
              color: c.primary.withValues(alpha: 0.12),
              blurRadius: 24,
              spreadRadius: 4,
            ),
          ],
        ),
        alignment: Alignment.center,
        child: Icon(
          Icons.auto_awesome_rounded,
          size: 32,
          color: c.primary,
        ),
      ),
    );
  }
}

/// A thin, warm progress bar that advances as Simi works through
/// the processing steps.
class SetupProcessingProgressBar extends StatelessWidget {
  const SetupProcessingProgressBar({
    super.key,
    required this.progress,
  });

  /// 0.0 to 1.0
  final double progress;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: PayaboSpacing.x4),
      child: ClipRRect(
        borderRadius: BorderRadius.circular(2),
        child: TweenAnimationBuilder<double>(
          tween: Tween<double>(begin: 0, end: progress),
          duration: const Duration(milliseconds: 600),
          curve: Curves.easeOut,
          builder: (BuildContext context, double value, Widget? _) {
            return LinearProgressIndicator(
              value: value,
              minHeight: 3,
              backgroundColor: c.borderDefault.withValues(alpha: 0.2),
              valueColor: AlwaysStoppedAnimation<Color>(c.primary),
            );
          },
        ),
      ),
    );
  }
}
