import 'package:flutter/widgets.dart';

abstract final class PayaboSpacing {
  static const double xxs = 2;
  static const double xs = 4;
  static const double sm = 8;
  static const double md = 12;
  static const double lg = 16;
  static const double xl = 20;
  static const double x2 = 24;
  static const double x3 = 30;
  static const double x4 = 40;

  static const EdgeInsets page =
      EdgeInsets.symmetric(horizontal: xl, vertical: lg);
  static const EdgeInsets card = EdgeInsets.all(xl);
  static const EdgeInsets cardLarge = EdgeInsets.all(x3);
  static const EdgeInsets listItem =
      EdgeInsets.symmetric(horizontal: xl, vertical: lg);
}
