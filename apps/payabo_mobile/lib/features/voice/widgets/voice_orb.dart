// ignore_for_file: public_member_api_docs

import 'dart:math' as math;
import 'dart:ui';

import 'package:flutter/material.dart';

/// What's happening inside the orb at any moment. Maps to the phase icon
/// (or Simi portrait) shown at the centre of the breathing core.
enum VoiceOrbCenter {
  /// Idle / no speaker — show the Simi portrait.
  portrait,
  user,
  bot,
  thinking,
  connecting,
  error,
}

/// Branded Simi orb: a soft circular halo, a faint outline ring, and an
/// orange breathing core with an inset sheen. Phase icon (or Simi portrait)
/// sits inside the core and swaps based on [center].
///
/// Mirrors `VoiceOrb` in the React starter kit (`payabo-core.jsx`). The orb
/// expects a parent-owned saw-tooth animation in [pulse] (0→1, repeating)
/// so the breathing rhythm stays in sync with the surrounding stage.
class VoiceOrb extends StatelessWidget {
  const VoiceOrb({
    super.key,
    required this.size,
    required this.pulse,
    required this.intensity,
    required this.center,
    required this.color,
    this.portraitAsset,
  });

  final double size;
  final Animation<double> pulse;

  /// 0..1 multiplier on halo opacity + breath amplitude. Bot speaking is
  /// typically 1.0, user speaking ~0.6, idle ~0.4, error ~0.0.
  final double intensity;
  final VoiceOrbCenter center;
  final Color color;

  /// Optional Simi portrait. When omitted, the [VoiceOrbCenter.portrait] case
  /// falls back to a generic mic icon so the orb still renders if the asset
  /// hasn't been bundled.
  final String? portraitAsset;

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: pulse,
      builder: (BuildContext context, Widget? _) {
        // sin envelope drives the breathing rhythm at the same period as the
        // surrounding stage's ring cycle — the orb feels alive even at rest.
        final double breath =
            (math.sin(pulse.value * 2 * math.pi) + 1) / 2;
        final double clampedIntensity = intensity.clamp(0.0, 1.0);

        final double coreSize = size * 0.55 + (breath * 6 * clampedIntensity);
        final double haloScale = 1 + breath * 0.06 * clampedIntensity;
        final double haloOpacity = (0.16 + breath * 0.08) * clampedIntensity;
        final double outlineOpacity =
            0.18 * clampedIntensity + breath * 0.05;
        final double sheenOpacity = 0.78 + breath * 0.12 * clampedIntensity;
        final double glowBlur = 18 + breath * 10 * clampedIntensity;

        return SizedBox(
          width: size,
          height: size,
          child: Stack(
            alignment: Alignment.center,
            children: <Widget>[
              // Soft circular halo — single subtle ring centred on the orb.
              _Halo(
                size: size * 0.92,
                scale: haloScale,
                color: color,
                opacity: haloOpacity.clamp(0.0, 1.0),
              ),
              // Faint outline ring just outside the core.
              _OutlineRing(
                size: size * 0.78,
                color: color.withValues(
                  alpha: outlineOpacity.clamp(0.0, 1.0),
                ),
              ),
              // Breathing core with inset sheen, glow shadow, and a phase
              // icon (or Simi portrait) on top.
              _Core(
                size: coreSize,
                color: color,
                glowBlur: glowBlur,
                glowOpacity: 0.28 * sheenOpacity,
                sheenOpacity: 0.38 * sheenOpacity,
                center: center,
                portraitAsset: portraitAsset,
              ),
            ],
          ),
        );
      },
    );
  }
}

class _Halo extends StatelessWidget {
  const _Halo({
    required this.size,
    required this.scale,
    required this.color,
    required this.opacity,
  });

  final double size;
  final double scale;
  final Color color;
  final double opacity;

  @override
  Widget build(BuildContext context) {
    return Transform.scale(
      scale: scale,
      child: Container(
        width: size,
        height: size,
        decoration: BoxDecoration(
          shape: BoxShape.circle,
          gradient: RadialGradient(
            colors: <Color>[
              color.withValues(alpha: opacity),
              color.withValues(alpha: 0),
            ],
            stops: const <double>[0.0, 0.62],
          ),
        ),
      ),
    );
  }
}

class _OutlineRing extends StatelessWidget {
  const _OutlineRing({required this.size, required this.color});

  final double size;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        border: Border.all(color: color, width: 1),
      ),
    );
  }
}

class _Core extends StatelessWidget {
  const _Core({
    required this.size,
    required this.color,
    required this.glowBlur,
    required this.glowOpacity,
    required this.sheenOpacity,
    required this.center,
    required this.portraitAsset,
  });

  final double size;
  final Color color;
  final double glowBlur;
  final double glowOpacity;
  final double sheenOpacity;
  final VoiceOrbCenter center;
  final String? portraitAsset;

