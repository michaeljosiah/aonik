import 'package:flutter/material.dart';

import 'payabo_borders.dart';
import 'payabo_colors.dart';
import 'payabo_radii.dart';
import 'payabo_shadows.dart';
import 'payabo_spacing.dart';
import 'payabo_typography.dart';

ThemeData buildPayaboTheme() {
  final base = ThemeData(
    useMaterial3: true,
    colorScheme: ColorScheme.fromSeed(
      seedColor: PayaboColors.primary,
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
        minimumSize: const Size.fromHeight(52),
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
        minimumSize: const Size.fromHeight(52),
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
      _PayaboShadowTheme(PayaboShadows.medium),
    ],
  );
}

class _PayaboShadowTheme extends ThemeExtension<_PayaboShadowTheme> {
  const _PayaboShadowTheme(this.cardShadow);

  final List<BoxShadow> cardShadow;

  @override
  ThemeExtension<_PayaboShadowTheme> copyWith({List<BoxShadow>? cardShadow}) {
    return _PayaboShadowTheme(cardShadow ?? this.cardShadow);
  }

  @override
  ThemeExtension<_PayaboShadowTheme> lerp(
    covariant ThemeExtension<_PayaboShadowTheme>? other,
    double t,
  ) {
    if (other is! _PayaboShadowTheme) {
      return this;
    }

    return t < 0.5 ? this : other;
  }
}
