import 'package:flutter/material.dart';

import '../theme/payabo_color_resolver.dart';

class PayaboWarmScaffold extends StatelessWidget {
  const PayaboWarmScaffold({
    super.key,
    required this.body,
    this.bottomNavigationBar,
    this.backgroundDecoration,
    this.statusBarColorNotifier,
    this.floatingActionButton,
  });

  final Widget body;
  final Widget? bottomNavigationBar;

  /// Override the default warm-screen gradient with a custom decoration.
  /// The decoration is painted behind the status bar so the colour is
  /// consistent all the way to the top of the screen.
  final BoxDecoration? backgroundDecoration;

  /// When provided, a [ColoredBox] overlay is rendered on top of the
  /// background decoration, covering exactly the status-bar area
  /// (above [SafeArea]).  The notifier value is the opacity (0.0–1.0) of the
  /// overlay, allowing callers to animate the status-bar colour in sync with
  /// scroll / sheet position.
  ///
  /// The overlay colour is [PayaboColorResolver.surfaceBase].
  final ValueNotifier<double>? statusBarColorNotifier;

  /// Optional floating action button passed through to the inner [Scaffold].
  final Widget? floatingActionButton;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    // viewPaddingOf must be called here, BEFORE SafeArea, so that it returns
    // the real status-bar inset rather than zero.
    final double statusBarHeight = MediaQuery.viewPaddingOf(context).top;

    Widget content = SafeArea(child: body);

    if (statusBarColorNotifier != null) {
      content = Stack(
        children: <Widget>[
          // Body sits behind the overlay.
          Positioned.fill(child: SafeArea(child: body)),
          // Status-bar overlay, above SafeArea, ignores all pointer events.
          Positioned(
            top: 0,
            left: 0,
            right: 0,
            height: statusBarHeight,
            child: IgnorePointer(
              child: ValueListenableBuilder<double>(
                valueListenable: statusBarColorNotifier!,
                builder: (BuildContext ctx, double opacity, Widget? _) {
                  if (opacity <= 0) return const SizedBox.shrink();
                  return Opacity(
                    opacity: opacity,
                    child: ColoredBox(color: c.surfaceBase),
                  );
                },
              ),
            ),
          ),
        ],
      );
    } else {
      content = SafeArea(child: body);
    }

    return Scaffold(
      backgroundColor: c.surfaceWarm,
      bottomNavigationBar: bottomNavigationBar,
      floatingActionButton: floatingActionButton,
      body: DecoratedBox(
        decoration:
            backgroundDecoration ?? BoxDecoration(gradient: c.warmScreenGradient),
        child: content,
      ),
    );
  }
}
