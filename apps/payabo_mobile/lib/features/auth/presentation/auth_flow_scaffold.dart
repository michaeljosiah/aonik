import 'package:flutter/material.dart';

import '../../../shared/theme/payabo_colors.dart';
import '../../../shared/theme/payabo_spacing.dart';

class AuthFlowScaffold extends StatelessWidget {
  const AuthFlowScaffold({
    super.key,
    required this.title,
    required this.child,
    this.description,
    this.footer,
    this.onBack,
    this.onClose,
    this.bottomSpacing = PayaboSpacing.x2,
  });

  final String title;
  final String? description;
  final Widget child;
  final Widget? footer;
  final VoidCallback? onBack;
  final VoidCallback? onClose;
  final double bottomSpacing;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: PayaboColors.white,
      body: SafeArea(
        child: Column(
          children: <Widget>[
            Padding(
              padding: const EdgeInsets.fromLTRB(PayaboSpacing.xl,
                  PayaboSpacing.lg, PayaboSpacing.xl, PayaboSpacing.x2),
              child: Row(
                children: <Widget>[
                  if (onBack != null)
                    _TopIconButton(
                      icon: Icons.arrow_back_ios_new,
                      onTap: onBack!,
                    )
                  else
                    const SizedBox(width: 44),
                  const Spacer(),
                  if (onClose != null)
                    _TopIconButton(
                      icon: Icons.close,
                      onTap: onClose!,
                    )
                  else
                    const SizedBox(width: 44),
                ],
              ),
            ),
            Expanded(
              child: SingleChildScrollView(
                padding:
                    const EdgeInsets.symmetric(horizontal: PayaboSpacing.xl),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text(
                      title,
                      style:
                          Theme.of(context).textTheme.headlineLarge?.copyWith(
                                fontWeight: FontWeight.w300,
                                fontSize: 32,
                                height: 1.15,
                              ),
                    ),
                    if (description != null) ...<Widget>[
                      const SizedBox(height: PayaboSpacing.md),
                      Text(description!,
                          style: Theme.of(context).textTheme.bodyLarge),
                    ],
                    const SizedBox(height: PayaboSpacing.xl),
                    child,
                    SizedBox(height: bottomSpacing),
                  ],
                ),
              ),
            ),
            if (footer != null)
              Padding(
                padding: const EdgeInsets.fromLTRB(
                    PayaboSpacing.xl, 0, PayaboSpacing.xl, PayaboSpacing.lg),
                child: footer!,
              ),
          ],
        ),
      ),
    );
  }
}

class _TopIconButton extends StatelessWidget {
  const _TopIconButton({
    required this.icon,
    required this.onTap,
  });

  final IconData icon;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(24),
      child: SizedBox(
        width: 44,
        height: 44,
        child: Icon(
          icon,
          color: PayaboColors.primary,
          size: icon == Icons.close ? 30 : 20,
        ),
      ),
    );
  }
}
