import 'package:flutter/material.dart';

import '../theme/payabo_colors.dart';
import '../theme/payabo_spacing.dart';

class PayaboScreenTitleBar extends StatelessWidget {
  const PayaboScreenTitleBar({
    super.key,
    required this.title,
    this.onBack,
    this.onClose,
    this.padding = const EdgeInsets.fromLTRB(
      PayaboSpacing.xl,
      0,
      PayaboSpacing.xl,
      PayaboSpacing.lg,
    ),
  });

  final String title;
  final VoidCallback? onBack;
  final VoidCallback? onClose;
  final EdgeInsets padding;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: padding,
      child: Row(
        children: <Widget>[
          SizedBox(
            width: 32,
            child: onBack == null
                ? const SizedBox.shrink()
                : InkWell(
                    onTap: onBack,
                    borderRadius: BorderRadius.circular(20),
                    child: const Icon(
                      Icons.arrow_back_ios_new,
                      size: 18,
                      color: PayaboColors.primary,
                    ),
                  ),
          ),
          Expanded(
            child: Text(
              title,
              textAlign: TextAlign.center,
              style: Theme.of(context).textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.w700,
                    color: PayaboColors.headerTitle,
                  ),
            ),
          ),
          SizedBox(
            width: 32,
            child: onClose == null
                ? const SizedBox.shrink()
                : InkWell(
                    onTap: onClose,
                    borderRadius: BorderRadius.circular(20),
                    child: const Icon(
                      Icons.close,
                      size: 22,
                      color: PayaboColors.primary,
                    ),
                  ),
          ),
        ],
      ),
    );
  }
}
