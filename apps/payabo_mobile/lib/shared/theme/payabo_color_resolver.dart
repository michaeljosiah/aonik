import 'package:flutter/material.dart';

import 'payabo_colors.dart';
import 'payabo_dark_colors.dart';
import 'payabo_gradients.dart';

/// Provides the correct semantic color for the current theme brightness.
///
/// Usage:
/// ```dart
/// final c = context.colors; // or PayaboColorResolver.of(context)
/// Container(color: c.surfaceWarm);
/// ```
class PayaboColorResolver {
  const PayaboColorResolver._({required this.isDark});

  final bool isDark;

  /// Resolve from a [BuildContext].
  static PayaboColorResolver of(BuildContext context) {
    final brightness = Theme.of(context).brightness;
    return brightness == Brightness.dark ? _dark : _light;
  }

  static const _light = PayaboColorResolver._(isDark: false);
  static const _dark = PayaboColorResolver._(isDark: true);

  // ── Brand ──────────────────────────────────────────────
  Color get primary =>
      isDark ? PayaboDarkColors.primary : PayaboColors.primary;
  Color get primaryHover =>
      isDark ? PayaboDarkColors.primaryHover : PayaboColors.primaryHover;

  // ── Typography ─────────────────────────────────────────
  Color get textPrimary =>
      isDark ? PayaboDarkColors.textPrimary : PayaboColors.textPrimary;
  Color get textSecondary =>
      isDark ? PayaboDarkColors.textSecondary : PayaboColors.textSecondary;
  Color get textMuted =>
      isDark ? PayaboDarkColors.textMuted : PayaboColors.textMuted;
  Color get textSubtleWarm =>
      isDark ? PayaboDarkColors.textSubtleWarm : PayaboColors.textSubtleWarm;
  Color get textInverse =>
      isDark ? PayaboDarkColors.textInverse : PayaboColors.textInverse;

  // ── Surfaces ───────────────────────────────────────────
  Color get surfaceBase =>
      isDark ? PayaboDarkColors.surfaceBase : PayaboColors.surfaceBase;
  Color get surfaceSubtle =>
      isDark ? PayaboDarkColors.surfaceSubtle : PayaboColors.surfaceSubtle;
  Color get surfaceMuted =>
      isDark ? PayaboDarkColors.surfaceMuted : PayaboColors.surfaceMuted;
  Color get surfaceWarm =>
      isDark ? PayaboDarkColors.surfaceWarm : PayaboColors.surfaceWarm;
  Color get surfaceWarmElevated => isDark
      ? PayaboDarkColors.surfaceWarmElevated
      : PayaboColors.surfaceWarmElevated;
  Color get surfaceWarmAccent =>
      isDark ? PayaboDarkColors.surfaceWarmAccent : PayaboColors.surfaceWarmAccent;
  Color get surfaceCard =>
      isDark ? PayaboDarkColors.surfaceCard : PayaboColors.surfaceBase;
  Color get surfaceCardElevated =>
      isDark ? PayaboDarkColors.surfaceCardElevated : PayaboColors.surfaceBase;

  // ── Borders ────────────────────────────────────────────
  Color get borderDefault =>
      isDark ? PayaboDarkColors.borderDefault : PayaboColors.borderDefault;
  Color get borderStrong =>
      isDark ? PayaboDarkColors.borderStrong : PayaboColors.borderStrong;
  Color get borderWarm =>
      isDark ? PayaboDarkColors.borderWarm : PayaboColors.borderWarm;

  // ── Status ─────────────────────────────────────────────
  Color get success =>
      isDark ? PayaboDarkColors.success : PayaboColors.success;
  Color get successSoft =>
      isDark ? PayaboDarkColors.successSoft : PayaboColors.successSoft;
  Color get warning =>
      isDark ? PayaboDarkColors.warning : PayaboColors.warning;
  Color get danger =>
      isDark ? PayaboDarkColors.danger : PayaboColors.danger;
  Color get info => isDark ? PayaboDarkColors.info : PayaboColors.info;

