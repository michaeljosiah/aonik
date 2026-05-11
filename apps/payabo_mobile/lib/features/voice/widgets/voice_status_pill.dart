// ignore_for_file: public_member_api_docs

import 'dart:math' as math;

import 'package:flutter/material.dart';

/// Compact uppercase status indicator with an animated pulse dot.
///
/// Mirrors the right-hand label in the React starter kit's [`VoiceScreen`]
/// header (`screens-chat.jsx`). The pulse dot is hidden when [showPulse] is
/// false so error states render as a flat label without the "live" cue.
class VoiceStatusPill extends StatefulWidget {
  const VoiceStatusPill({
    super.key,
    required this.label,
    required this.color,
    this.showPulse = true,
  });

  final String label;
  final Color color;
  final bool showPulse;

  @override
  State<VoiceStatusPill> createState() => _VoiceStatusPillState();
}

class _VoiceStatusPillState extends State<VoiceStatusPill>
    with SingleTickerProviderStateMixin {
  late final AnimationController _pulse;

  @override
  void initState() {
    super.initState();
    _pulse = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 1600),
    )..repeat();
  }

  @override
  void dispose() {
    _pulse.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        if (widget.showPulse) ...<Widget>[
          _PulseDot(controller: _pulse, color: widget.color),
          const SizedBox(width: 6),
        ],
        Text(
          widget.label,
          style: TextStyle(
            color: widget.color,
            fontSize: 10,
            height: 1.4,
            fontWeight: FontWeight.w800,
            letterSpacing: 1.4,
          ),
        ),
      ],
    );
  }
}

class _PulseDot extends StatelessWidget {
  const _PulseDot({required this.controller, required this.color});

  final AnimationController controller;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: controller,
      builder: (BuildContext context, Widget? _) {
        // sin envelope so the halo grows + fades on a smooth cycle. The solid
        // dot underneath stays at full opacity so the pill always reads
        // clearly even at the trough of the animation.
        final double phase = controller.value;
        final double envelope = math.sin(phase * math.pi);
        final double scale = 1 + envelope * 0.9;
        final double opacity = 0.6 - envelope * 0.45;
        return SizedBox(
          width: 6,
          height: 6,
          child: Stack(
            alignment: Alignment.center,
            children: <Widget>[
              Transform.scale(
                scale: scale,
                child: Container(
                  decoration: BoxDecoration(
                    shape: BoxShape.circle,
                    color: color.withValues(alpha: opacity.clamp(0.0, 1.0)),
                  ),
                ),
              ),
              Container(
                decoration: BoxDecoration(
                  shape: BoxShape.circle,
                  color: color,
                ),
              ),
            ],
          ),
        );
      },
    );
  }
}
