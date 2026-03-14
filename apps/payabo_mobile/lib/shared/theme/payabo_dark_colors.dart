import 'package:flutter/material.dart';

import 'payabo_palette.dart';

/// Semantic color roles for the dark theme.
///
/// Mirrors the structure of [PayaboColors] so every widget that references
/// semantic tokens can be theme-aware.
abstract final class PayaboDarkColors {
  // ── Brand ──────────────────────────────────────────────
  static const Color brandPrimary = PayaboPalette.orange500;
  static const Color brandPrimaryHover = PayaboPalette.orange600;

  // ── Typography ─────────────────────────────────────────
  static const Color textPrimary = PayaboPalette.darkWhite;
  static const Color textSecondary = PayaboPalette.dark100;
  static const Color textMuted = PayaboPalette.dark200;
  static const Color textSubtleWarm = PayaboPalette.dark200;
  static const Color textInverse = PayaboPalette.ink900;

  // ── Surfaces ───────────────────────────────────────────
  static const Color surfaceBase = PayaboPalette.dark900;
  static const Color surfaceSubtle = PayaboPalette.dark950;
  static const Color surfaceMuted = PayaboPalette.dark800;
  static const Color surfaceWarm = PayaboPalette.dark900;
  static const Color surfaceWarmElevated = PayaboPalette.dark800;
  static const Color surfaceWarmAccent = PayaboPalette.dark700;
  static const Color surfaceCard = PayaboPalette.dark700;
  static const Color surfaceCardElevated = PayaboPalette.dark600;

  // ── Borders ────────────────────────────────────────────
  static const Color borderDefault = PayaboPalette.dark500;
  static const Color borderStrong = PayaboPalette.dark400;
  static const Color borderWarm = PayaboPalette.dark500;

  // ── Status ─────────────────────────────────────────────
  static const Color statusSuccess = PayaboPalette.success500;
  static const Color statusSuccessSoft = Color(0xFF1A3A24);
  static const Color statusWarning = PayaboPalette.warning500;
  static const Color statusDanger = PayaboPalette.danger500;
  static const Color statusInfo = PayaboPalette.info500;

  // ── Header ─────────────────────────────────────────────
  static const Color headerTitle = PayaboPalette.darkWhite;
  static const Color headerSubtitle = PayaboPalette.dark200;
  static const Color headerIconSurface = PayaboPalette.dark700;
  static const Color headerIconSurfaceAccent = PayaboPalette.dark600;
  static const Color headerIconBorder = PayaboPalette.dark500;
  static const Color headerIconAccent = PayaboPalette.orange500;
  static const Color headerNotificationDot = PayaboPalette.orange500;

  // ── Navigation ─────────────────────────────────────────
  static const Color navBackground = PayaboPalette.dark800;
  static const Color navBorder = PayaboPalette.darkNavBorder;
  static const Color navSelected = PayaboPalette.darkNavSelected;
  static const Color navUnselected = PayaboPalette.darkNavUnselected;
  static const Color navFabBackground = PayaboPalette.orange500;
  static const Color navShadow = Color(0x33000000);
  static const Color navFabShadow = Color(0x40000000);

  // ── Spending ───────────────────────────────────────────
  static const List<Color> spendingAccountGradientPrimary = <Color>[
    Color(0xFF1A2332),
    Color(0xFF1F2A3A),
  ];
  static const List<Color> spendingAccountGradientSavings = <Color>[
    Color(0xFF19222E),
    Color(0xFF1D2836),
  ];
  static const List<Color> spendingAccountGradientBills = <Color>[
    Color(0xFF1A2230),
    Color(0xFF1E2938),
  ];
  static const Color spendingAccountAccentPrimary = PayaboPalette.orange500;
  static const Color spendingAccountAccentSavings = Color(0xFFF08A2E);
  static const Color spendingAccountAccentBills = Color(0xFFE39A4C);
  static const Color spendingSliceBills = Color(0xFFEA7A27);
  static const Color spendingSliceOther = Color(0xFFFFC56D);
  static const Color spendingMerchantIconDark = PayaboPalette.darkWhite;
  static const Color spendingMerchantIconWarmSurface = PayaboPalette.dark600;
  static const Color spendingMerchantIconWarmAccent = PayaboPalette.dark600;
  static const Color spendingMerchantIconWarmText = PayaboPalette.dark100;
  static const Color spendingDotInactive = PayaboPalette.dark500;
  static const Color spendingQuickActionSurface = PayaboPalette.dark700;
  static const Color spendingQuickActionBorder = PayaboPalette.dark500;
  static const Color spendingTrendGrid = PayaboPalette.dark500;
  static const Color spendingCardWarm = PayaboPalette.dark700;
  static const Color spendingCardWarmElevated = PayaboPalette.dark700;
  static const Color spendingInsightLabel = PayaboPalette.orange500;
  static const Color spendingInsightBorder = PayaboPalette.dark500;

  // ── Chat ───────────────────────────────────────────────
  static const Color chatScreenSurface = PayaboPalette.dark900;
  static const Color chatGlowPrimary = Color(0x22F37920);
  static const Color chatGlowSecondary = Color(0x1FD4A36A);
  static const Color chatTextPrimary = PayaboPalette.darkWhite;
  static const Color chatTextSecondary = PayaboPalette.dark200;
  static const Color chatTextTertiary = PayaboPalette.dark300;
  static const Color chatPlanBorder = PayaboPalette.dark500;
  static const Color chatPlanIconSurface = Color(0x1AF37920);
  static const Color chatComposerSurface = PayaboPalette.dark800;
  static const Color chatComposerHandle = PayaboPalette.dark400;
  static const Color chatInputBorder = PayaboPalette.dark500;
  static const Color chatSendActive = PayaboPalette.darkWhite;

  // ── Utility ────────────────────────────────────────────
  static const Color transparent = PayaboPalette.transparent;

  // ── Backwards-compatible aliases ───────────────────────
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
