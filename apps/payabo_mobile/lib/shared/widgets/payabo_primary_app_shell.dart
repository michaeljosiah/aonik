import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../theme/payabo_spacing.dart';
import 'payabo_bottom_nav.dart';
import 'payabo_list_row.dart';
import 'payabo_modal_sheet.dart';

enum PayaboPrimaryDestination {
  dashboard,
  pay,
  spending,
  chat,
  none,
}

class PayaboPrimaryAppShell extends StatelessWidget {
  const PayaboPrimaryAppShell({
    super.key,
    required this.destination,
    this.backgroundOverride,
    this.borderOverride,
    this.shadowOverride,
    this.selectedOverride,
    this.unselectedOverride,
    this.fabBackgroundOverride,
    this.fabShadowOverride,
  });

  final PayaboPrimaryDestination destination;

  /// Optional color overrides forwarded to [PayaboBottomNav].
  final Color? backgroundOverride;
  final Color? borderOverride;
  final Color? shadowOverride;
  final Color? selectedOverride;
  final Color? unselectedOverride;
  final Color? fabBackgroundOverride;
  final Color? fabShadowOverride;

  @override
  Widget build(BuildContext context) {
    return PayaboBottomNav(
      items: const <PayaboBottomNavItem>[
        PayaboBottomNavItem(icon: Icons.home_outlined, label: 'Home'),
        PayaboBottomNavItem(icon: Icons.receipt_long_outlined, label: 'Pay'),
        PayaboBottomNavItem(icon: Icons.show_chart_outlined, label: 'Spending'),
        PayaboBottomNavItem(icon: Icons.chat_bubble_outline, label: 'Chat'),
      ],
      currentIndex: _currentIndexFor(destination),
      onTap: (int index) => _handleNavTap(context, index),
      onCenterTap: () => _showQuickActions(context),
      backgroundOverride: backgroundOverride,
      borderOverride: borderOverride,
      shadowOverride: shadowOverride,
      selectedOverride: selectedOverride,
      unselectedOverride: unselectedOverride,
      fabBackgroundOverride: fabBackgroundOverride,
      fabShadowOverride: fabShadowOverride,
    );
  }

  static int _currentIndexFor(PayaboPrimaryDestination destination) {
    switch (destination) {
      case PayaboPrimaryDestination.dashboard:
        return 0;
      case PayaboPrimaryDestination.pay:
        return 1;
      case PayaboPrimaryDestination.spending:
        return 2;
      case PayaboPrimaryDestination.chat:
        return 3;
      case PayaboPrimaryDestination.none:
        return -1;
    }
  }

  static void _handleNavTap(BuildContext context, int index) {
    switch (index) {
      case 0:
        context.go('/dashboard');
        return;
      case 1:
        context.go('/pay');
        return;
      case 2:
        context.go('/spending');
        return;
      case 3:
        context.go('/chat');
        return;
    }
  }

  static Future<void> _showQuickActions(BuildContext context) async {
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
            onTap: () {
              Navigator.of(context).pop();
              context.go('/payments/friends');
            },
          ),
          const SizedBox(height: PayaboSpacing.sm),
          PayaboListRow(
            title: 'Account',
            subtitle: 'Manage your account details',
            leading: const Icon(Icons.account_balance_outlined),
            onTap: () {
              Navigator.of(context).pop();
              context.go('/spending/accounts');
            },
          ),
          const SizedBox(height: PayaboSpacing.sm),
          PayaboListRow(
            title: 'Income',
            subtitle: 'Track and categorize income',
            leading: const Icon(Icons.trending_up_outlined),
            onTap: () {
              Navigator.of(context).pop();
              ScaffoldMessenger.of(context)
                ..hideCurrentSnackBar()
                ..showSnackBar(
                  const SnackBar(
                    content: Text('Income tracking coming soon.'),
                  ),
                );
            },
          ),
        ],
      ),
    );
  }
}
