import 'package:flutter/material.dart';

import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_app_header.dart';
import '../../../shared/widgets/payabo_primary_app_shell.dart';
import '../../../shared/widgets/payabo_screen_title_bar.dart';
import '../../../shared/widgets/payabo_warm_scaffold.dart';

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
    return PayaboWarmScaffold(
      bottomNavigationBar: const PayaboPrimaryAppShell(
        destination: PayaboPrimaryDestination.pay,
      ),
      body: Column(
        children: <Widget>[
          const PayaboAppHeader(),
          PayaboScreenTitleBar(
            title: title,
            onBack: onBack,
            onClose: onClose,
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
    );
  }
}
