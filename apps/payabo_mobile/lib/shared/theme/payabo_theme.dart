import 'package:flutter/material.dart';

import 'payabo_borders.dart';
import 'payabo_colors.dart';
import 'payabo_dark_colors.dart';
import 'payabo_palette.dart';
import 'payabo_radii.dart';
import 'payabo_shadows.dart';
import 'payabo_spacing.dart';
import 'payabo_typography.dart';

// ─────────────────────────────────────────────────────────
//  Light (warm) theme
// ─────────────────────────────────────────────────────────

ThemeData buildPayaboTheme() {
  final base = ThemeData(
    useMaterial3: true,
    brightness: Brightness.light,
    colorScheme: ColorScheme.fromSeed(
      seedColor: PayaboColors.primary,
      brightness: Brightness.light,
      primary: PayaboColors.primary,
      onPrimary: PayaboColors.white,
      surface: PayaboColors.white,
      onSurface: PayaboColors.ink,
      error: PayaboColors.danger,
      onError: PayaboColors.white,
      outline: PayaboColors.border,
    ),
    scaffoldBackgroundColor: PayaboColors.backgroundSoft,
  );

  return base.copyWith(
    textTheme: buildPayaboTextTheme(base.textTheme),
    dividerTheme: const DividerThemeData(
      color: PayaboColors.border,
      thickness: 1,
      space: 1,
    ),
    appBarTheme: base.appBarTheme.copyWith(
      backgroundColor: PayaboColors.white,
      foregroundColor: PayaboColors.ink,
      elevation: 0,
      surfaceTintColor: Colors.transparent,
      centerTitle: false,
    ),
    cardTheme: const CardThemeData(
      color: PayaboColors.white,
      elevation: 0,
      margin: EdgeInsets.zero,
      shape: RoundedRectangleBorder(
        borderRadius: PayaboRadii.radiusSm,
        side: PayaboBorders.strongBorder,
      ),
      shadowColor: PayaboColors.transparent,
    ),
    inputDecorationTheme: InputDecorationTheme(
      filled: true,
      fillColor: PayaboColors.white,
      contentPadding: const EdgeInsets.symmetric(
          horizontal: PayaboSpacing.lg, vertical: 14),
      labelStyle: base.textTheme.labelMedium,
      hintStyle: base.textTheme.bodyLarge?.copyWith(color: PayaboColors.muted),
      border: const OutlineInputBorder(
        borderRadius: BorderRadius.zero,
        borderSide: PayaboBorders.defaultBorder,
      ),
      enabledBorder: const OutlineInputBorder(
        borderRadius: BorderRadius.zero,
        borderSide: PayaboBorders.defaultBorder,
      ),
      focusedBorder: const OutlineInputBorder(
        borderRadius: BorderRadius.zero,
        borderSide: PayaboBorders.activeBorder,
      ),
      errorBorder: const OutlineInputBorder(
        borderRadius: BorderRadius.zero,
        borderSide: PayaboBorders.errorBorder,
      ),
      focusedErrorBorder: const OutlineInputBorder(
        borderRadius: BorderRadius.zero,
        borderSide: PayaboBorders.errorBorder,
      ),
    ),
    bottomSheetTheme: const BottomSheetThemeData(
      backgroundColor: PayaboColors.transparent,
      modalBackgroundColor: PayaboColors.transparent,
      showDragHandle: false,
    ),
    navigationBarTheme: NavigationBarThemeData(
      backgroundColor: PayaboColors.white,
      elevation: 0,
      indicatorColor: PayaboColors.background,
      labelTextStyle: WidgetStateProperty.resolveWith<TextStyle?>(
        (states) {
          if (states.contains(WidgetState.selected)) {
            return base.textTheme.bodySmall?.copyWith(
              color: PayaboColors.primary,
              fontWeight: FontWeight.w700,
            );
          }

          return base.textTheme.bodySmall?.copyWith(color: PayaboColors.muted);
        },
      ),
      iconTheme: WidgetStateProperty.resolveWith<IconThemeData?>(
        (states) {
          if (states.contains(WidgetState.selected)) {
            return const IconThemeData(color: PayaboColors.primary);
          }

          return const IconThemeData(color: PayaboColors.muted);
        },
      ),
    ),
    elevatedButtonTheme: ElevatedButtonThemeData(
      style: ElevatedButton.styleFrom(
        backgroundColor: PayaboColors.primary,
        foregroundColor: PayaboColors.white,
        minimumSize: const Size.fromHeight(48),
        elevation: 0,
        shadowColor: Colors.transparent,
        padding: const EdgeInsets.symmetric(
            horizontal: PayaboSpacing.lg, vertical: PayaboSpacing.md),
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(PayaboRadii.sm),
          side: PayaboBorders.buttonBorder,
        ),
      ),
    ),
    outlinedButtonTheme: OutlinedButtonThemeData(
      style: OutlinedButton.styleFrom(
        minimumSize: const Size.fromHeight(48),
        foregroundColor: PayaboColors.primary,
        side: PayaboBorders.buttonBorder,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(PayaboRadii.sm),
        ),
        padding: const EdgeInsets.symmetric(
            horizontal: PayaboSpacing.lg, vertical: PayaboSpacing.md),
      ),
    ),
    textButtonTheme: TextButtonThemeData(
      style: TextButton.styleFrom(
        foregroundColor: PayaboColors.primary,
        padding: const EdgeInsets.symmetric(
            horizontal: PayaboSpacing.sm, vertical: PayaboSpacing.xs),
      ),
    ),
    extensions: const <ThemeExtension<dynamic>>[
      PayaboShadowTheme(PayaboShadows.medium),
    ],
  );
}

