import 'package:flutter/material.dart';

import 'payabo_colors.dart';

abstract final class PayaboBorders {
  static const BorderSide defaultBorder =
      BorderSide(color: PayaboColors.border, width: 1);
  static const BorderSide activeBorder =
      BorderSide(color: PayaboColors.primary, width: 1);
  static const BorderSide errorBorder =
      BorderSide(color: PayaboColors.danger, width: 1);
  static const BorderSide strongBorder =
      BorderSide(color: PayaboColors.borderStrong, width: 1);
  static const BorderSide buttonBorder =
      BorderSide(color: PayaboColors.primary, width: 2);
}
