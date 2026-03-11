import 'package:flutter/material.dart';

import 'payabo_palette.dart';

abstract final class PayaboGradients {
  static const LinearGradient warmScreen = LinearGradient(
    colors: <Color>[
      PayaboPalette.warm050,
      PayaboPalette.warm150,
    ],
    begin: Alignment.topCenter,
    end: Alignment.bottomCenter,
  );
}