  // ── Header ─────────────────────────────────────────────
  Color get headerTitle =>
      isDark ? PayaboDarkColors.headerTitle : PayaboColors.headerTitle;
  Color get headerSubtitle =>
      isDark ? PayaboDarkColors.headerSubtitle : PayaboColors.headerSubtitle;
  Color get headerIconSurface =>
      isDark ? PayaboDarkColors.headerIconSurface : PayaboColors.headerIconSurface;
  Color get headerIconSurfaceAccent => isDark
      ? PayaboDarkColors.headerIconSurfaceAccent
      : PayaboColors.headerIconSurfaceAccent;
  Color get headerIconBorder =>
      isDark ? PayaboDarkColors.headerIconBorder : PayaboColors.headerIconBorder;
  Color get headerIconAccent =>
      isDark ? PayaboDarkColors.headerIconAccent : PayaboColors.headerIconAccent;
  Color get headerNotificationDot => isDark
      ? PayaboDarkColors.headerNotificationDot
      : PayaboColors.headerNotificationDot;

  // ── Navigation ─────────────────────────────────────────
  Color get navBackground =>
      isDark ? PayaboDarkColors.navBackground : PayaboColors.navBackground;
  Color get navBorder =>
      isDark ? PayaboDarkColors.navBorder : PayaboColors.navBorder;
  Color get navSelected =>
      isDark ? PayaboDarkColors.navSelected : PayaboColors.navSelected;
  Color get navUnselected =>
      isDark ? PayaboDarkColors.navUnselected : PayaboColors.navUnselected;
  Color get navFabBackground =>
      isDark ? PayaboDarkColors.navFabBackground : PayaboColors.navFabBackground;
  Color get navShadow =>
      isDark ? PayaboDarkColors.navShadow : PayaboColors.navShadow;
  Color get navFabShadow =>
      isDark ? PayaboDarkColors.navFabShadow : PayaboColors.navFabShadow;

  // ── Spending ───────────────────────────────────────────
  List<Color> get spendingAccountGradientPrimary => isDark
      ? PayaboDarkColors.spendingAccountGradientPrimary
      : PayaboColors.spendingAccountGradientPrimary;
  List<Color> get spendingAccountGradientSavings => isDark
      ? PayaboDarkColors.spendingAccountGradientSavings
      : PayaboColors.spendingAccountGradientSavings;
  List<Color> get spendingAccountGradientBills => isDark
      ? PayaboDarkColors.spendingAccountGradientBills
      : PayaboColors.spendingAccountGradientBills;
  Color get spendingAccountAccentPrimary => isDark
      ? PayaboDarkColors.spendingAccountAccentPrimary
      : PayaboColors.spendingAccountAccentPrimary;
  Color get spendingAccountAccentSavings => isDark
      ? PayaboDarkColors.spendingAccountAccentSavings
      : PayaboColors.spendingAccountAccentSavings;
  Color get spendingAccountAccentBills => isDark
      ? PayaboDarkColors.spendingAccountAccentBills
      : PayaboColors.spendingAccountAccentBills;
  Color get spendingSliceBills =>
      isDark ? PayaboDarkColors.spendingSliceBills : PayaboColors.spendingSliceBills;
  Color get spendingSliceOther =>
      isDark ? PayaboDarkColors.spendingSliceOther : PayaboColors.spendingSliceOther;
  Color get spendingMerchantIconDark => isDark
      ? PayaboDarkColors.spendingMerchantIconDark
      : PayaboColors.spendingMerchantIconDark;
  Color get spendingMerchantIconWarmSurface => isDark
      ? PayaboDarkColors.spendingMerchantIconWarmSurface
      : PayaboColors.spendingMerchantIconWarmSurface;
  Color get spendingMerchantIconWarmAccent => isDark
      ? PayaboDarkColors.spendingMerchantIconWarmAccent
      : PayaboColors.spendingMerchantIconWarmAccent;
  Color get spendingMerchantIconWarmText => isDark
      ? PayaboDarkColors.spendingMerchantIconWarmText
      : PayaboColors.spendingMerchantIconWarmText;
  Color get spendingDotInactive =>
      isDark ? PayaboDarkColors.spendingDotInactive : PayaboColors.spendingDotInactive;
  Color get spendingQuickActionSurface => isDark
      ? PayaboDarkColors.spendingQuickActionSurface
      : PayaboColors.spendingQuickActionSurface;
  Color get spendingQuickActionBorder => isDark
      ? PayaboDarkColors.spendingQuickActionBorder
      : PayaboColors.spendingQuickActionBorder;
  Color get spendingTrendGrid =>
      isDark ? PayaboDarkColors.spendingTrendGrid : PayaboColors.spendingTrendGrid;
  Color get spendingCardWarm =>
      isDark ? PayaboDarkColors.spendingCardWarm : PayaboColors.spendingCardWarm;
  Color get spendingCardWarmElevated => isDark
      ? PayaboDarkColors.spendingCardWarmElevated
      : PayaboColors.spendingCardWarmElevated;
  Color get spendingInsightLabel =>
      isDark ? PayaboDarkColors.spendingInsightLabel : PayaboColors.spendingInsightLabel;
  Color get spendingInsightBorder =>
      isDark ? PayaboDarkColors.spendingInsightBorder : PayaboColors.spendingInsightBorder;

