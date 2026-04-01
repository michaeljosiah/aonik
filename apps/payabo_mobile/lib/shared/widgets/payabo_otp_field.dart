import 'package:flutter/material.dart';
import 'package:pinput/pinput.dart';

import '../theme/payabo_color_resolver.dart';

class PayaboOtpField extends StatelessWidget {
  const PayaboOtpField({
    super.key,
    this.length = 4,
    this.enabled = true,
    this.controller,
    this.onChanged,
    this.onCompleted,
  });

  final int length;
  final bool enabled;
  final TextEditingController? controller;
  final ValueChanged<String>? onChanged;
  final ValueChanged<String>? onCompleted;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    final defaultPinTheme = PinTheme(
      width: 44,
      height: 54,
      textStyle: Theme.of(context).textTheme.titleLarge?.copyWith(
            fontWeight: FontWeight.w700,
            color: enabled ? c.ink : c.muted,
          ),
      decoration: BoxDecoration(
        border: Border(
          bottom: BorderSide(color: c.border, width: 1),
        ),
      ),
    );

    return Pinput(
      enabled: enabled,
      length: length,
      controller: controller,
      defaultPinTheme: defaultPinTheme,
      focusedPinTheme: defaultPinTheme.copyWith(
        decoration: BoxDecoration(
          border: Border(
            bottom: BorderSide(color: c.primary, width: 1),
          ),
        ),
      ),
      disabledPinTheme: defaultPinTheme.copyWith(
        decoration: BoxDecoration(
          border: Border(
            bottom: BorderSide(color: c.border, width: 1),
          ),
        ),
      ),
      onChanged: onChanged,
      onCompleted: onCompleted,
    );
  }
}
