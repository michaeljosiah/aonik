import 'package:flutter/material.dart';

import '../theme/payabo_borders.dart';
import '../theme/payabo_colors.dart';
import '../theme/payabo_radii.dart';
import '../theme/payabo_spacing.dart';

enum PayaboButtonVariant {
  primary,
  secondary,
  link,
}

enum PayaboButtonSize {
  sm,
  md,
  lg,
}

class PayaboButton extends StatelessWidget {
  const PayaboButton({
    super.key,
    required this.label,
    this.variant = PayaboButtonVariant.primary,
    this.size = PayaboButtonSize.md,
    this.expand = true,
    this.onPressed,
    this.leading,
  });

  final String label;
  final PayaboButtonVariant variant;
  final PayaboButtonSize size;
  final bool expand;
  final VoidCallback? onPressed;
  final Widget? leading;

  @override
  Widget build(BuildContext context) {
    final Widget child = _ButtonLabel(
      label: label,
      leading: leading,
    );

    final double minHeight = _resolveHeight(size);

    if (variant == PayaboButtonVariant.secondary) {
      return SizedBox(
        width: expand ? double.infinity : null,
        height: minHeight,
        child: OutlinedButton(
          onPressed: onPressed,
          style: _secondaryStyle(context, size),
          child: child,
        ),
      );
    }

    if (variant == PayaboButtonVariant.link) {
      return SizedBox(
        width: expand ? double.infinity : null,
        height: minHeight,
        child: OutlinedButton(
          onPressed: onPressed,
          style: _linkStyle(context, size),
          child: child,
        ),
      );
    }

    return SizedBox(
      width: expand ? double.infinity : null,
      height: minHeight,
      child: ElevatedButton(
        onPressed: onPressed,
        style: _primaryStyle(context, size),
        child: child,
      ),
    );
  }

  double _resolveHeight(PayaboButtonSize size) {
    switch (size) {
      case PayaboButtonSize.sm:
        return 40;
      case PayaboButtonSize.md:
        return 48;
      case PayaboButtonSize.lg:
        return 52;
    }
  }

  EdgeInsets _resolvePadding(PayaboButtonSize size) {
    switch (size) {
      case PayaboButtonSize.sm:
        return const EdgeInsets.symmetric(
            horizontal: PayaboSpacing.md, vertical: PayaboSpacing.sm);
      case PayaboButtonSize.md:
        return const EdgeInsets.symmetric(
            horizontal: PayaboSpacing.lg, vertical: PayaboSpacing.md);
      case PayaboButtonSize.lg:
        return const EdgeInsets.symmetric(
            horizontal: PayaboSpacing.lg, vertical: 13);
    }
  }

  ButtonStyle _primaryStyle(BuildContext context, PayaboButtonSize size) {
    return ElevatedButton.styleFrom(
      foregroundColor: PayaboColors.white,
      backgroundColor: PayaboColors.primary,
      disabledForegroundColor: PayaboColors.muted,
      disabledBackgroundColor: PayaboColors.background,
      shape: const RoundedRectangleBorder(
        borderRadius: PayaboRadii.radiusSm,
        side: PayaboBorders.buttonBorder,
      ),
      elevation: 0,
      shadowColor: Colors.transparent,
      textStyle: Theme.of(context).textTheme.labelLarge,
      padding: _resolvePadding(size),
    ).copyWith(
      backgroundColor: WidgetStateProperty.resolveWith<Color>((states) {
        if (states.contains(WidgetState.disabled)) {
          return PayaboColors.background;
        }

        if (states.contains(WidgetState.pressed) ||
            states.contains(WidgetState.hovered)) {
          return PayaboColors.primaryHover;
        }

        return PayaboColors.primary;
      }),
      side: WidgetStateProperty.resolveWith<BorderSide>((states) {
        if (states.contains(WidgetState.disabled)) {
          return const BorderSide(color: PayaboColors.background, width: 2);
        }

        if (states.contains(WidgetState.pressed) ||
            states.contains(WidgetState.hovered)) {
          return const BorderSide(color: PayaboColors.primaryHover, width: 2);
        }

        return PayaboBorders.buttonBorder;
      }),
    );
  }

  ButtonStyle _secondaryStyle(BuildContext context, PayaboButtonSize size) {
    return OutlinedButton.styleFrom(
      foregroundColor: PayaboColors.primary,
      disabledForegroundColor: PayaboColors.muted,
      shape: const RoundedRectangleBorder(borderRadius: PayaboRadii.radiusSm),
      side: PayaboBorders.buttonBorder,
      textStyle: Theme.of(context).textTheme.labelLarge,
      padding: _resolvePadding(size),
    ).copyWith(
      foregroundColor: WidgetStateProperty.resolveWith<Color>((states) {
        if (states.contains(WidgetState.disabled)) {
          return PayaboColors.muted;
        }

        if (states.contains(WidgetState.pressed) ||
            states.contains(WidgetState.hovered)) {
          return PayaboColors.primaryHover;
        }

        return PayaboColors.primary;
      }),
      side: WidgetStateProperty.resolveWith<BorderSide>((states) {
        if (states.contains(WidgetState.disabled)) {
          return const BorderSide(color: PayaboColors.muted, width: 2);
        }

        if (states.contains(WidgetState.pressed) ||
            states.contains(WidgetState.hovered)) {
          return const BorderSide(color: PayaboColors.primaryHover, width: 2);
        }

        return PayaboBorders.buttonBorder;
      }),
      overlayColor:
          const WidgetStatePropertyAll<Color>(PayaboColors.transparent),
    );
  }

  ButtonStyle _linkStyle(BuildContext context, PayaboButtonSize size) {
    return OutlinedButton.styleFrom(
      foregroundColor: PayaboColors.primary,
      backgroundColor: PayaboColors.white,
      disabledForegroundColor: PayaboColors.muted,
      disabledBackgroundColor: PayaboColors.white,
      shape: const RoundedRectangleBorder(borderRadius: PayaboRadii.radiusSm),
      side: const BorderSide(color: PayaboColors.primary, width: 1),
      textStyle: Theme.of(context).textTheme.bodyLarge,
      padding: _resolvePadding(size),
    ).copyWith(
      foregroundColor: WidgetStateProperty.resolveWith<Color>((states) {
        if (states.contains(WidgetState.disabled)) {
          return PayaboColors.muted;
        }

        if (states.contains(WidgetState.pressed) ||
            states.contains(WidgetState.hovered)) {
          return PayaboColors.primaryHover;
        }

        return PayaboColors.primary;
      }),
      side: WidgetStateProperty.resolveWith<BorderSide>((states) {
        if (states.contains(WidgetState.disabled)) {
          return const BorderSide(color: PayaboColors.muted, width: 1);
        }

        if (states.contains(WidgetState.pressed) ||
            states.contains(WidgetState.hovered)) {
          return const BorderSide(color: PayaboColors.primaryHover, width: 1);
        }

        return const BorderSide(color: PayaboColors.primary, width: 1);
      }),
      overlayColor:
          const WidgetStatePropertyAll<Color>(PayaboColors.transparent),
    );
  }
}

class _ButtonLabel extends StatelessWidget {
  const _ButtonLabel({
    required this.label,
    this.leading,
  });

  final String label;
  final Widget? leading;

  @override
  Widget build(BuildContext context) {
    if (leading == null) {
      return Text(label.toUpperCase());
    }

    return Row(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        leading!,
        const SizedBox(width: PayaboSpacing.sm),
        Text(label.toUpperCase()),
      ],
    );
  }
}
