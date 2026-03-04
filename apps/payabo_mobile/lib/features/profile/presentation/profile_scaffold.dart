import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../../shared/theme/payabo_colors.dart';
import '../../../shared/theme/payabo_spacing.dart';

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
    return Scaffold(
      backgroundColor: PayaboColors.white,
      body: SafeArea(
        child: Column(
          children: <Widget>[
            Padding(
              padding: const EdgeInsets.fromLTRB(
                PayaboSpacing.xl,
                PayaboSpacing.lg,
                PayaboSpacing.xl,
                PayaboSpacing.md,
              ),
              child: Row(
                children: <Widget>[
                  SizedBox(
                    width: 32,
                    child: backRoute == null
                        ? const SizedBox.shrink()
                        : InkWell(
                            onTap: () => context.go(backRoute!),
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
                          ),
                    ),
                  ),
                  const SizedBox(width: 32),
                ],
              ),
            ),
            Expanded(
              child: SingleChildScrollView(
                padding: const EdgeInsets.fromLTRB(
                  PayaboSpacing.xl,
                  PayaboSpacing.md,
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
    );
  }
}
