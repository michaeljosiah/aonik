// ignore_for_file: public_member_api_docs

import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../shared/theme/payabo_spacing.dart';
import 'realtime_voice_controller.dart';

/// Slim 4-phase voice stage for the Voxa WSS realtime pipeline.
///
/// Replaces the legacy `_RealtimeVoiceStage` widget (5 phases, periodic
/// 160 ms tick, ambient "thinking" loop) with:
///
///  * **One** `AnimationController` driving the orb pulse — amplitude is
///    derived from [RealtimeVoiceState.whoIsSpeaking] so it visibly tracks
///    "user vs bot vs neither" without a per-frame `setState`.
///  * **No** thinking sound, end-of-turn timer, retry budget, or "ready"
///    phase. Server VAD owns turn detection; the WS connection state owns
///    error surface; barge-in is implicit (just keep talking).
///
/// The widget is purely a renderer — it reads
/// [realtimeVoiceControllerProvider] and forwards taps to [onOrbTap]. The
/// chat screen owns the start/stop policy so a single voice toggle works
/// for both pipelines while the [voxaVoiceModeEnabledProvider] flag is in
/// place.
class RealtimeVoiceStage extends ConsumerStatefulWidget {
  const RealtimeVoiceStage({
    super.key,
    required this.onOrbTap,
  });

  /// Invoked when the user taps the orb. The chat screen interprets the
  /// tap based on the current phase (idle/error → start, connecting/live →
  /// stop) so the legacy `_handleVoiceTap` busy-watchdog wrapping can keep
  /// guarding against re-entrant taps.
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
    // Triangle wave 0 → 1 → 0 every 1.4 s. Slow enough to feel alive
    // rather than urgent; the amplitude envelope (set by phase / speaker)
    // does the work of communicating intensity.
    _pulse = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 700),
    )..repeat(reverse: true);
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
      // Surface live partials in italic so the user sees the mic is
      // working between server-side finals.
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

/// Pulse-only orb. Three concentric circles whose radii are driven by
/// [pulse] * `baseAmplitude(state)` — the latter encodes who is speaking
/// so the visual feedback matches the audio without an extra clock.
class _RealtimeOrb extends StatelessWidget {
  const _RealtimeOrb({required this.state, required this.pulse});

  final RealtimeVoiceState state;
  final AnimationController pulse;

  @override
  Widget build(BuildContext context) {
    final ColorScheme scheme = Theme.of(context).colorScheme;

    return AnimatedBuilder(
      animation: pulse,
      builder: (BuildContext context, Widget? child) {
        final double base = _baseAmplitude(state);
        final double amplitude = pulse.value * base;

        final double outerSize = 202 + (amplitude * 26);
        final double middleSize = 160 + (amplitude * 18);
        final double coreSize = 110 + (amplitude * 10);

        final Color primary = state.phase == RealtimeVoicePhase.error
            ? scheme.error
            : scheme.primary;

        return SizedBox(
          width: 240,
          height: 240,
          child: Stack(
            alignment: Alignment.center,
            children: <Widget>[
              Container(
                width: outerSize,
                height: outerSize,
                decoration: BoxDecoration(
                  shape: BoxShape.circle,
                  color: primary.withValues(alpha: 0.08),
                ),
              ),
              Container(
                width: middleSize,
                height: middleSize,
                decoration: BoxDecoration(
                  shape: BoxShape.circle,
                  color: primary.withValues(alpha: 0.18),
                ),
              ),
              Container(
                width: coreSize,
                height: coreSize,
                decoration: BoxDecoration(
                  shape: BoxShape.circle,
                  gradient: RadialGradient(
                    colors: <Color>[
                      primary,
                      primary.withValues(alpha: 0.6),
                    ],
                  ),
                ),
                child: state.phase == RealtimeVoicePhase.connecting
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
              ),
            ],
          ),
        );
      },
    );
  }

  /// Encodes "what's happening" as a pulse-amplitude multiplier so the
  /// orb visibly tracks audio. Bot speaking = strongest beat, user
  /// speaking = mid, neither = subtle breath. Error = flat (no pulse).
  static double _baseAmplitude(RealtimeVoiceState state) {
    switch (state.phase) {
      case RealtimeVoicePhase.error:
        return 0;
      case RealtimeVoicePhase.idle:
        return 0.2;
      case RealtimeVoicePhase.connecting:
        return 0.35;
      case RealtimeVoicePhase.live:
        switch (state.whoIsSpeaking) {
          case RealtimeSpeaker.bot:
            return 0.9;
          case RealtimeSpeaker.user:
            return 0.55;
          case RealtimeSpeaker.none:
            return 0.3;
        }
    }
  }
}
