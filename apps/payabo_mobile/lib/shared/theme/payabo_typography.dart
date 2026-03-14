import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';

import 'payabo_colors.dart';

/// Builds the Payabo text theme with a compact, modern type scale.
///
/// The default [textColor] is [PayaboColors.ink] (light theme). Pass
/// the dark-theme text primary when building the dark text theme.
TextTheme buildPayaboTextTheme(TextTheme base, {Color? textColor}) {
  final Color primary = textColor ?? PayaboColors.ink;
  final Color muted = textColor?.withValues(alpha: 0.55) ?? PayaboColors.muted;

  final textTheme = GoogleFonts.openSansTextTheme(base);

  return textTheme.copyWith(
    // Hero / headline numbers (e.g. balance totals)
    displayLarge: textTheme.displayLarge?.copyWith(
      fontSize: 32,
      height: 38 / 32,
      fontWeight: FontWeight.w600,
      letterSpacing: -0.4,
      color: primary,
    ),
    displayMedium: textTheme.displayMedium?.copyWith(
      fontSize: 28,
      height: 34 / 28,
      fontWeight: FontWeight.w600,
      letterSpacing: -0.3,
      color: primary,
    ),
    // Page / section headings
    headlineLarge: textTheme.headlineLarge?.copyWith(
      fontSize: 24,
      height: 30 / 24,
      fontWeight: FontWeight.w600,
      letterSpacing: -0.2,
      color: primary,
    ),
    headlineMedium: textTheme.headlineMedium?.copyWith(
      fontSize: 20,
      height: 26 / 20,
      fontWeight: FontWeight.w700,
      letterSpacing: 0,
      color: primary,
    ),
    // Card / section titles
    titleLarge: textTheme.titleLarge?.copyWith(
      fontSize: 17,
      height: 22 / 17,
      fontWeight: FontWeight.w700,
      letterSpacing: 0,
      color: primary,
    ),
    titleMedium: textTheme.titleMedium?.copyWith(
      fontSize: 15,
      height: 20 / 15,
      fontWeight: FontWeight.w600,
      letterSpacing: 0,
      color: primary,
    ),
    titleSmall: textTheme.titleSmall?.copyWith(
      fontSize: 13,
      height: 18 / 13,
      fontWeight: FontWeight.w600,
      letterSpacing: 0,
      color: primary,
    ),
    // Body copy
    bodyLarge: textTheme.bodyLarge?.copyWith(
      fontSize: 14,
      height: 20 / 14,
      fontWeight: FontWeight.w400,
      letterSpacing: 0,
      color: primary,
    ),
    bodyMedium: textTheme.bodyMedium?.copyWith(
      fontSize: 13,
      height: 18 / 13,
      fontWeight: FontWeight.w400,
      letterSpacing: 0,
      color: primary,
    ),
    bodySmall: textTheme.bodySmall?.copyWith(
      fontSize: 11,
      height: 16 / 11,
      fontWeight: FontWeight.w400,
      letterSpacing: 0.1,
      color: muted,
    ),
    // Labels / chips / tags
    labelLarge: textTheme.labelLarge?.copyWith(
      fontSize: 12,
      height: 16 / 12,
      fontWeight: FontWeight.w700,
      letterSpacing: 0.1,
      color: primary,
    ),
    labelMedium: textTheme.labelMedium?.copyWith(
      fontSize: 11,
      height: 16 / 11,
      fontWeight: FontWeight.w600,
      letterSpacing: 0.1,
      color: muted,
    ),
  );
}
