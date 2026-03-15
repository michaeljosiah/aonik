import 'package:flutter/material.dart';

import '../theme/payabo_color_resolver.dart';

class PayaboWarmScaffold extends StatelessWidget {
  const PayaboWarmScaffold({
    super.key,
    required this.body,
    this.bottomNavigationBar,
    this.backgroundDecoration,
  });

  final Widget body;
  final Widget? bottomNavigationBar;

  /// Override the default warm-screen gradient with a custom decoration.
  /// The decoration is painted behind the status bar so the colour is
  /// consistent all the way to the top of the screen.
  final BoxDecoration? backgroundDecoration;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Scaffold(
      backgroundColor: c.surfaceWarm,
      bottomNavigationBar: bottomNavigationBar,
      body: DecoratedBox(
        decoration:
            backgroundDecoration ?? BoxDecoration(gradient: c.warmScreenGradient),
        child: SafeArea(
          child: body,
        ),
      ),
    );
  }
}
