// ignore_for_file: public_member_api_docs

import 'dart:async';
import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../shared/theme/payabo_spacing.dart';
import 'realtime_voice_controller.dart';

/// Slim realtime voice stage for the Voxa WSS pipeline.
///
/// The visual centrepiece is a Siri-style orb — concentric rings that expand
/// outward and fade, anchored by a soft breathing core. Ring intensity is
/// gated on [RealtimeVoiceState.whoIsSpeaking] so the pulse "comes alive"
/// when the bot is talking and settles to a gentle breath when it's the
/// user's turn or no one is speaking.
///
/// Architecture:
///  * **One forward-only `AnimationController`** drives a single phase value
///    (0→1 over 1.8 s, then loops). Multiple rings sample that phase with
///    offsets so we get a continuous "ping" rhythm from a single ticker.
///  * **No** thinking sound, end-of-turn timer, retry budget, "ready" phase
///    or periodic 160 ms `setState` tick — server VAD owns turn detection,
///    the WS connection state owns the error surface, barge-in is implicit.
///
/// The widget is purely a renderer — it reads
/// [realtimeVoiceControllerProvider] and forwards taps to [onOrbTap]. The
/// chat screen owns the start/stop policy.
class RealtimeVoiceStage extends ConsumerStatefulWidget {
  const RealtimeVoiceStage({
    super.key,
    required this.onOrbTap,
  });

  /// Invoked when the user taps the orb. The chat screen interprets the
  /// tap based on the current phase (idle/error → start, connecting/live
  /// → stop) and wraps the call in a busy-watchdog so re-entrant taps
  /// can't diverge the state machine.
  final Future<void> Function() onOrbTap;

  @override
  ConsumerState<RealtimeVoiceStage> createState() =>
      _RealtimeVoiceStageState();
}

class _RealtimeVoiceStageState extends ConsumerState<RealtimeVoiceStage>
    with SingleTickerProviderStateMixin {
  late final AnimationController _pulse;

  @override
  void initState() {
    super.initState();
    // Forward-only saw-tooth (0 → 1, jump back, repeat) over 1.8 s. With four
    // rings offset by 25 % each, a new ring starts roughly every 450 ms — fast
    // enough to feel responsive when the bot is mid-sentence, slow enough that
    // a single ring takes long enough to expand all the way out.
    _pulse = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 1800),
    )..repeat();
  }

  @override
  void dispose() {
    _pulse.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final RealtimeVoiceState state =
        ref.watch(realtimeVoiceControllerProvider);
    final ColorScheme scheme = Theme.of(context).colorScheme;
    final bool isError = state.phase == RealtimeVoicePhase.error;

    return LayoutBuilder(
      builder: (BuildContext context, BoxConstraints constraints) {
        return SingleChildScrollView(
          padding: const EdgeInsets.fromLTRB(
            PayaboSpacing.xl,
            PayaboSpacing.xl,
            PayaboSpacing.xl,
            PayaboSpacing.xl,
          ),
          child: ConstrainedBox(
            constraints: BoxConstraints(minHeight: constraints.maxHeight),
            child: Center(
              child: ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 420),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: <Widget>[
                    Text(
                      _eyebrowLabel(state),
                      style: Theme.of(context).textTheme.labelLarge?.copyWith(
                            color: isError ? scheme.error : scheme.primary,
                            fontWeight: FontWeight.w800,
                            letterSpacing: 3.2,
                          ),
                    ),
                    const SizedBox(height: PayaboSpacing.xl),
                    Semantics(
                      button: true,
                      label: 'Voice orb',
                      child: GestureDetector(
                        onTap: () => unawaited(widget.onOrbTap()),
                        behavior: HitTestBehavior.opaque,
                        child: _RealtimeOrb(state: state, pulse: _pulse),
                      ),
                    ),
                    const SizedBox(height: PayaboSpacing.lg),
                    _BodyPanel(state: state),
                  ],
                ),
              ),
            ),
          ),
        );
      },
    );
  }

  String _eyebrowLabel(RealtimeVoiceState state) {
    return switch (state.phase) {
      RealtimeVoicePhase.idle => 'SIMI',
      RealtimeVoicePhase.connecting => 'OPENING THE LINE',
      RealtimeVoicePhase.error => 'TAP TO RETRY',
      RealtimeVoicePhase.live => switch (state.whoIsSpeaking) {
          RealtimeSpeaker.bot => 'SIMI RESPONDING',
          RealtimeSpeaker.user => 'SIMI LISTENING',
          RealtimeSpeaker.none => 'SIMI LIVE',
        },
    };
  }
}

