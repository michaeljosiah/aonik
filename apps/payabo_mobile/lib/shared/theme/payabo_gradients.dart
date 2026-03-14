import 'package:flutter/material.dart';

import 'payabo_palette.dart';

abstract final class PayaboGradients {
  // ── Light theme gradients ──────────────────────────────
  static const LinearGradient warmScreen = LinearGradient(
    colors: <Color>[
      PayaboPalette.warm050,
      PayaboPalette.warm150,
    ],
    begin: Alignment.topCenter,
    end: Alignment.bottomCenter,
  );

  static const LinearGradient chatScreen = LinearGradient(
    colors: <Color>[
      Color(0xFFFBF5EE),
      Color(0xFFF2DEC8),
    ],
    begin: Alignment.topLeft,
    end: Alignment.bottomRight,
  );

  static const LinearGradient spendingSafeToSpend = LinearGradient(
    colors: <Color>[
      Color(0xFF122C1C),
      Color(0xFF285634),
    ],
    begin: Alignment.topLeft,
    end: Alignment.bottomRight,
  );

  static const LinearGradient spendingInsight = LinearGradient(
    colors: <Color>[
      Color(0xFFFFE2C5),
      Color(0xFFFFF2E3),
    ],
    begin: Alignment.topLeft,
    end: Alignment.bottomRight,
  );

  // ── Dark theme gradients ───────────────────────────────
  static const LinearGradient darkScreen = LinearGradient(
    colors: <Color>[
      PayaboPalette.dark950,
      PayaboPalette.dark900,
    ],
    begin: Alignment.topCenter,
    end: Alignment.bottomCenter,
  );

  static const LinearGradient darkChatScreen = LinearGradient(
    colors: <Color>[
      PayaboPalette.dark900,
      PayaboPalette.dark800,
    ],
    begin: Alignment.topLeft,
    end: Alignment.bottomRight,
  );

  static const LinearGradient darkSpendingInsight = LinearGradient(
    colors: <Color>[
      Color(0xFF1E2530),
      Color(0xFF252D38),
    ],
    begin: Alignment.topLeft,
    end: Alignment.bottomRight,
  );

  static const LinearGradient darkSpendingSafeToSpend = LinearGradient(
    colors: <Color>[
      Color(0xFF0F1F17),
      Color(0xFF183226),
    ],
    begin: Alignment.topLeft,
    end: Alignment.bottomRight,
  );
}
