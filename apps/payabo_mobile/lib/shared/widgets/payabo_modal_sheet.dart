import 'package:flutter/material.dart';

import '../theme/payabo_colors.dart';
import '../theme/payabo_radii.dart';
import '../theme/payabo_spacing.dart';

Future<T?> showPayaboModalSheet<T>({
  required BuildContext context,
  required Widget child,
  String? title,
}) {
  return showModalBottomSheet<T>(
    context: context,
    isScrollControlled: true,
    backgroundColor: PayaboColors.transparent,
    builder: (context) {
      return PayaboModalSheet(
        title: title,
        child: child,
      );
    },
  );
}

class PayaboModalSheet extends StatelessWidget {
  const PayaboModalSheet({
    super.key,
    required this.child,
    this.title,
  });

  final String? title;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    final bottomInset = MediaQuery.viewInsetsOf(context).bottom;

    return Padding(
      padding: EdgeInsets.only(bottom: bottomInset),
      child: DecoratedBox(
        decoration: const BoxDecoration(
          color: PayaboColors.white,
          borderRadius: PayaboRadii.sheetTop,
        ),
        child: SafeArea(
          top: false,
          child: Padding(
            padding: const EdgeInsets.fromLTRB(PayaboSpacing.xl,
                PayaboSpacing.lg, PayaboSpacing.xl, PayaboSpacing.xl),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                if (title != null) ...<Widget>[
                  Row(
                    children: <Widget>[
                      Expanded(
                        child: Text(
                          title!,
                          style: Theme.of(context).textTheme.titleLarge,
                        ),
                      ),
                      IconButton(
                        onPressed: () => Navigator.of(context).pop(),
                        icon: const Icon(Icons.close,
                            color: PayaboColors.primary),
                      ),
                    ],
                  ),
                  const SizedBox(height: PayaboSpacing.sm),
                ],
                child,
              ],
            ),
          ),
        ),
      ),
    );
  }
}
