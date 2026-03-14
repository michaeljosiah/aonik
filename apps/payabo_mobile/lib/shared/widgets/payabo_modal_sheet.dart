import 'package:flutter/material.dart';

import '../theme/payabo_color_resolver.dart';
import '../theme/payabo_radii.dart';
import '../theme/payabo_spacing.dart';

Future<T?> showPayaboModalSheet<T>({
  required BuildContext context,
  required Widget child,
  String? title,
  bool isDismissible = true,
  bool enableDrag = true,
}) {
  return showModalBottomSheet<T>(
    context: context,
    isScrollControlled: true,
    isDismissible: isDismissible,
    enableDrag: enableDrag,
    backgroundColor: Colors.transparent,
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
    final c = context.colors;
    final bottomInset = MediaQuery.viewInsetsOf(context).bottom;

    return Padding(
      padding: EdgeInsets.only(bottom: bottomInset),
      child: DecoratedBox(
        decoration: BoxDecoration(
          color: c.surfaceBase,
          borderRadius: PayaboRadii.sheetTop,
          border: Border.all(color: c.borderStrong),
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
                          style: Theme.of(context).textTheme.titleLarge?.copyWith(
                                color: c.textPrimary,
                              ),
                        ),
                      ),
                      IconButton(
                        onPressed: () => Navigator.of(context).pop(),
                        icon: Icon(Icons.close, color: c.primary),
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
