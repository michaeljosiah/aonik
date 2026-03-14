import 'package:flutter/material.dart';

abstract final class PayaboPalette {
  // ── Brand ──────────────────────────────────────────────
  static const Color orange500 = Color(0xFFF37920);
  static const Color orange600 = Color(0xFFD55F0B);

  // ── Ink / Neutral ──────────────────────────────────────
  static const Color ink900 = Color(0xFF1A1C20);
  static const Color neutral500 = Color(0xFFB4BFC3);

  static const Color white = Color(0xFFFFFFFF);
  static const Color neutral050 = Color(0xFFF7F8FA);
  static const Color neutral100 = Color(0xFFF2F4F4);
  static const Color neutral200 = Color(0xFFE5E9EA);
  static const Color neutralShadow = Color(0x66B4BFC3);

  // ── Warm (Light theme) ─────────────────────────────────
  static const Color warm050 = Color(0xFFFFFCF9);
  static const Color warm100 = Color(0xFFFFFBF7);
  static const Color warm150 = Color(0xFFF7EEE4);
  static const Color warm200 = Color(0xFFF4ECDE);
  static const Color warm300 = Color(0xFFDCCDB7);
  static const Color warm500 = Color(0xFFD7A14E);
  static const Color warm600 = Color(0xFF9B7A43);
  static const Color warm800 = Color(0xFF77594A);
  static const Color warm900 = Color(0xFF4D3120);

  // ── Status ─────────────────────────────────────────────
  static const Color success500 = Color(0xFF4ACB64);
  static const Color success050 = Color(0xFFECFAEF);
  static const Color warning500 = Color(0xFFFF9E15);
  static const Color danger500 = Color(0xFFE60037);
  static const Color info500 = Color(0xFF2465E8);

  static const Color black12 = Color(0x12000000);
  static const Color black16 = Color(0x26000000);

  // ── Navigation (Light) ─────────────────────────────────
  static const Color navBorder = Color(0xFFF0E7DA);
  static const Color navSelected = Color(0xFFC29752);
  static const Color navUnselected = Color(0xFF99958F);

  // ── Dark palette ───────────────────────────────────────
  static const Color dark950 = Color(0xFF0D1117);
  static const Color dark900 = Color(0xFF121820);
  static const Color dark800 = Color(0xFF161B22);
  static const Color dark700 = Color(0xFF1C2128);
  static const Color dark600 = Color(0xFF21262D);
  static const Color dark500 = Color(0xFF30363D);
  static const Color dark400 = Color(0xFF484F58);
  static const Color dark300 = Color(0xFF6E7681);
  static const Color dark200 = Color(0xFF8B949E);
  static const Color dark100 = Color(0xFFC9D1D9);
  static const Color darkWhite = Color(0xFFE6EDF3);

  static const Color darkNavBorder = Color(0xFF21262D);
  static const Color darkNavSelected = Color(0xFFF37920);
  static const Color darkNavUnselected = Color(0xFF6E7681);

  static const Color transparent = Colors.transparent;
}
