import 'package:flutter/material.dart';

import '../../../../shared/theme/payabo_color_resolver.dart';

/// Full-screen hero background for the spending empty state.
///
/// Mirrors [SetupHeroBackground] but uses a spending-specific hero
/// image (`assets/images/spending-empty-hero.png`) with a gradient
/// scrim overlay for text readability.
///
/// Falls back to a warm gradient when the image fails to load
/// (e.g. in widget tests where assets aren't bundled).
class SpendingHeroBackground extends StatelessWidget {
  const SpendingHeroBackground({super.key});

  static const String _heroAsset = 'assets/images/spending-empty-hero.png';

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final bool isDark = c.isDark;

    return Stack(
      fit: StackFit.expand,
      children: <Widget>[
        // Hero photograph — covers the full area.
        Image.asset(
          _heroAsset,
          fit: BoxFit.cover,
          width: double.infinity,
          height: double.infinity,
          errorBuilder: (_, __, ___) => _GradientFallback(isDark: isDark),
        ),

        // Gradient scrim — ensures text legibility over the photo.
        DecoratedBox(
          decoration: BoxDecoration(
            gradient: isDark ? _darkScrim : _lightScrim,
          ),
          child: const SizedBox.expand(),
        ),
      ],
    );
  }

  /// Light mode: warm white fading from ~75 % opaque at the top
  /// to ~20 % at the bottom so the image shows through.
  static const LinearGradient _lightScrim = LinearGradient(
    colors: <Color>[
      Color(0xBFFFEEDD), // ~75 % opaque warm peach
      Color(0x66FFF5EC), // ~40 % opaque warm peach
      Color(0x33F7EBD9), // ~20 % opaque warm sand
    ],
    begin: Alignment.topCenter,
    end: Alignment.bottomCenter,
    stops: <double>[0.0, 0.45, 1.0],
  );

  /// Dark mode: dark surface fading from ~80 % opaque at the top
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
  const _GradientFallback({required this.isDark});

  final bool isDark;

  @override
  Widget build(BuildContext context) {
    if (isDark) {
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

    return const DecoratedBox(
      decoration: BoxDecoration(
        gradient: LinearGradient(
          colors: <Color>[
            Color(0xFFFFF5EC),
            Color(0xFFFFEEDD),
            Color(0xFFF7EBD9),
          ],
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
          stops: <double>[0.0, 0.5, 1.0],
        ),
      ),
      child: SizedBox.expand(),
    );
  }
}
