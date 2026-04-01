import 'package:flutter/material.dart';
import 'package:flutter_svg/flutter_svg.dart';

import '../reference/payabo_country_reference.dart';

class PayaboCountryFlag extends StatelessWidget {
  const PayaboCountryFlag({
    super.key,
    required this.country,
    this.width = 32,
    this.height = 24,
    this.fontSize = 20,
  });

  final PayaboCountryReference country;
  final double width;
  final double height;
  final double fontSize;

  @override
  Widget build(BuildContext context) {
    if (country.flagAsset != null) {
      return SvgPicture.asset(
        country.flagAsset!,
        width: width,
        height: height,
      );
    }

    return SizedBox(
      width: width,
      height: height,
      child: Center(
        child: Text(
          country.flagEmoji,
          style: TextStyle(fontSize: fontSize),
        ),
      ),
    );
  }
}