class _BodyPanel extends StatelessWidget {
  const _BodyPanel({required this.state});

  final RealtimeVoiceState state;

  @override
  Widget build(BuildContext context) {
    if (state.phase == RealtimeVoicePhase.error) {
      return _ErrorPanel(message: state.errorMessage);
    }

    final String text;
    final bool italic;
    if (state.livePartialTranscript.isNotEmpty &&
        state.phase == RealtimeVoicePhase.live) {
      // Surface live partials in italic so the user sees the mic is working
      // between server-side finals.
      text = state.livePartialTranscript;
      italic = true;
    } else {
      italic = false;
      text = switch (state.phase) {
        RealtimeVoicePhase.idle =>
          'Tap the orb to start a voice conversation.',
        RealtimeVoicePhase.connecting => 'Opening the line…',
        RealtimeVoicePhase.live => switch (state.whoIsSpeaking) {
            RealtimeSpeaker.bot => '',
            RealtimeSpeaker.user => 'Listening…',
            RealtimeSpeaker.none => 'Speak whenever you’re ready.',
          },
        RealtimeVoicePhase.error => '',
      };
    }

    if (text.isEmpty) return const SizedBox.shrink();

    return ConstrainedBox(
      constraints: const BoxConstraints(maxWidth: 360),
      child: Text(
        text,
        textAlign: TextAlign.center,
        style: Theme.of(context).textTheme.bodyLarge?.copyWith(
              color: Colors.white.withValues(alpha: 0.64),
              height: 1.55,
              fontStyle: italic ? FontStyle.italic : FontStyle.normal,
            ),
      ),
    );
  }
}

class _ErrorPanel extends StatelessWidget {
  const _ErrorPanel({required this.message});

  final String? message;

  @override
  Widget build(BuildContext context) {
    final ColorScheme scheme = Theme.of(context).colorScheme;
    return ConstrainedBox(
      constraints: const BoxConstraints(maxWidth: 360),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Text(
            message ?? 'Voice connection failed.',
            textAlign: TextAlign.center,
            style: Theme.of(context).textTheme.bodyLarge?.copyWith(
                  color: scheme.error,
                  height: 1.55,
                ),
          ),
          const SizedBox(height: PayaboSpacing.sm),
          Text(
            'Tap the orb to try again.',
            textAlign: TextAlign.center,
            style: Theme.of(context).textTheme.bodySmall?.copyWith(
                  color: Colors.white.withValues(alpha: 0.6),
                ),
          ),
        ],
      ),
    );
  }
}

/// Siri-style animated orb. Four concentric rings expand outward from the
/// core, fading as they grow, with phase offsets so a new ring is always
/// "leaving" the centre while previous rings dissipate at the edge. A soft
/// inner core breathes underneath the rings so the orb feels alive even
/// during silence.
///
/// Intensity (ring count opacity + breathing amplitude) is gated on
/// `state.whoIsSpeaking`:
///
///   bot  → strongest, lively pulse (the AI is talking — show it)
///   user → moderate, the mic is hot
///   none → gentle breath
///   error → static red dot, no rings
///
/// Transitions between intensities animate smoothly via [TweenAnimationBuilder]
/// so flipping between speakers doesn't look snappy.
class _RealtimeOrb extends StatelessWidget {
  const _RealtimeOrb({required this.state, required this.pulse});

  final RealtimeVoiceState state;
  final AnimationController pulse;

  /// Number of concentric expanding rings.
  static const int _ringCount = 4;

  /// Inner core size at rest. Rings expand from this radius outward.
  static const double _coreSize = 120;

  /// Total bounding box (also the maximum extent the outermost ring reaches).
  static const double _orbSize = 320;

  @override
  Widget build(BuildContext context) {
    final ColorScheme scheme = Theme.of(context).colorScheme;
    final Color primary = state.phase == RealtimeVoicePhase.error
        ? scheme.error
        : scheme.primary;
    final double targetIntensity = _targetIntensity(state);

    return SizedBox(
      width: _orbSize,
      height: _orbSize,
      child: TweenAnimationBuilder<double>(
        // Cross-fade intensity over ~500 ms so transitions between user/bot
        // speech don't look like a switch flip.
        tween: Tween<double>(begin: targetIntensity, end: targetIntensity),
        duration: const Duration(milliseconds: 500),
        curve: Curves.easeOutCubic,
        builder: (BuildContext context, double intensity, Widget? _) {
          return AnimatedBuilder(
            animation: pulse,
            builder: (BuildContext context, Widget? __) {
              return Stack(
                alignment: Alignment.center,
                children: <Widget>[
                  // Ring waves — drawn back-to-front so the freshest ring
                  // (closest to the core) sits on top.
                  for (int i = _ringCount - 1; i >= 0; i--)
                    _Ring(
                      phase: (pulse.value + (i / _ringCount)) % 1.0,
                      color: primary,
                      intensity: intensity,
                      coreSize: _coreSize,
                      maxSize: _orbSize,
                    ),
                  // Inner breathing core.
                  _Core(
                    pulseValue: pulse.value,
                    intensity: intensity,
                    color: primary,
                    showSpinner:
                        state.phase == RealtimeVoicePhase.connecting,
                  ),
                ],
              );
            },
          );
        },
      ),
    );
  }

