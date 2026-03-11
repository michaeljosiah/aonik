import 'package:flutter/material.dart';

import '../theme/payabo_colors.dart';
import '../theme/payabo_spacing.dart';
import '../validation/payabo_input_validators.dart';

class PayaboPasswordRequirements extends StatelessWidget {
  const PayaboPasswordRequirements({
    super.key,
    required this.password,
    this.disabled = false,
    this.titleStyle,
  });

  final String password;
  final bool disabled;
  final TextStyle? titleStyle;

  @override
  Widget build(BuildContext context) {
    final PayaboPasswordValidation validation =
        validatePayaboPassword(password);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Text(
          'Your password must contain at least:',
          style: titleStyle ?? Theme.of(context).textTheme.bodyLarge,
        ),
        const SizedBox(height: PayaboSpacing.sm),
        _PasswordRequirementLine(
          label: '8 characters',
          met: validation.hasMinLength,
          disabled: disabled,
        ),
        _PasswordRequirementLine(
          label: '1 lowercase letter',
          met: validation.hasLowercase,
          disabled: disabled,
        ),
        _PasswordRequirementLine(
          label: '1 uppercase letter',
          met: validation.hasUppercase,
          disabled: disabled,
        ),
        _PasswordRequirementLine(
          label: '1 number',
          met: validation.hasDigit,
          disabled: disabled,
        ),
      ],
    );
  }
}

class _PasswordRequirementLine extends StatelessWidget {
  const _PasswordRequirementLine({
    required this.label,
    required this.met,
    required this.disabled,
  });

  final String label;
  final bool met;
  final bool disabled;

  @override
  Widget build(BuildContext context) {
    final Color color = disabled
        ? PayaboColors.muted
        : met
            ? PayaboColors.success
            : PayaboColors.muted;

    return Padding(
      padding: const EdgeInsets.only(bottom: 6),
      child: Row(
        children: <Widget>[
          Icon(
            met ? Icons.check_circle : Icons.radio_button_unchecked,
            size: 16,
            color: color,
          ),
          const SizedBox(width: PayaboSpacing.sm),
          Text(
            label,
            style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                  color: color,
                ),
          ),
        ],
      ),
    );
  }
}
