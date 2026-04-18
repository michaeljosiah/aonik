import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';

/// Warms caches during the splash window so the first few screens after
/// navigation render without perceivable load artefacts:
///  * Open Sans (the theme font) is queued for fetch so no fallback-font
///    flash occurs on the first `Text` after splash.
///  * Intro carousel PNGs and first-landing hero images are decoded into
///    the image cache so `Image.asset` hits warm memory.
///
/// Best-effort: individual failures (network-blocked fonts, missing asset,
/// etc.) are swallowed so a flaky warmup never hangs the splash.
abstract final class SplashWarmup {
  /// Six slider images shown on the intro carousel (the screen navigated to
  /// immediately after splash). Order matches `IntroScreen._pages`.
  static const List<String> _introSlider = <String>[
    'assets/images/slider-img-04.png',
    'assets/images/slider-img-01.png',
    'assets/images/slider-img-02.png',
    'assets/images/slider-img-03.png',
    'assets/images/slider-img-05.png',
    'assets/images/slider-img-06.png',
  ];

  /// Hero images surfaced on the screens that commonly follow intro.
  static const List<String> _firstLandingHeroes = <String>[
    'assets/images/setup-hero.png',
    'assets/images/simi.png',
    'assets/images/demo_profile.jpg',
  ];

  /// Font weights Payabo actively composes with (see payabo_typography.dart
  /// and the orbit loader). Pre-queuing ensures the google_fonts http fetch
  /// has completed before the first post-splash frame.
  static List<TextStyle> _fontPrewarmStyles() => <TextStyle>[
        GoogleFonts.openSans(fontWeight: FontWeight.w400),
        GoogleFonts.openSans(fontWeight: FontWeight.w600),
        GoogleFonts.openSans(fontWeight: FontWeight.w700),
        GoogleFonts.openSans(fontWeight: FontWeight.w800),
      ];

  static Future<void> run(BuildContext context) async {
    final List<Future<void>> tasks = <Future<void>>[
      GoogleFonts.pendingFonts(_fontPrewarmStyles())
          .catchError((Object _) => <void>[]),
      for (final String asset in <String>[
        ..._introSlider,
        ..._firstLandingHeroes,
      ])
        precacheImage(AssetImage(asset), context)
            .catchError((Object _) {}),
    ];
    await Future.wait(tasks);
  }
}
