// ignore_for_file: public_member_api_docs

import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../shared/theme/payabo_palette.dart';
import '../../shared/theme/payabo_spacing.dart';
import 'realtime_voice_controller.dart';
import 'widgets/voice_action_bar.dart';
import 'widgets/voice_glow_orb.dart';
import 'widgets/voice_orb.dart';
import 'widgets/voice_status_pill.dart';

/// Branded voice mode stage — mirrors the [`VoiceScreen`] from the Payabo
/// mobile starter kit (`Templates/payabo-mobile-starterkit/components/
/// screens-chat.jsx`).
///
/// Layout:
///   * Header — close button (left) + status pill (right) with a pulse dot.
///   * Centre — orb with a soft halo + breathing core, primary transcript
///     line, optional sub-line.
///   * Footer — three round buttons: mute, end, minimise.
///
/// All session lifecycle (busy / watchdog / error / mute) is owned by
/// [RealtimeVoiceController]. The stage is purely a renderer.
class RealtimeVoiceStage extends ConsumerStatefulWidget {
  const RealtimeVoiceStage({
    super.key,
    required this.onOrbTap,
    required this.onMinimise,
    this.subline,
  });

  /// Invoked when the orb / overall stage area is tapped. Controller decides
  /// whether this starts or stops the session.
  final Future<void> Function() onOrbTap;

  /// Hides the stage without ending the session — the call keeps running in
  /// the background and the user returns to chat history.
  final VoidCallback onMinimise;

