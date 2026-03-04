import 'package:flutter/material.dart';

import 'payabo_colors.dart';

abstract final class PayaboShadows {
  static const List<BoxShadow> soft = <BoxShadow>[
    BoxShadow(
      color: PayaboColors.borderStrong,
      offset: Offset(0, 2),
      blurRadius: 10,
      spreadRadius: 0,
    ),
  ];

  static const List<BoxShadow> medium = <BoxShadow>[
    BoxShadow(
      color: PayaboColors.borderStrong,
      offset: Offset(0, 3),
      blurRadius: 15,
      spreadRadius: 0,
    ),
  ];

  static const List<BoxShadow> strong = <BoxShadow>[
    BoxShadow(
      color: PayaboColors.borderStrong,
      offset: Offset(0, 5),
      blurRadius: 20,
      spreadRadius: 0,
    ),
  ];
}
