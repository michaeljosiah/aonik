import 'package:flutter/material.dart';

/// Full-screen hero background for the setup journey.
///
/// Displays the hero photograph (`assets/images/setup-hero.png`)
/// with a dark gradient scrim overlay so text remains readable.
///
/// Uses the same dark treatment in both light and dark mode so
/// the setup flow has a consistent cinematic feel regardless of
/// the user's system theme.
///
/// Falls back to a plain gradient if the image fails to load
/// (e.g. in widget tests where assets aren't bundled).
class SetupHeroBackground extends StatelessWidget {
  const SetupHeroBackground({super.key});

  static const String _heroAsset = 'assets/images/setup-hero.png';

  @override
  Widget build(BuildContext context) {
    return Stack(
      fit: StackFit.expand,
      children: <Widget>[
        // Hero photograph — shown in both light and dark modes.
        Image.asset(
          _heroAsset,
          fit: BoxFit.cover,
          width: double.infinity,
          height: double.infinity,
          errorBuilder: (_, __, ___) => const _GradientFallback(),
        ),

        // Gradient scrim — ensures text legibility over the photo.
        const DecoratedBox(
          decoration: BoxDecoration(
            gradient: _darkScrim,
          ),
          child: SizedBox.expand(),
        ),
      ],
    );
  }

  /// Dark scrim: dark surface fading from ~80 % opaque at the top
  /// to ~40 % at the bottom so the image is visible but muted.
  static const LinearGradient _darkScrim = LinearGradient(
    colors: <Color>[
      Color(0xCC1A1A1A), // ~80 % opaque dark
      Color(0x991A1A1A), // ~60 % opaque dark
      Color(0x661A1A1A), // ~40 % opaque dark
    ],
    begin: Alignment.topCenter,
    end: Alignment.bottomCenter,
    stops: <double>[0.0, 0.45, 1.0],
  );
}

/// Gradient-only fallback used when the hero image asset fails
/// to load (e.g. in test environments or if the file is missing).
class _GradientFallback extends StatelessWidget {
  const _GradientFallback();

  @override
  Widget build(BuildContext context) {
    return const DecoratedBox(
      decoration: BoxDecoration(
        gradient: LinearGradient(
          colors: <Color>[
            Color(0xFF1A1A1A),
            Color(0xFF121212),
          ],
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
        ),
      ),
      child: SizedBox.expand(),
    );
  }
}
