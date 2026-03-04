import 'package:flutter/material.dart';

import '../theme/payabo_borders.dart';
import '../theme/payabo_colors.dart';

enum PayaboInputVariant {
  boxed,
  floating,
}

class PayaboTextField extends StatelessWidget {
  const PayaboTextField({
    super.key,
    required this.label,
    this.controller,
    this.hintText,
    this.errorText,
    this.keyboardType,
    this.textInputAction,
    this.obscureText = false,
    this.enabled = true,
    this.variant = PayaboInputVariant.boxed,
    this.prefixIcon,
    this.suffixIcon,
    this.onChanged,
  });

  final String label;
  final TextEditingController? controller;
  final String? hintText;
  final String? errorText;
  final TextInputType? keyboardType;
  final TextInputAction? textInputAction;
  final bool obscureText;
  final bool enabled;
  final PayaboInputVariant variant;
  final Widget? prefixIcon;
  final Widget? suffixIcon;
  final ValueChanged<String>? onChanged;

  @override
  Widget build(BuildContext context) {
    return TextField(
      controller: controller,
      keyboardType: keyboardType,
      textInputAction: textInputAction,
      obscureText: obscureText,
      enabled: enabled,
      onChanged: onChanged,
      style: Theme.of(context)
          .textTheme
          .bodyLarge
          ?.copyWith(color: PayaboColors.ink),
      decoration: _buildDecoration(context),
    );
  }

  InputDecoration _buildDecoration(BuildContext context) {
    if (variant == PayaboInputVariant.floating) {
      return InputDecoration(
        labelText: label,
        hintText: hintText,
        errorText: errorText,
        prefixIcon: prefixIcon,
        suffixIcon: suffixIcon,
        filled: false,
        isDense: true,
        contentPadding: const EdgeInsets.symmetric(vertical: 11),
        labelStyle: Theme.of(context).textTheme.bodyLarge?.copyWith(
              color: PayaboColors.muted,
              fontWeight: FontWeight.w600,
            ),
        floatingLabelStyle: Theme.of(context).textTheme.bodyLarge?.copyWith(
              color: PayaboColors.primary,
              fontWeight: FontWeight.w400,
            ),
        border:
            const UnderlineInputBorder(borderSide: PayaboBorders.defaultBorder),
        enabledBorder:
            const UnderlineInputBorder(borderSide: PayaboBorders.defaultBorder),
        focusedBorder:
            const UnderlineInputBorder(borderSide: PayaboBorders.activeBorder),
        errorBorder:
            const UnderlineInputBorder(borderSide: PayaboBorders.errorBorder),
        focusedErrorBorder:
            const UnderlineInputBorder(borderSide: PayaboBorders.errorBorder),
      );
    }

    return InputDecoration(
      labelText: label,
      hintText: hintText,
      errorText: errorText,
      prefixIcon: prefixIcon,
      suffixIcon: suffixIcon,
      floatingLabelBehavior: FloatingLabelBehavior.never,
    );
  }
}
