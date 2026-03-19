import 'package:flutter/material.dart';

import 'payabo_palette.dart';

abstract final class PayaboColors {
  // Brand foundation
  static const Color brandPrimary = PayaboPalette.orange500;
  static const Color brandPrimaryHover = PayaboPalette.orange600;

  // Typography roles
  static const Color textPrimary = PayaboPalette.ink900;
  static const Color textSecondary = PayaboPalette.warm900;
  static const Color textMuted = PayaboPalette.neutral500;
  static const Color textSubtleWarm = PayaboPalette.warm800;
  static const Color textInverse = PayaboPalette.white;

  // Surface roles
  static const Color surfaceBase = PayaboPalette.white;
  static const Color surfaceSubtle = PayaboPalette.neutral050;
  static const Color surfaceMuted = PayaboPalette.neutral100;
  static const Color surfaceWarm = PayaboPalette.warm100;
  static const Color surfaceWarmElevated = PayaboPalette.warm050;
  static const Color surfaceWarmAccent = PayaboPalette.warm200;

  // Border roles
  static const Color borderDefault = PayaboPalette.neutral200;
  static const Color borderStrong = PayaboPalette.neutralShadow;
  static const Color borderWarm = PayaboPalette.warm300;

  // State roles
  static const Color statusSuccess = PayaboPalette.success500;
  static const Color statusSuccessSoft = PayaboPalette.success050;
  static const Color statusWarning = PayaboPalette.warning500;
  static const Color statusDanger = PayaboPalette.danger500;
  static const Color statusInfo = PayaboPalette.info500;

  // Header roles
  static const Color headerTitle = PayaboPalette.warm900;
  static const Color headerSubtitle = PayaboPalette.warm800;
  static const Color headerIconSurface = PayaboPalette.warm050;
  static const Color headerIconSurfaceAccent = PayaboPalette.warm200;
  static const Color headerIconBorder = PayaboPalette.warm300;
  static const Color headerIconAccent = PayaboPalette.warm600;
  static const Color headerNotificationDot = PayaboPalette.warm500;

  // Navigation roles
  static const Color navBackground = PayaboPalette.white;
  static const Color navBorder = PayaboPalette.navBorder;
  static const Color navSelected = PayaboPalette.navSelected;
  static const Color navUnselected = PayaboPalette.navUnselected;
  static const Color navFabBackground = PayaboPalette.orange500;
  static const Color navShadow = PayaboPalette.black12;
  static const Color navFabShadow = PayaboPalette.black16;

  // Spending roles
  static const List<Color> spendingAccountGradientPrimary = <Color>[
    Color(0xFFFFF8F0),
    Color(0xFFFFE9D4),
  ];
  static const List<Color> spendingAccountGradientSavings = <Color>[
    Color(0xFFFFFBF6),
    Color(0xFFF7EBDD),
  ];
  static const List<Color> spendingAccountGradientBills = <Color>[
    Color(0xFFFFFCF8),
    Color(0xFFF6EDE3),
  ];
  static const Color spendingAccountAccentPrimary = Color(0xFFD86F17);
  static const Color spendingAccountAccentSavings = Color(0xFFF08A2E);
  static const Color spendingAccountAccentBills = Color(0xFFE39A4C);
  static const Color spendingSliceBills = Color(0xFFEA7A27);
  static const Color spendingSliceOther = Color(0xFFFFC56D);
  static const Color spendingMerchantIconDark = Color(0xFF111111);
  static const Color spendingMerchantIconWarmSurface = Color(0xFFF4F1EC);
  static const Color spendingMerchantIconWarmAccent = Color(0xFFFFEFE3);
  static const Color spendingMerchantIconWarmText = Color(0xFF7A3211);
  static const Color spendingDotInactive = Color(0xFFF1D9C4);
  static const Color spendingQuickActionSurface = Color(0xFFFFF3E8);
  static const Color spendingQuickActionBorder = Color(0xFFF1DEC9);
  static const Color spendingTrendGrid = Color(0xFFE8DDD2);
  static const Color spendingCardWarm = Color(0xFFFFFAF5);
  static const Color spendingCardWarmElevated = Color(0xFFFFFBF8);
  static const Color spendingInsightLabel = Color(0xFF7A3211);
  static const Color spendingInsightBorder = Color(0xFFF2C79A);

  // Chat roles
  static const Color chatScreenSurface = Color(0xFFF8ECDD);

  // Card roles (shared across dashboard, pay, spending)
  static const Color cardWarmBackground = spendingCardWarmElevated; // 0xFFFFFBF8
  static const Color cardWarmBorder = spendingQuickActionBorder; // 0xFFF1DEC9
  static const Color insightAccent = Color(0xFFD3A04B);
  static const Color chatGlowPrimary = Color(0x22F37920);
  static const Color chatGlowSecondary = Color(0x1FD4A36A);
  static const Color chatTextPrimary = Color(0xFF4B2B1F);
  static const Color chatTextSecondary = Color(0xFF6E4B3D);
  static const Color chatTextTertiary = Color(0xFF5C3A2D);
  static const Color chatPlanBorder = Color(0xFFD9C7B8);
  static const Color chatPlanIconSurface = Color(0x1AF37920);
  static const Color chatComposerSurface = Color(0xFFEFE6DC);
  static const Color chatComposerHandle = Color(0xFFD0C1B5);
  static const Color chatInputBorder = Color(0xFFD8D0C8);
  static const Color chatSendActive = Color(0xFF4B2B1F);

  // Utility
  static const Color transparent = PayaboPalette.transparent;

  // Backwards-compatible aliases
  static const Color primary = brandPrimary;
  static const Color primaryHover = brandPrimaryHover;
  static const Color ink = textPrimary;
  static const Color muted = textMuted;
  static const Color accentBrown = headerTitle;
  static const Color accentBrownMuted = headerSubtitle;
  static const Color white = surfaceBase;
  static const Color background = surfaceMuted;
  static const Color backgroundSoft = surfaceSubtle;
  static const Color border = borderDefault;
  static const Color success = statusSuccess;
  static const Color successSoft = statusSuccessSoft;
  static const Color warning = statusWarning;
  static const Color danger = statusDanger;
  static const Color info = statusInfo;
}