  /// Optional muted sub-line below the primary transcript. The starter kit
  /// uses this for context like "That's GHS 1,942 at today's rate"; we leave
  /// it null in v1 because the live transcript already covers most prompts.
  final String? subline;

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
    // Single forward-only saw-tooth (0 → 1, jump back, repeat) over 2.6 s —
    // matches the React `period = 2600` in the starter kit so the breathing
    // feels identical.
    _pulse = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 2600),
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
    final Color accent = isError ? scheme.error : PayaboPalette.orange500;

    return Stack(
      children: <Widget>[
        // Ambient glow orb behind the orb — same trick the chat screen uses
        // to push the dark gradient towards "alive".
        Positioned(
          top: MediaQuery.sizeOf(context).height * 0.18,
          left: -80,
          child: const VoiceGlowOrb(
            size: 380,
            color: PayaboPalette.orange500,
            opacity: 0.32,
            blur: 80,
          ),
        ),
        const Positioned(
          bottom: 80,
          right: -60,
          child: VoiceGlowOrb(
            size: 220,
            color: Color(0xFFD7A14E),
            opacity: 0.18,
            blur: 70,
          ),
        ),
        SafeArea(
          child: Column(
            children: <Widget>[
              _StageHeader(
                accent: accent,
                state: state,
                onClose: widget.onMinimise,
              ),
              Expanded(
                child: LayoutBuilder(
                  builder:
                      (BuildContext context, BoxConstraints constraints) {
                    // Cap the orb at 240 px (the starter-kit size) but shrink
                    // on small screens so the transcript still has room.
                    final double orbSize = constraints.maxWidth < 320
                        ? constraints.maxWidth * 0.72
                        : 240;
                    return SingleChildScrollView(
                      padding: const EdgeInsets.symmetric(
                        horizontal: PayaboSpacing.xl,
                      ),
                      child: ConstrainedBox(
                        constraints: BoxConstraints(
                          minHeight: constraints.maxHeight,
                        ),
                        child: Column(
                          mainAxisAlignment: MainAxisAlignment.center,
                          children: <Widget>[
                            Semantics(
                              button: true,
                              label: 'Voice orb',
                              child: GestureDetector(
                                onTap: () =>
                                    unawaited(widget.onOrbTap()),
                                behavior: HitTestBehavior.opaque,
                                child: VoiceOrb(
                                  size: orbSize,
                                  pulse: _pulse,
                                  intensity: _intensityFor(state),
                                  color: accent,
                                  center: _centerFor(state),
                                  portraitAsset: 'assets/images/simi.png',
                                ),
                              ),
                            ),
                            const SizedBox(height: 28),
                            _TranscriptBlock(
                              state: state,
                              subline: widget.subline,
                            ),
                          ],
                        ),
                      ),
                    );
                  },
                ),
              ),
              Padding(
                padding: const EdgeInsets.fromLTRB(
                  PayaboSpacing.lg,
                  PayaboSpacing.md,
                  PayaboSpacing.lg,
                  PayaboSpacing.lg,
                ),
                child: VoiceActionBar(
                  muted: state.micMuted,
                  muteEnabled: state.phase == RealtimeVoicePhase.live,
                  onToggleMute: () => ref
                      .read(realtimeVoiceControllerProvider.notifier)
                      .toggleMute(),
                  onEnd: () => unawaited(_end()),
                  onMinimise: widget.onMinimise,
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }

  /// Tear the session down explicitly from the bottom-bar end button. The
  /// minimise button keeps the call alive — only this path stops it.
  Future<void> _end() async {
    await ref.read(realtimeVoiceControllerProvider.notifier).dismiss();
    if (!mounted) return;
    widget.onMinimise();
  }

  /// Map controller state → orb intensity. Bot speaking is the "loud" state,
  /// error is flat. Same shape as the starter kit's `intensity` calculation.
  static double _intensityFor(RealtimeVoiceState state) {
    switch (state.phase) {
      case RealtimeVoicePhase.error:
        return 0;
      case RealtimeVoicePhase.idle:
        return 0.4;
      case RealtimeVoicePhase.connecting:
        return 0.5;
      case RealtimeVoicePhase.live:
        switch (state.whoIsSpeaking) {
          case RealtimeSpeaker.bot:
            return 1.0;
          case RealtimeSpeaker.user:
            return 0.6;
          case RealtimeSpeaker.none:
            return 0.4;
        }
    }
  }

  static VoiceOrbCenter _centerFor(RealtimeVoiceState state) {
    switch (state.phase) {
      case RealtimeVoicePhase.idle:
        return VoiceOrbCenter.portrait;
      case RealtimeVoicePhase.connecting:
        return VoiceOrbCenter.connecting;
      case RealtimeVoicePhase.error:
        return VoiceOrbCenter.error;
      case RealtimeVoicePhase.live:
        switch (state.whoIsSpeaking) {
          case RealtimeSpeaker.bot:
            return VoiceOrbCenter.bot;
          case RealtimeSpeaker.user:
            return VoiceOrbCenter.user;
          case RealtimeSpeaker.none:
            return VoiceOrbCenter.portrait;
        }
    }
  }
}

class _StageHeader extends StatelessWidget {
  const _StageHeader({
    required this.accent,
    required this.state,
    required this.onClose,
  });

  final Color accent;
  final RealtimeVoiceState state;
  final VoidCallback onClose;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(
        PayaboSpacing.md,
        PayaboSpacing.sm,
        PayaboSpacing.md,
        PayaboSpacing.sm,
      ),
      child: Row(
        children: <Widget>[
          _HeaderIconButton(
            icon: Icons.close_rounded,
            onTap: onClose,
            semanticsLabel: 'Close voice mode',
          ),
          const Spacer(),
          VoiceStatusPill(
            label: _label(state),
            color: accent,
            showPulse: state.phase != RealtimeVoicePhase.error,
          ),
        ],
      ),
    );
  }

  static String _label(RealtimeVoiceState state) {
    if (state.micMuted && state.phase == RealtimeVoicePhase.live) {
      return 'MIC MUTED';
    }
    return switch (state.phase) {
      RealtimeVoicePhase.idle => 'SIMI',
      RealtimeVoicePhase.connecting => 'OPENING THE LINE',
      RealtimeVoicePhase.error => 'TAP TO RETRY',
      RealtimeVoicePhase.live => switch (state.whoIsSpeaking) {
          RealtimeSpeaker.bot => 'SIMI SPEAKING',
          RealtimeSpeaker.user => 'SIMI LISTENING',
          RealtimeSpeaker.none => 'SIMI LIVE',
        },
    };
  }
}

class _HeaderIconButton extends StatelessWidget {
  const _HeaderIconButton({
    required this.icon,
    required this.onTap,
    required this.semanticsLabel,
  });

  final IconData icon;
  final VoidCallback onTap;
  final String semanticsLabel;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      button: true,
      label: semanticsLabel,
      child: GestureDetector(
        onTap: onTap,
        behavior: HitTestBehavior.opaque,
        child: Container(
          width: 38,
          height: 38,
          decoration: BoxDecoration(
            shape: BoxShape.circle,
            color: Colors.white.withValues(alpha: 0.06),
            border: Border.all(color: Colors.white.withValues(alpha: 0.1)),
          ),
          child: Icon(icon, color: Colors.white, size: 18),
        ),
      ),
    );
  }
}

class _TranscriptBlock extends StatelessWidget {
  const _TranscriptBlock({required this.state, required this.subline});

  final RealtimeVoiceState state;
  final String? subline;

  @override
  Widget build(BuildContext context) {
    // Error state owns the whole text block.
    if (state.phase == RealtimeVoicePhase.error) {
      return _ErrorPanel(message: state.errorMessage);
    }

    final ({String text, bool italic}) primary = _primary(state);
    if (primary.text.isEmpty && (subline == null || subline!.isEmpty)) {
      return const SizedBox.shrink();
    }

    return ConstrainedBox(
      constraints: const BoxConstraints(maxWidth: 360),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          if (primary.text.isNotEmpty)
            AnimatedSwitcher(
              duration: const Duration(milliseconds: 220),
              child: Text(
                primary.text,
                key: ValueKey<String>(primary.text),
                textAlign: TextAlign.center,
                style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                      color: Colors.white,
                      fontWeight: FontWeight.w700,
                      height: 1.2,
                      letterSpacing: -0.4,
                      fontStyle: primary.italic
                          ? FontStyle.italic
                          : FontStyle.normal,
                    ),
              ),
            ),
          if (subline != null && subline!.isNotEmpty) ...<Widget>[
            const SizedBox(height: 10),
            Text(
              subline!,
              textAlign: TextAlign.center,
              style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                    color: Colors.white.withValues(alpha: 0.65),
                    height: 1.55,
                  ),
            ),
          ],
          // Tap hint while idle so the user knows what to do.
          if (state.phase == RealtimeVoicePhase.idle) ...<Widget>[
            const SizedBox(height: 12),
            Text(
              'Tap the orb to start',
              textAlign: TextAlign.center,
              style: Theme.of(context).textTheme.bodySmall?.copyWith(
                    color: Colors.white.withValues(alpha: 0.5),
                    letterSpacing: 0.4,
                  ),
            ),
          ],
        ],
      ),
    );
  }

  /// Resolution order for the primary line:
  ///   1. Live assistant text while the bot is speaking (the "what Simi is
  ///      saying" view).
  ///   2. Live partial transcription while the user is speaking.
  ///   3. Phase-specific placeholders.
  static ({String text, bool italic}) _primary(RealtimeVoiceState state) {
    if (state.phase == RealtimeVoicePhase.live) {
      if (state.whoIsSpeaking == RealtimeSpeaker.bot &&
          state.liveAssistantText.isNotEmpty) {
        return (text: state.liveAssistantText, italic: false);
      }
      if (state.livePartialTranscript.isNotEmpty) {
        return (text: state.livePartialTranscript, italic: true);
      }
    }
    final String fallback = switch (state.phase) {
      RealtimeVoicePhase.idle => '',
      RealtimeVoicePhase.connecting => 'Opening the line…',
      RealtimeVoicePhase.live => switch (state.whoIsSpeaking) {
          RealtimeSpeaker.bot => '',
          RealtimeSpeaker.user => 'Listening…',
          RealtimeSpeaker.none => 'Speak whenever you’re ready.',
        },
      RealtimeVoicePhase.error => '',
    };
    return (text: fallback, italic: false);
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
