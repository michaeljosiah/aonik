import 'package:flutter/widgets.dart';

abstract final class PayaboRadii {
  static const double none = 0;
  static const double sm = 4;
  static const double md = 5;
  static const double lg = 12;
  static const double xl = 20;
  static const double pill = 50;

  static const BorderRadius radiusSm = BorderRadius.all(Radius.circular(sm));
  static const BorderRadius radiusLg = BorderRadius.all(Radius.circular(lg));
  static const BorderRadius radiusPill =
      BorderRadius.all(Radius.circular(pill));
  static const BorderRadius sheetTop = BorderRadius.only(
    topLeft: Radius.circular(xl),
    topRight: Radius.circular(xl),
  );
}
