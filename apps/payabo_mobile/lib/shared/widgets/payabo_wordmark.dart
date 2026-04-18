import 'package:flutter/material.dart';
import 'package:flutter_svg/flutter_svg.dart';

class PayaboWordmark extends StatelessWidget {
  const PayaboWordmark({
    super.key,
    this.width,
    this.fit = BoxFit.contain,
  });

  final double? width;
  final BoxFit fit;

  @override
  Widget build(BuildContext context) {
    final bool isDark = Theme.of(context).brightness == Brightness.dark;
    return SvgPicture.asset(
      isDark
          ? 'assets/images/payabo_wordmark_dark.svg'
          : 'assets/images/payabo_wordmark.svg',
      width: width,
      fit: fit,
      semanticsLabel: 'Payabo',
    );
  }
}