  @override
  Widget build(BuildContext context) {
    // Three-stop radial gradient mirrors the React `radial-gradient(circle at
    // 35% 30%, #FFD3A4 0%, #F37920 55%, #C95F0B 100%)` — the off-centre light
    // anchor is what makes the orb feel three-dimensional.
    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        gradient: const RadialGradient(
          center: Alignment(-0.3, -0.4),
          colors: <Color>[
            Color(0xFFFFD3A4),
            Color(0xFFF37920),
            Color(0xFFC95F0B),
          ],
          stops: <double>[0.0, 0.55, 1.0],
        ),
        boxShadow: <BoxShadow>[
          BoxShadow(
            color: color.withValues(alpha: glowOpacity.clamp(0.0, 1.0)),
            blurRadius: glowBlur,
            spreadRadius: 2,
          ),
        ],
      ),
      child: ClipOval(
        child: Stack(
          alignment: Alignment.center,
          children: <Widget>[
            // Simi portrait — only when the orb has no specific phase icon
            // to show. Rendered at reduced opacity so it blends into the
            // orange core (the React version uses CSS luminosity blend mode;
            // plain opacity is close enough at a glance).
            if (center == VoiceOrbCenter.portrait && portraitAsset != null)
              _Portrait(asset: portraitAsset!),
            // Inset sheen — a soft top-left highlight that sells the core
            // as a polished sphere.
            _InsetSheen(opacity: sheenOpacity.clamp(0.0, 1.0)),
            // Phase icon on top of everything.
            _CenterIcon(center: center, size: size * 0.32),
          ],
        ),
      ),
    );
  }
}

class _Portrait extends StatelessWidget {
  const _Portrait({required this.asset});

  final String asset;

  @override
  Widget build(BuildContext context) {
    return Positioned.fill(
      child: Padding(
        padding: const EdgeInsets.all(8),
        child: ClipOval(
          // Luminosity-ish — the React template uses `mixBlendMode:
          // luminosity` which Flutter doesn't expose without a shader. We
          // approximate with a saturation-killing filter + opacity so the
          // portrait warms with the core rather than fighting it.
          child: ColorFiltered(
            colorFilter: const ColorFilter.matrix(<double>[
              0.33, 0.33, 0.33, 0, 0,
              0.33, 0.33, 0.33, 0, 0,
              0.33, 0.33, 0.33, 0, 0,
              0,    0,    0,    0.7, 0,
            ]),
            child: Image.asset(
              asset,
              fit: BoxFit.cover,
              errorBuilder:
                  (BuildContext _, Object __, StackTrace? ___) =>
                      const SizedBox.shrink(),
            ),
          ),
        ),
      ),
    );
  }
}

class _InsetSheen extends StatelessWidget {
  const _InsetSheen({required this.opacity});

  final double opacity;

  @override
  Widget build(BuildContext context) {
    return Positioned.fill(
      child: DecoratedBox(
        decoration: BoxDecoration(
          shape: BoxShape.circle,
          gradient: RadialGradient(
            center: const Alignment(-0.3, -0.5),
            radius: 0.7,
            colors: <Color>[
              Colors.white.withValues(alpha: opacity),
              Colors.white.withValues(alpha: 0),
            ],
            stops: const <double>[0.0, 0.5],
          ),
        ),
      ),
    );
  }
}

class _CenterIcon extends StatelessWidget {
  const _CenterIcon({required this.center, required this.size});

  final VoiceOrbCenter center;
  final double size;

  @override
  Widget build(BuildContext context) {
    final Widget? icon = switch (center) {
      VoiceOrbCenter.portrait => null,
      VoiceOrbCenter.user =>
        Icon(Icons.mic_rounded, color: Colors.white, size: size),
      VoiceOrbCenter.bot =>
        Icon(Icons.graphic_eq_rounded, color: Colors.white, size: size),
      VoiceOrbCenter.thinking =>
        Icon(Icons.auto_awesome_rounded, color: Colors.white, size: size),
      VoiceOrbCenter.connecting => SizedBox(
          width: size * 0.85,
          height: size * 0.85,
          child: const CircularProgressIndicator(
            strokeWidth: 2.5,
            valueColor: AlwaysStoppedAnimation<Color>(Colors.white),
          ),
        ),
      VoiceOrbCenter.error =>
        Icon(Icons.error_outline_rounded, color: Colors.white, size: size),
    };

    if (icon == null) return const SizedBox.shrink();

    // AnimatedSwitcher cross-fades icon changes so phase transitions don't
    // pop. KeyedSubtree forces a rebuild whenever the enum changes.
    return AnimatedSwitcher(
      duration: const Duration(milliseconds: 220),
      switchInCurve: Curves.easeOutCubic,
      switchOutCurve: Curves.easeInCubic,
      child: KeyedSubtree(
        key: ValueKey<VoiceOrbCenter>(center),
        child: icon,
      ),
    );
  }
}

/// Tiny helper for parents that want a quick orange highlight inside a
/// [Stack] without pulling in this whole widget — kept here so the visual
/// language stays in one file.
class VoiceOrbDot extends StatelessWidget {
  const VoiceOrbDot({super.key, required this.color, this.size = 6});

  final Color color;
  final double size;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: size,
      height: size,
      child: DecoratedBox(
        decoration: BoxDecoration(shape: BoxShape.circle, color: color),
      ),
    );
  }
}

/// Convenience filter for blurring overlays. Public so the stage can apply
/// the same blur to backdrop layers if needed.
ImageFilter voiceStageBackdropBlur(double sigma) =>
    ImageFilter.blur(sigmaX: sigma, sigmaY: sigma);