  /// Per-phase intensity multiplier. The ring opacity AND breathing amplitude
  /// scale by this — bot speech is the "loud" state, error is flat.
  static double _targetIntensity(RealtimeVoiceState state) {
    switch (state.phase) {
      case RealtimeVoicePhase.error:
        return 0;
      case RealtimeVoicePhase.idle:
        return 0.25;
      case RealtimeVoicePhase.connecting:
        return 0.45;
      case RealtimeVoicePhase.live:
        switch (state.whoIsSpeaking) {
          case RealtimeSpeaker.bot:
            return 1.0;
          case RealtimeSpeaker.user:
            return 0.7;
          case RealtimeSpeaker.none:
            return 0.35;
        }
    }
  }
}

/// One expanding/fading ring. [phase] is the position in the 0→1 cycle —
/// 0 = just emitted at core radius, 1 = fully expanded and faded out.
class _Ring extends StatelessWidget {
  const _Ring({
    required this.phase,
    required this.color,
    required this.intensity,
    required this.coreSize,
    required this.maxSize,
  });

  final double phase;
  final Color color;
  final double intensity;
  final double coreSize;
  final double maxSize;

  @override
  Widget build(BuildContext context) {
    // Use easeOut so rings sprint out from the core then decelerate as they
    // fade — mirrors how a real ripple loses energy as it travels.
    final double eased = Curves.easeOutCubic.transform(phase);
    final double size = coreSize + eased * (maxSize - coreSize) * intensity;
    // Opacity envelope: peaks just after the ring emerges (so it's visible)
    // and tapers to zero as it reaches the edge. Sin(pi * phase) gives a
    // nice hump shape.
    final double envelope = math.sin(phase * math.pi);
    final double opacity = envelope * 0.55 * intensity;
    // Stroke fattens slightly as the ring grows so the visual mass stays
    // roughly constant rather than the ring thinning into invisibility.
    final double strokeWidth = 1.5 + (1.5 * eased);

    return IgnorePointer(
      child: Container(
        width: size,
        height: size,
        decoration: BoxDecoration(
          shape: BoxShape.circle,
          border: Border.all(
            color: color.withValues(alpha: opacity.clamp(0.0, 1.0)),
            width: strokeWidth,
          ),
        ),
      ),
    );
  }
}

/// The orb's inner core. Solid circle with a radial gradient that gently
/// breathes (sin-shaped scale modulation) so the orb feels alive even when
/// no rings are emitting (idle / error states).
class _Core extends StatelessWidget {
  const _Core({
    required this.pulseValue,
    required this.intensity,
    required this.color,
    required this.showSpinner,
  });

  final double pulseValue;
  final double intensity;
  final Color color;
  final bool showSpinner;

  @override
  Widget build(BuildContext context) {
    // sin(2π · pulse) gives a continuous breathing rhythm at the same period
    // as the ring cycle — feels like the rings are pumping out of the core.
    final double breath = (math.sin(pulseValue * 2 * math.pi) + 1) / 2;
    final double coreSize = 108 + (breath * 12 * intensity);
    final double innerGlow = 0.7 + (breath * 0.2 * intensity);

    return Container(
      width: coreSize,
      height: coreSize,
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        gradient: RadialGradient(
          colors: <Color>[
            color,
            color.withValues(alpha: innerGlow),
          ],
        ),
        boxShadow: <BoxShadow>[
          // Soft glow that intensifies with the breath — sells the "alive"
          // feeling more than the gradient alone does.
          BoxShadow(
            color: color.withValues(alpha: 0.35 * intensity),
            blurRadius: 20 + (breath * 10 * intensity),
            spreadRadius: 2,
          ),
        ],
      ),
      child: showSpinner
          ? const Center(
              child: SizedBox(
                width: 28,
                height: 28,
                child: CircularProgressIndicator(
                  strokeWidth: 2.5,
                  valueColor:
                      AlwaysStoppedAnimation<Color>(Colors.white),
                ),
              ),
            )
          : const SizedBox.shrink(),
    );
  }
}
