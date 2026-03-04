import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../data/repositories/dashboard_repository.dart';
import '../../../data/repositories/repository_providers.dart';
import '../../../shared/theme/payabo_colors.dart';
import '../../../shared/theme/payabo_radii.dart';
import '../../../shared/theme/payabo_shadows.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_bottom_nav.dart';
import '../../../shared/widgets/payabo_button.dart';
import '../../../shared/widgets/payabo_card.dart';
import '../../../shared/widgets/payabo_list_row.dart';
import '../../../shared/widgets/payabo_modal_sheet.dart';

final FutureProvider<DashboardSummary> dashboardSummaryProvider =
    FutureProvider<DashboardSummary>((Ref ref) async {
  final repository = ref.watch(dashboardRepositoryProvider);
  return repository.getSummary();
});

class DashboardScreen extends ConsumerStatefulWidget {
  const DashboardScreen({
    super.key,
    this.showEmptyState = false,
  });

  final bool showEmptyState;

  @override
  ConsumerState<DashboardScreen> createState() => _DashboardScreenState();
}

class _DashboardScreenState extends ConsumerState<DashboardScreen> {
  int _navIndex = 0;

  @override
  Widget build(BuildContext context) {
    final summaryValue = ref.watch(dashboardSummaryProvider);

    return Scaffold(
      backgroundColor: PayaboColors.white,
      body: SafeArea(
        child: Column(
          children: <Widget>[
            _DashboardHeader(onProfileTap: () => context.go('/profile')),
            Expanded(
              child: summaryValue.when(
                data: (summary) {
                  final isEmpty = widget.showEmptyState;

                  return RefreshIndicator(
                    onRefresh: () async =>
                        ref.refresh(dashboardSummaryProvider.future),
                    child: ListView(
                      padding: const EdgeInsets.only(bottom: 24),
                      children: <Widget>[
                        _SectionHeading(
                          title: 'Organisations',
                          actionLabel: 'View all',
                          onActionTap: () {},
                        ),
                        if (isEmpty)
                          const _EmptyPanel(
                            icon: Icons.groups_2_outlined,
                            message: "You don't follow any\norganisations.",
                            actionLabel: 'Add Organisation',
                          )
                        else
                          const _OrganisationCarousel(),
                        _SectionHeading(
                          title: 'Upcoming bills',
                          actionLabel: isEmpty ? null : 'View all',
                          onActionTap: () {},
                        ),
                        if (isEmpty)
                          const _EmptyPanel(
                            icon: Icons.receipt_long_outlined,
                            message: 'No bills to show.',
                            actionLabel: 'Pay a Bill',
                          )
                        else
                          _BillList(items: summary.upcomingBills),
                        _SectionHeading(
                          title: 'Recent transactions',
                          actionLabel: isEmpty ? null : 'View all',
                          onActionTap: () {},
                        ),
                        if (isEmpty)
                          const _EmptyPanel(
                            icon: Icons.swap_horiz_outlined,
                            message: 'No transactions.',
                          )
                        else
                          _TransactionList(items: summary.recentTransactions),
                        _SectionHeading(
                          title: 'Budget',
                          actionLabel: isEmpty ? null : 'View report',
                          onActionTap: () {},
                        ),
                        if (isEmpty)
                          const _EmptyPanel(
                            icon: Icons.pie_chart_outline,
                            message: 'No budget set.',
                            actionLabel: 'Create your budget',
                          )
                        else
                          const _BudgetCarousel(),
                        _SectionHeading(
                          title: 'Bill pay assist request',
                          actionLabel: isEmpty ? null : 'View all',
                          onActionTap: () {},
                        ),
                        if (isEmpty)
                          const _EmptyPanel(
                            icon: Icons.handshake_outlined,
                            message: 'No requests.',
                          )
                        else
                          const _AssistRequestCarousel(),
                      ],
                    ),
                  );
                },
                loading: () => const Center(child: CircularProgressIndicator()),
                error: (error, stackTrace) {
                  return Center(
                    child: Padding(
                      padding: const EdgeInsets.all(PayaboSpacing.xl),
                      child: Text('Unable to load dashboard: $error'),
                    ),
                  );
                },
              ),
            ),
          ],
        ),
      ),
      bottomNavigationBar: PayaboBottomNav(
        items: const <PayaboBottomNavItem>[
          PayaboBottomNavItem(icon: Icons.home_outlined, label: 'Home'),
          PayaboBottomNavItem(
              icon: Icons.receipt_long_outlined, label: 'Bills'),
          PayaboBottomNavItem(
              icon: Icons.show_chart_outlined, label: 'Spending'),
          PayaboBottomNavItem(icon: Icons.more_horiz, label: 'More'),
        ],
        currentIndex: _navIndex,
        onTap: _handleNavTap,
        onCenterTap: _showQuickActions,
      ),
    );
  }

