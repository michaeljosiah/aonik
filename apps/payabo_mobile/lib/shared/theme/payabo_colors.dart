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
