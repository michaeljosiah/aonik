import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_radii.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_app_header.dart';
import '../../../shared/widgets/payabo_list_row.dart';
import '../../../shared/widgets/payabo_primary_app_shell.dart';
import '../../../shared/widgets/payabo_warm_scaffold.dart';

class PayDashboardScreen extends StatelessWidget {
  const PayDashboardScreen({super.key});

  static const List<_PayActivityItem> _activities = <_PayActivityItem>[
    _PayActivityItem(
      title: 'DSTV subscription',
      subtitle: 'Today, 09:42 AM',
      amount: 'GHS 240.00',
      status: 'Completed',
      icon: Icons.receipt_long_outlined,
    ),
    _PayActivityItem(
      title: 'Transfer to Ama Serwaa',
      subtitle: 'Yesterday, 07:18 PM',
      amount: 'GHS 500.00',
      status: 'Sent',
      icon: Icons.send_rounded,
    ),
    _PayActivityItem(
      title: 'ECG prepaid top-up',
      subtitle: 'Mon, 11:05 AM',
      amount: 'GHS 120.00',
      status: 'Processing',
      icon: Icons.flash_on_outlined,
    ),
  ];

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final textTheme = Theme.of(context).textTheme;

    return PayaboWarmScaffold(
      backgroundDecoration: const BoxDecoration(
        gradient: LinearGradient(
          colors: <Color>[
            Color(0xFFF7F1E8),
            Color(0xFFF3E7D8),
            Color(0xFFE8D6C2),
          ],
          stops: <double>[0, 0.5, 1],
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
        ),
      ),
      bottomNavigationBar: const PayaboPrimaryAppShell(
        destination: PayaboPrimaryDestination.pay,
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.only(bottom: PayaboSpacing.xl),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            PayaboAppHeader(
              title: 'Pay',
              subtitle: 'Choose how you want to move money today.',
              titleStyle: textTheme.headlineMedium?.copyWith(
                color: c.headerTitle,
                fontWeight: FontWeight.w800,
              ),
            ),
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: PayaboSpacing.xl),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Row(
                    children: <Widget>[
                      Expanded(
                        child: _PayActionCard(
                          title: 'Pay a bill',
                          subtitle: 'Utilities, TV and everyday essentials.',
                          icon: Icons.receipt_long_outlined,
                          colors: const <Color>[
                            Color(0xFF27435F),
                            Color(0xFF446D8C),
                          ],
                          onTap: () => context.go('/payments/country'),
                        ),
                      ),
                      const SizedBox(width: PayaboSpacing.md),
                      Expanded(
                        child: _PayActionCard(
                          title: 'Send Money',
                          subtitle: 'Transfer to friends and family quickly.',
                          icon: Icons.send_rounded,
                          colors: const <Color>[
                            Color(0xFF5B3C2C),
                            Color(0xFF9A6846),
                          ],
                          onTap: () => context.go('/payments/friends'),
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: PayaboSpacing.xl),
                  Text(
                    'Recent Activities',
                    style: textTheme.titleLarge?.copyWith(
                      color: c.headerTitle,
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                  const SizedBox(height: PayaboSpacing.xs),
                  Text(
                    'Keep track of your latest bill payments and transfers.',
                    style: textTheme.bodyMedium?.copyWith(
                      color: c.textSecondary,
                      height: 1.4,
                    ),
                  ),
                  const SizedBox(height: PayaboSpacing.lg),
                  for (final activity in _activities) ...<Widget>[
                    PayaboListRow(
                      title: activity.title,
                      subtitle: activity.subtitle,
                      leading: _ActivityLeadingIcon(icon: activity.icon),
                      trailing: _ActivityTrailing(
                        amount: activity.amount,
                        status: activity.status,
                      ),
                    ),
                    const SizedBox(height: PayaboSpacing.md),
                  ],
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _PayActionCard extends StatelessWidget {
  const _PayActionCard({
    required this.title,
    required this.subtitle,
    required this.icon,
    required this.colors,
    required this.onTap,
  });

  final String title;
  final String subtitle;
  final IconData icon;
  final List<Color> colors;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;

    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(24),
        child: Ink(
          padding: const EdgeInsets.all(PayaboSpacing.lg),
          decoration: BoxDecoration(
            gradient: LinearGradient(
              colors: colors,
              begin: Alignment.topLeft,
              end: Alignment.bottomRight,
            ),
            borderRadius: BorderRadius.circular(24),
          ),
          child: SizedBox(
            height: 164,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Container(
                  width: 44,
                  height: 44,
                  decoration: BoxDecoration(
                    color: Colors.white.withValues(alpha: 0.18),
                    borderRadius: PayaboRadii.radiusLg,
                  ),
                  child: Icon(icon, color: Colors.white),
                ),
                const Spacer(),
                Text(
                  title,
                  style: textTheme.titleMedium?.copyWith(
                    color: Colors.white,
                    fontWeight: FontWeight.w800,
                  ),
                ),
                const SizedBox(height: PayaboSpacing.xs),
                Text(
                  subtitle,
                  style: textTheme.bodySmall?.copyWith(
                    color: Colors.white.withValues(alpha: 0.84),
                    height: 1.4,
                  ),
                ),
                const SizedBox(height: PayaboSpacing.md),
                const Icon(
                  Icons.arrow_forward_rounded,
                  color: Colors.white,
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _ActivityLeadingIcon extends StatelessWidget {
  const _ActivityLeadingIcon({required this.icon});

  final IconData icon;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Container(
      width: 44,
      height: 44,
      decoration: BoxDecoration(
        color: c.surfaceWarmAccent,
        borderRadius: PayaboRadii.radiusLg,
      ),
      child: Icon(icon, color: c.headerTitle),
    );
  }
}

class _ActivityTrailing extends StatelessWidget {
  const _ActivityTrailing({required this.amount, required this.status});

  final String amount;
  final String status;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final textTheme = Theme.of(context).textTheme;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.end,
      mainAxisAlignment: MainAxisAlignment.center,
      children: <Widget>[
        Text(
          amount,
          style: textTheme.titleSmall?.copyWith(
            color: c.textPrimary,
            fontWeight: FontWeight.w700,
          ),
        ),
        const SizedBox(height: 4),
        Container(
          padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
          decoration: BoxDecoration(
            color: c.surfaceWarmAccent,
            borderRadius: PayaboRadii.radiusPill,
          ),
          child: Text(
            status,
            style: textTheme.labelSmall?.copyWith(
              color: c.headerTitle,
              fontWeight: FontWeight.w700,
            ),
          ),
        ),
      ],
    );
  }
}

class _PayActivityItem {
  const _PayActivityItem({
    required this.title,
    required this.subtitle,
    required this.amount,
    required this.status,
    required this.icon,
  });

  final String title;
  final String subtitle;
  final String amount;
  final String status;
  final IconData icon;
}