  void _handleNavTap(int index) {
    setState(() {
      _navIndex = index;
    });

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
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Section coming soon in mock build.')),
        );
        return;
    }
  }

  Future<void> _showQuickActions() async {
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

class _DashboardHeader extends StatelessWidget {
  const _DashboardHeader({required this.onProfileTap});

  final VoidCallback onProfileTap;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.fromLTRB(PayaboSpacing.xl, PayaboSpacing.md,
          PayaboSpacing.xl, PayaboSpacing.md),
      decoration: const BoxDecoration(
        color: PayaboColors.white,
        boxShadow: PayaboShadows.soft,
      ),
      child: Row(
        children: <Widget>[
          InkWell(
            onTap: onProfileTap,
            borderRadius: BorderRadius.circular(20),
            child: const CircleAvatar(
              radius: 20,
              backgroundColor: PayaboColors.background,
              child: Icon(Icons.person_outline, color: PayaboColors.ink),
            ),
          ),
          Expanded(
            child: Center(
              child: Image.asset(
                'assets/images/mba_logo.png',
                height: 34,
                fit: BoxFit.contain,
              ),
            ),
          ),
          SizedBox(
            width: 40,
            child: Stack(
              alignment: Alignment.center,
              children: <Widget>[
                IconButton(
                  onPressed: () {},
                  icon: const Icon(Icons.mail_outline, color: PayaboColors.ink),
                ),
                Positioned(
                  top: 10,
                  right: 10,
                  child: Container(
                    width: 10,
                    height: 10,
                    decoration: const BoxDecoration(
                      color: PayaboColors.primary,
                      shape: BoxShape.circle,
                    ),
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _SectionHeading extends StatelessWidget {
  const _SectionHeading({
    required this.title,
    required this.onActionTap,
    this.actionLabel,
  });

  final String title;
  final String? actionLabel;
  final VoidCallback onActionTap;

  @override
  Widget build(BuildContext context) {
    return Container(
      color: PayaboColors.background,
      padding: const EdgeInsets.fromLTRB(PayaboSpacing.xl, PayaboSpacing.md,
          PayaboSpacing.xl, PayaboSpacing.md),
      child: Row(
        children: <Widget>[
          Expanded(
            child: Text(
              title,
              style: Theme.of(context)
                  .textTheme
                  .titleMedium
                  ?.copyWith(fontSize: 18, fontWeight: FontWeight.w700),
            ),
          ),
          if (actionLabel != null)
            TextButton(
              onPressed: onActionTap,
              child: Text(actionLabel!),
            ),
        ],
      ),
    );
  }
}

class _EmptyPanel extends StatelessWidget {
  const _EmptyPanel({
    required this.icon,
    required this.message,
    this.actionLabel,
  });

  final IconData icon;
  final String message;
  final String? actionLabel;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.all(PayaboSpacing.xl),
      child: Container(
        padding: const EdgeInsets.all(PayaboSpacing.x3),
        decoration: const BoxDecoration(
          color: PayaboColors.white,
          borderRadius: PayaboRadii.radiusLg,
          boxShadow: PayaboShadows.soft,
        ),
        child: Column(
          children: <Widget>[
            Icon(icon, size: 56, color: PayaboColors.muted),
            const SizedBox(height: PayaboSpacing.md),
            Text(
              message,
              textAlign: TextAlign.center,
              style: Theme.of(context)
                  .textTheme
                  .titleSmall
                  ?.copyWith(color: PayaboColors.muted),
            ),
            if (actionLabel != null) ...<Widget>[
              const SizedBox(height: PayaboSpacing.md),
              PayaboButton(
                label: actionLabel!,
                size: PayaboButtonSize.sm,
                expand: false,
                onPressed: () {},
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _OrganisationCarousel extends StatelessWidget {
  const _OrganisationCarousel();

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 230,
      child: PageView(
        controller: PageController(viewportFraction: 0.88),
        children: const <Widget>[
          _OrganisationCard(name: 'Volunteer of the World', sponsored: true),
          _OrganisationCard(name: 'Organisation name'),
          _OrganisationCard(name: 'Organisation name'),
        ],
      ),
    );
  }
}

class _OrganisationCard extends StatelessWidget {
  const _OrganisationCard({
    required this.name,
    this.sponsored = false,
  });

  final String name;
  final bool sponsored;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(PayaboSpacing.xl, PayaboSpacing.lg,
          PayaboSpacing.sm, PayaboSpacing.x2),
      child: PayaboCard(
        padding: EdgeInsets.zero,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Expanded(
              child: Container(
                decoration: const BoxDecoration(
                  borderRadius: BorderRadius.only(
                    topLeft: Radius.circular(PayaboRadii.sm),
                    topRight: Radius.circular(PayaboRadii.sm),
                  ),
                  gradient: LinearGradient(
                    colors: <Color>[Color(0xFF4A74FF), Color(0xFF7A15FF)],
                    begin: Alignment.topLeft,
                    end: Alignment.bottomRight,
                  ),
                ),
                child: Stack(
                  children: <Widget>[
                    if (sponsored)
                      Positioned(
                        left: 12,
                        top: 12,
                        child: Container(
                          padding: const EdgeInsets.symmetric(
                              horizontal: 10, vertical: 4),
                          decoration: BoxDecoration(
                            color: PayaboColors.info,
                            borderRadius: BorderRadius.circular(4),
                          ),
                          child: Text(
                            'SPONSORED',
                            style:
                                Theme.of(context).textTheme.bodySmall?.copyWith(
                                      color: PayaboColors.white,
                                      fontWeight: FontWeight.w700,
                                      fontSize: 10,
                                    ),
                          ),
                        ),
                      ),
                  ],
                ),
              ),
            ),
            Padding(
              padding: const EdgeInsets.all(PayaboSpacing.lg),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(name, style: Theme.of(context).textTheme.titleSmall),
                  const SizedBox(height: 4),
                  Text('Line for extra info',
                      style: Theme.of(context).textTheme.bodySmall),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _BillList extends StatelessWidget {
  const _BillList({required this.items});

  final List<DashboardUpcomingBill> items;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: PayaboSpacing.xl),
      child: Column(
        children: items
            .map(
              (bill) => _ProductRow(
                title: bill.biller,
                subtitle: bill.dueDateLabel,
                amountLabel: bill.amountLabel,
              ),
            )
            .toList(growable: false),
      ),
    );
  }
}

class _TransactionList extends StatelessWidget {
  const _TransactionList({required this.items});

  final List<DashboardTransaction> items;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: PayaboSpacing.xl),
      child: Column(
        children: items
            .map(
              (transaction) => _ProductRow(
                title: transaction.title,
                subtitle: transaction.status,
                amountLabel: transaction.amountLabel,
              ),
            )
            .toList(growable: false),
      ),
    );
  }
}

class _ProductRow extends StatelessWidget {
  const _ProductRow({
    required this.title,
    required this.subtitle,
    required this.amountLabel,
  });

  final String title;
  final String subtitle;
  final String amountLabel;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(vertical: PayaboSpacing.md),
      decoration: const BoxDecoration(
        border:
            Border(bottom: BorderSide(color: PayaboColors.border, width: 1)),
      ),
      child: Row(
        children: <Widget>[
          Container(
            width: 42,
            height: 42,
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(8),
              color: PayaboColors.background,
            ),
            child: const Icon(Icons.description_outlined,
                color: PayaboColors.primary),
          ),
          const SizedBox(width: PayaboSpacing.md),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(title, style: Theme.of(context).textTheme.titleSmall),
                const SizedBox(height: 2),
                Text(subtitle, style: Theme.of(context).textTheme.bodySmall),
              ],
            ),
          ),
          Text(
            amountLabel,
            style: Theme.of(context)
                .textTheme
                .bodyLarge
                ?.copyWith(fontWeight: FontWeight.w600),
          ),
        ],
      ),
    );
  }
}

class _BudgetCarousel extends StatelessWidget {
  const _BudgetCarousel();

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 196,
      child: PageView(
        controller: PageController(viewportFraction: 0.88),
        children: const <Widget>[
          _BudgetCard(month: 'July 2022', progress: 0.25),
          _BudgetCard(month: 'June 2022', progress: 0.42),
          _BudgetCard(month: 'May 2022', progress: 0.35),
        ],
      ),
    );
  }
}

class _BudgetCard extends StatelessWidget {
  const _BudgetCard({
    required this.month,
    required this.progress,
  });

  final String month;
  final double progress;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(PayaboSpacing.xl, PayaboSpacing.lg,
          PayaboSpacing.sm, PayaboSpacing.x2),
      child: PayaboCard(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Text(month, style: Theme.of(context).textTheme.titleSmall),
            const SizedBox(height: PayaboSpacing.md),
            ClipRRect(
              borderRadius: BorderRadius.circular(5),
              child: LinearProgressIndicator(
                minHeight: 10,
                value: progress,
                backgroundColor: PayaboColors.border,
                valueColor:
                    const AlwaysStoppedAnimation<Color>(PayaboColors.primary),
              ),
            ),
            const SizedBox(height: PayaboSpacing.md),
            Row(
              children: <Widget>[
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Text('SPENT',
                          style: Theme.of(context).textTheme.bodySmall),
                      const SizedBox(height: 2),
                      Text('N 0,000.00',
                          style: Theme.of(context).textTheme.titleSmall),
                    ],
                  ),
                ),
                Column(
                  crossAxisAlignment: CrossAxisAlignment.end,
                  children: <Widget>[
                    Text('BUDGET',
                        style: Theme.of(context).textTheme.bodySmall),
                    const SizedBox(height: 2),
                    Text('N 0,000.00',
                        style: Theme.of(context).textTheme.titleSmall),
                  ],
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _AssistRequestCarousel extends StatelessWidget {
  const _AssistRequestCarousel();

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 186,
      child: PageView(
        controller: PageController(viewportFraction: 0.88),
        children: const <Widget>[
          _AssistRequestCard(
              friendName: 'Alicia Keys', amountLabel: 'N 0,000.00'),
          _AssistRequestCard(
              friendName: 'Friend Name', amountLabel: 'N 0,000.00'),
          _AssistRequestCard(
              friendName: 'Friend Name', amountLabel: 'N 0,000.00'),
        ],
      ),
    );
  }
}

class _AssistRequestCard extends StatelessWidget {
  const _AssistRequestCard({
    required this.friendName,
    required this.amountLabel,
  });

  final String friendName;
  final String amountLabel;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(PayaboSpacing.xl, PayaboSpacing.lg,
          PayaboSpacing.sm, PayaboSpacing.x2),
      child: PayaboCard(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Row(
              children: <Widget>[
                const CircleAvatar(
                  radius: 18,
                  backgroundColor: PayaboColors.background,
                  child:
                      Icon(Icons.person_outline, color: PayaboColors.primary),
                ),
                const SizedBox(width: PayaboSpacing.md),
                Text(friendName, style: Theme.of(context).textTheme.titleSmall),
              ],
            ),
            const SizedBox(height: PayaboSpacing.lg),
            Row(
              children: <Widget>[
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Text('Bill name',
                          style: Theme.of(context).textTheme.titleSmall),
                      Text('Line for extra info',
                          style: Theme.of(context).textTheme.bodySmall),
                    ],
                  ),
                ),
                Text(amountLabel, style: Theme.of(context).textTheme.bodyLarge),
              ],
            ),
          ],
        ),
      ),
    );
  }
}