  // ── Chat ───────────────────────────────────────────────
  Color get chatScreenSurface =>
      isDark ? PayaboDarkColors.chatScreenSurface : PayaboColors.chatScreenSurface;
  Color get chatGlowPrimary =>
      isDark ? PayaboDarkColors.chatGlowPrimary : PayaboColors.chatGlowPrimary;
  Color get chatGlowSecondary =>
      isDark ? PayaboDarkColors.chatGlowSecondary : PayaboColors.chatGlowSecondary;
  Color get chatTextPrimary =>
      isDark ? PayaboDarkColors.chatTextPrimary : PayaboColors.chatTextPrimary;
  Color get chatTextSecondary =>
      isDark ? PayaboDarkColors.chatTextSecondary : PayaboColors.chatTextSecondary;
  Color get chatTextTertiary =>
      isDark ? PayaboDarkColors.chatTextTertiary : PayaboColors.chatTextTertiary;
  Color get chatPlanBorder =>
      isDark ? PayaboDarkColors.chatPlanBorder : PayaboColors.chatPlanBorder;
  Color get chatPlanIconSurface =>
      isDark ? PayaboDarkColors.chatPlanIconSurface : PayaboColors.chatPlanIconSurface;
  Color get chatComposerSurface =>
      isDark ? PayaboDarkColors.chatComposerSurface : PayaboColors.chatComposerSurface;
  Color get chatComposerHandle =>
      isDark ? PayaboDarkColors.chatComposerHandle : PayaboColors.chatComposerHandle;
  Color get chatInputBorder =>
      isDark ? PayaboDarkColors.chatInputBorder : PayaboColors.chatInputBorder;
  Color get chatSendActive =>
      isDark ? PayaboDarkColors.chatSendActive : PayaboColors.chatSendActive;

  // ── Gradients ──────────────────────────────────────────
  LinearGradient get warmScreenGradient =>
      isDark ? PayaboGradients.darkScreen : PayaboGradients.warmScreen;
  LinearGradient get chatScreenGradient =>
      isDark ? PayaboGradients.darkChatScreen : PayaboGradients.chatScreen;
  LinearGradient get spendingInsightGradient =>
      isDark ? PayaboGradients.darkSpendingInsight : PayaboGradients.spendingInsight;
  LinearGradient get spendingSafeToSpendGradient => isDark
      ? PayaboGradients.darkSpendingSafeToSpend
      : PayaboGradients.spendingSafeToSpend;

  // ── Backwards-compatible aliases ───────────────────────
  Color get ink => textPrimary;
  Color get muted => textMuted;
  Color get accentBrown => headerTitle;
  Color get accentBrownMuted => headerSubtitle;
  Color get white => surfaceBase;
  Color get background => surfaceMuted;
  Color get backgroundSoft => surfaceSubtle;
  Color get border => borderDefault;
}

/// Extension on [BuildContext] for quick access to semantic colors.
extension PayaboColorResolverX on BuildContext {
  /// Returns the correct [PayaboColorResolver] for the current brightness.
  PayaboColorResolver get colors => PayaboColorResolver.of(this);
}
