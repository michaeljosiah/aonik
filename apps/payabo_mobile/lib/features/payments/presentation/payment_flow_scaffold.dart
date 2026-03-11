import 'package:flutter/material.dart';

import '../../../shared/theme/payabo_colors.dart';
import '../../../shared/theme/payabo_gradients.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_app_header.dart';

class PaymentFlowScaffold extends StatelessWidget {
  const PaymentFlowScaffold({
    super.key,
    required this.title,
    required this.child,
    this.onBack,
    this.onClose,
    this.footer,
  });

  final String title;
  final Widget child;
  final VoidCallback? onBack;
  final VoidCallback? onClose;
  final Widget? footer;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: PayaboColors.surfaceWarm,
      body: DecoratedBox(
        decoration: const BoxDecoration(
          gradient: PayaboGradients.warmScreen,
        ),
        child: SafeArea(
          child: Column(
            children: <Widget>[
              const PayaboAppHeader(),
              Container(
                padding: const EdgeInsets.fromLTRB(
                  PayaboSpacing.xl,
                  0,
                  PayaboSpacing.xl,
                  PayaboSpacing.lg,
                ),
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
                        style:
                            Theme.of(context).textTheme.titleMedium?.copyWith(
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
              ),
              Expanded(
                child: SingleChildScrollView(
                  padding: const EdgeInsets.fromLTRB(
                    PayaboSpacing.xl,
                    PayaboSpacing.lg,
                    PayaboSpacing.xl,
                    PayaboSpacing.xl,
                  ),
                  child: child,
                ),
              ),
              if (footer != null)
                Padding(
                  padding: const EdgeInsets.fromLTRB(
                    PayaboSpacing.xl,
                    0,
                    PayaboSpacing.xl,
                    PayaboSpacing.lg,
                  ),
                  child: footer!,
                ),
            ],
          ),
        ),
      ),
    );
  }
}
