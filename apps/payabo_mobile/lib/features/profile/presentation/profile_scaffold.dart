import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_primary_app_shell.dart';
import '../../../shared/widgets/payabo_screen_title_bar.dart';
import '../../../shared/widgets/payabo_warm_scaffold.dart';

class ProfileScaffold extends StatelessWidget {
  const ProfileScaffold({
    super.key,
    required this.title,
    required this.child,
    this.backRoute,
    this.footer,
  });

  final String title;
  final Widget child;
  final String? backRoute;
  final Widget? footer;

  @override
  Widget build(BuildContext context) {
    return PayaboWarmScaffold(
      body: Column(
        children: <Widget>[
          PayaboScreenTitleBar(
            title: title,
            onBack: backRoute == null ? null : () => context.go(backRoute!),
            padding: const EdgeInsets.fromLTRB(
              PayaboSpacing.xl,
              0,
              PayaboSpacing.xl,
              PayaboSpacing.md,
            ),
          ),
          Expanded(
            child: SingleChildScrollView(
              padding: const EdgeInsets.fromLTRB(
                PayaboSpacing.xl,
                0,
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
      bottomNavigationBar: const PayaboPrimaryAppShell(
        destination: PayaboPrimaryDestination.none,
      ),
    );
  }
}
