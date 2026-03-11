import 'package:flutter/material.dart';

import '../theme/payabo_colors.dart';
import '../theme/payabo_gradients.dart';

class PayaboWarmScaffold extends StatelessWidget {
  const PayaboWarmScaffold({
    super.key,
    required this.body,
    this.bottomNavigationBar,
  });

  final Widget body;
  final Widget? bottomNavigationBar;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: PayaboColors.surfaceWarm,
      bottomNavigationBar: bottomNavigationBar,
      body: DecoratedBox(
        decoration: const BoxDecoration(
          gradient: PayaboGradients.warmScreen,
        ),
        child: SafeArea(
          child: body,
        ),
      ),
    );
  }
}
