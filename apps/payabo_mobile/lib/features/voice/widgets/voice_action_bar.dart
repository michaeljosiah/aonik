// ignore_for_file: public_member_api_docs

import 'package:flutter/material.dart';

import '../../../shared/theme/payabo_palette.dart';

/// Three-button footer for the voice stage: mute, end, minimise.
///
/// Mirrors the bottom row of the React starter kit's [`VoiceScreen`]
/// (`screens-chat.jsx`):
///   * Left  — dark 56 px circle, mic / mic-off icon.
///   * Centre — orange 64 px filled circle with a glow shadow, close icon
///     (ends the voice session).
///   * Right — dark 56 px circle, chat icon (returns to the message view
///     without ending the session — so the call keeps running in the
///     background).
class VoiceActionBar extends StatelessWidget {
  const VoiceActionBar({
    super.key,
    required this.muted,
    required this.onToggleMute,
    required this.onEnd,
    required this.onMinimise,
    this.muteEnabled = true,
  });

  final bool muted;
  final VoidCallback onToggleMute;
  final VoidCallback onEnd;
  final VoidCallback onMinimise;

  /// Disables the mute toggle when the session isn't live (idle/connecting/
  /// error). Visually dims the button but keeps the layout stable.
  final bool muteEnabled;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.center,
      children: <Widget>[
        _SecondaryButton(
          icon: muted ? Icons.mic_off_rounded : Icons.mic_rounded,
          enabled: muteEnabled,
          onTap: onToggleMute,
          semanticsLabel: muted ? 'Unmute microphone' : 'Mute microphone',
        ),
        const SizedBox(width: 14),
        _PrimaryEndButton(onTap: onEnd),
        const SizedBox(width: 14),
        _SecondaryButton(
          icon: Icons.chat_bubble_outline_rounded,
          enabled: true,
          onTap: onMinimise,
          semanticsLabel: 'Return to chat',
        ),
      ],
    );
  }
}

class _SecondaryButton extends StatelessWidget {
  const _SecondaryButton({
    required this.icon,
    required this.onTap,
    required this.semanticsLabel,
    required this.enabled,
  });

  final IconData icon;
  final VoidCallback onTap;
  final String semanticsLabel;
  final bool enabled;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      button: true,
      label: semanticsLabel,
      child: Opacity(
        opacity: enabled ? 1.0 : 0.45,
        child: GestureDetector(
          onTap: enabled ? onTap : null,
          behavior: HitTestBehavior.opaque,
          child: Container(
            width: 56,
            height: 56,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color: Colors.white.withValues(alpha: 0.06),
              border: Border.all(
                color: Colors.white.withValues(alpha: 0.12),
              ),
            ),
            child: Icon(icon, color: Colors.white, size: 20),
          ),
        ),
      ),
    );
  }
}

class _PrimaryEndButton extends StatelessWidget {
  const _PrimaryEndButton({required this.onTap});

  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      button: true,
      label: 'End voice call',
      child: GestureDetector(
        onTap: onTap,
        behavior: HitTestBehavior.opaque,
        child: Container(
          width: 64,
          height: 64,
          decoration: BoxDecoration(
            shape: BoxShape.circle,
            color: PayaboPalette.orange500,
            boxShadow: <BoxShadow>[
              BoxShadow(
                color: PayaboPalette.orange500.withValues(alpha: 0.5),
                blurRadius: 16,
                offset: const Offset(0, 4),
              ),
            ],
          ),
          child: const Icon(
            Icons.close_rounded,
            color: Colors.white,
            size: 24,
          ),
        ),
      ),
    );
  }
}
