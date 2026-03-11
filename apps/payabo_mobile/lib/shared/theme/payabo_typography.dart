import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';

import 'payabo_colors.dart';

TextTheme buildPayaboTextTheme(TextTheme base) {
  final textTheme = GoogleFonts.openSansTextTheme(base);

  return textTheme.copyWith(
    displayLarge: textTheme.displayLarge?.copyWith(
      fontSize: 60,
      height: 66 / 60,
      fontWeight: FontWeight.w300,
      letterSpacing: 0,
      color: PayaboColors.ink,
    ),
    displayMedium: textTheme.displayMedium?.copyWith(
      fontSize: 48,
      height: 52 / 48,
      fontWeight: FontWeight.w300,
      letterSpacing: 0,
      color: PayaboColors.ink,
    ),
    headlineLarge: textTheme.headlineLarge?.copyWith(
      fontSize: 42,
      height: 45 / 42,
      fontWeight: FontWeight.w300,
      letterSpacing: 0,
      color: PayaboColors.ink,
    ),
    headlineMedium: textTheme.headlineMedium?.copyWith(
      fontSize: 27,
      height: 34 / 27,
      fontWeight: FontWeight.w700,
      letterSpacing: 0,
      color: PayaboColors.ink,
    ),
    titleLarge: textTheme.titleLarge?.copyWith(
      fontSize: 20,
      height: 27 / 20,
      fontWeight: FontWeight.w700,
      letterSpacing: 0,
      color: PayaboColors.ink,
    ),
    titleMedium: textTheme.titleMedium?.copyWith(
      fontSize: 18,
      height: 25 / 18,
      fontWeight: FontWeight.w700,
      letterSpacing: 0,
      color: PayaboColors.ink,
    ),
    titleSmall: textTheme.titleSmall?.copyWith(
      fontSize: 16,
      height: 24 / 16,
      fontWeight: FontWeight.w600,
      letterSpacing: 0,
      color: PayaboColors.ink,
    ),
    bodyLarge: textTheme.bodyLarge?.copyWith(
      fontSize: 16,
      height: 24 / 16,
      fontWeight: FontWeight.w400,
      letterSpacing: 0,
      color: PayaboColors.ink,
    ),
    bodyMedium: textTheme.bodyMedium?.copyWith(
      fontSize: 15,
      height: 22 / 15,
      fontWeight: FontWeight.w400,
      letterSpacing: 0,
      color: PayaboColors.ink,
    ),
    bodySmall: textTheme.bodySmall?.copyWith(
      fontSize: 13,
      height: 18 / 13,
      fontWeight: FontWeight.w400,
      letterSpacing: 0,
      color: PayaboColors.muted,
    ),
    labelLarge: textTheme.labelLarge?.copyWith(
      fontSize: 14,
      height: 20 / 14,
      fontWeight: FontWeight.w700,
      letterSpacing: 0,
      color: PayaboColors.ink,
    ),
    labelMedium: textTheme.labelMedium?.copyWith(
      fontSize: 14,
      height: 20 / 14,
      fontWeight: FontWeight.w600,
      letterSpacing: 0,
      color: PayaboColors.muted,
    ),
  );
}
