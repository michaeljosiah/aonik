import 'package:flutter/material.dart';

import '../theme/payabo_color_resolver.dart';

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
    final c = context.colors;

    return Scaffold(
      backgroundColor: c.surfaceWarm,
      bottomNavigationBar: bottomNavigationBar,
      body: DecoratedBox(
        decoration: BoxDecoration(
          gradient: c.warmScreenGradient,
        ),
        child: SafeArea(
          child: body,
        ),
      ),
    );
  }
}
