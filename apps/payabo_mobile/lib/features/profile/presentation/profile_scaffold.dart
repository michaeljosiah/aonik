import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_app_header.dart';
import '../../../shared/widgets/payabo_bottom_nav.dart';
import '../../../shared/widgets/payabo_list_row.dart';
import '../../../shared/widgets/payabo_modal_sheet.dart';
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
          const PayaboAppHeader(),
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
      bottomNavigationBar: PayaboBottomNav(
        items: const <PayaboBottomNavItem>[
          PayaboBottomNavItem(icon: Icons.home_outlined, label: 'Home'),
          PayaboBottomNavItem(
              icon: Icons.receipt_long_outlined, label: 'Bills'),
          PayaboBottomNavItem(
            icon: Icons.show_chart_outlined,
            label: 'Spending',
          ),
          PayaboBottomNavItem(icon: Icons.chat_bubble_outline, label: 'Chat'),
        ],
        currentIndex: -1,
        onTap: (index) => _handleNavTap(context, index),
        onCenterTap: () => _showQuickActions(context),
      ),
    );
  }

  void _handleNavTap(BuildContext context, int index) {
    switch (index) {
      case 0:
        context.go('/dashboard');
        return;
      case 1:
        context.go('/payments/country');
        return;
      case 2:
        context.go('/spending');
        return;
      case 3:
        context.go('/chat');
        return;
    }
  }

  Future<void> _showQuickActions(BuildContext context) async {
    await showPayaboModalSheet<void>(
      context: context,
      title: 'Quick Actions',
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          PayaboListRow(
            title: 'Pay a bill',
            subtitle: 'Start a bill payment now',
            leading: const Icon(Icons.receipt_long_outlined),
            onTap: () {
              Navigator.of(context).pop();
              context.go('/payments/country');
            },
          ),
          const SizedBox(height: PayaboSpacing.sm),
          PayaboListRow(
            title: 'Transfer',
            subtitle: 'Send money to another account',
            leading: const Icon(Icons.compare_arrows_outlined),
            onTap: () => Navigator.of(context).pop(),
          ),
          const SizedBox(height: PayaboSpacing.sm),
          PayaboListRow(
            title: 'Account',
            subtitle: 'Manage your account details',
            leading: const Icon(Icons.account_balance_outlined),
            onTap: () => Navigator.of(context).pop(),
          ),
          const SizedBox(height: PayaboSpacing.sm),
          PayaboListRow(
            title: 'Income',
            subtitle: 'Track and categorize income',
            leading: const Icon(Icons.trending_up_outlined),
            onTap: () => Navigator.of(context).pop(),
          ),
        ],
      ),
    );
  }
}