// ─────────────────────────────────────────────────────────
//  Dark theme
// ─────────────────────────────────────────────────────────

ThemeData buildPayaboDarkTheme() {
  final base = ThemeData(
    useMaterial3: true,
    brightness: Brightness.dark,
    colorScheme: ColorScheme.fromSeed(
      seedColor: PayaboDarkColors.primary,
      brightness: Brightness.dark,
      primary: PayaboDarkColors.primary,
      onPrimary: PayaboPalette.ink900,
      surface: PayaboDarkColors.surfaceBase,
      onSurface: PayaboDarkColors.textPrimary,
      error: PayaboDarkColors.danger,
      onError: PayaboPalette.white,
      outline: PayaboDarkColors.border,
    ),
    scaffoldBackgroundColor: PayaboDarkColors.surfaceSubtle,
  );

  return base.copyWith(
    textTheme:
        buildPayaboTextTheme(base.textTheme, textColor: PayaboDarkColors.ink),
    dividerTheme: const DividerThemeData(
      color: PayaboDarkColors.border,
      thickness: 1,
      space: 1,
    ),
    appBarTheme: base.appBarTheme.copyWith(
      backgroundColor: PayaboDarkColors.surfaceBase,
      foregroundColor: PayaboDarkColors.textPrimary,
      elevation: 0,
      surfaceTintColor: Colors.transparent,
      centerTitle: false,
    ),
    cardTheme: const CardThemeData(
      color: PayaboDarkColors.surfaceCard,
      elevation: 0,
      margin: EdgeInsets.zero,
      shape: RoundedRectangleBorder(
        borderRadius: PayaboRadii.radiusSm,
        side: BorderSide(color: PayaboDarkColors.borderStrong),
      ),
      shadowColor: PayaboDarkColors.transparent,
    ),
    inputDecorationTheme: InputDecorationTheme(
      filled: true,
      fillColor: PayaboDarkColors.surfaceCard,
      contentPadding: const EdgeInsets.symmetric(
          horizontal: PayaboSpacing.lg, vertical: 14),
      labelStyle: base.textTheme.labelMedium,
      hintStyle:
          base.textTheme.bodyLarge?.copyWith(color: PayaboDarkColors.muted),
      border: const OutlineInputBorder(
        borderRadius: BorderRadius.zero,
        borderSide: BorderSide(color: PayaboDarkColors.border),
      ),
      enabledBorder: const OutlineInputBorder(
        borderRadius: BorderRadius.zero,
        borderSide: BorderSide(color: PayaboDarkColors.border),
      ),
      focusedBorder: const OutlineInputBorder(
        borderRadius: BorderRadius.zero,
        borderSide: BorderSide(color: PayaboDarkColors.primary),
      ),
      errorBorder: const OutlineInputBorder(
        borderRadius: BorderRadius.zero,
        borderSide: BorderSide(color: PayaboDarkColors.danger),
      ),
      focusedErrorBorder: const OutlineInputBorder(
        borderRadius: BorderRadius.zero,
        borderSide: BorderSide(color: PayaboDarkColors.danger),
      ),
    ),
    bottomSheetTheme: const BottomSheetThemeData(
      backgroundColor: PayaboDarkColors.transparent,
      modalBackgroundColor: PayaboDarkColors.transparent,
      showDragHandle: false,
    ),
    navigationBarTheme: NavigationBarThemeData(
      backgroundColor: PayaboDarkColors.navBackground,
      elevation: 0,
      indicatorColor: PayaboDarkColors.surfaceCard,
      labelTextStyle: WidgetStateProperty.resolveWith<TextStyle?>(
        (states) {
          if (states.contains(WidgetState.selected)) {
            return base.textTheme.bodySmall?.copyWith(
              color: PayaboDarkColors.primary,
              fontWeight: FontWeight.w700,
            );
          }

          return base.textTheme.bodySmall
              ?.copyWith(color: PayaboDarkColors.muted);
        },
      ),
      iconTheme: WidgetStateProperty.resolveWith<IconThemeData?>(
        (states) {
          if (states.contains(WidgetState.selected)) {
            return const IconThemeData(color: PayaboDarkColors.primary);
          }

          return const IconThemeData(color: PayaboDarkColors.navUnselected);
        },
      ),
    ),
    elevatedButtonTheme: ElevatedButtonThemeData(
      style: ElevatedButton.styleFrom(
        backgroundColor: PayaboDarkColors.primary,
        foregroundColor: PayaboPalette.ink900,
        minimumSize: const Size.fromHeight(48),
        elevation: 0,
        shadowColor: Colors.transparent,
        padding: const EdgeInsets.symmetric(
            horizontal: PayaboSpacing.lg, vertical: PayaboSpacing.md),
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(PayaboRadii.sm),
          side: const BorderSide(color: PayaboDarkColors.primary, width: 2),
        ),
      ),
    ),
    outlinedButtonTheme: OutlinedButtonThemeData(
      style: OutlinedButton.styleFrom(
        minimumSize: const Size.fromHeight(48),
        foregroundColor: PayaboDarkColors.primary,
        side: const BorderSide(color: PayaboDarkColors.primary, width: 2),
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(PayaboRadii.sm),
        ),
        padding: const EdgeInsets.symmetric(
            horizontal: PayaboSpacing.lg, vertical: PayaboSpacing.md),
      ),
    ),
    textButtonTheme: TextButtonThemeData(
      style: TextButton.styleFrom(
        foregroundColor: PayaboDarkColors.primary,
        padding: const EdgeInsets.symmetric(
            horizontal: PayaboSpacing.sm, vertical: PayaboSpacing.xs),
      ),
    ),
    extensions: const <ThemeExtension<dynamic>>[
      PayaboShadowTheme(PayaboShadows.soft),
    ],
  );
}

// ─────────────────────────────────────────────────────────
//  Shadow theme extension
// ─────────────────────────────────────────────────────────

class PayaboShadowTheme extends ThemeExtension<PayaboShadowTheme> {
  const PayaboShadowTheme(this.cardShadow);

  final List<BoxShadow> cardShadow;

  @override
  ThemeExtension<PayaboShadowTheme> copyWith({List<BoxShadow>? cardShadow}) {
    return PayaboShadowTheme(cardShadow ?? this.cardShadow);
  }

  @override
  ThemeExtension<PayaboShadowTheme> lerp(
    covariant ThemeExtension<PayaboShadowTheme>? other,
    double t,
  ) {
    if (other is! PayaboShadowTheme) {
      return this;
    }

    return t < 0.5 ? this : other;
  }
}
