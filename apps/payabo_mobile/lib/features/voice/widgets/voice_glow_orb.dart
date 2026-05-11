// ignore_for_file: public_member_api_docs

import 'dart:ui';

import 'package:flutter/material.dart';

import '../../../shared/theme/payabo_palette.dart';

/// Soft blurred circle painted behind the voice stage for ambient glow.
///
/// Mirrors `GlowOrb` from the React starter kit (`payabo-core.jsx`). Used
/// inside a [Stack] with positional offsets — the orb itself just owns the
/// blur + colour + opacity.
class VoiceGlowOrb extends StatelessWidget {
  const VoiceGlowOrb({
    super.key,
    this.size = 240,
    this.color = PayaboPalette.orange500,
    this.opacity = 0.35,
    this.blur = 60,
  });

  final double size;
  final Color color;
  final double opacity;
  final double blur;

  @override
  Widget build(BuildContext context) {
    return IgnorePointer(
      child: ImageFiltered(
        imageFilter: ImageFilter.blur(sigmaX: blur, sigmaY: blur),
        child: Container(
          width: size,
          height: size,
          decoration: BoxDecoration(
            shape: BoxShape.circle,
            color: color.withValues(alpha: opacity),
          ),
        ),
      ),
    );
  }
}
