import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../data/repositories/pay_activity_repository.dart';
import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_radii.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_primary_app_shell.dart';
import '../../../shared/widgets/payabo_warm_scaffold.dart';
import '../application/pay_activity_providers.dart';
import 'pay_dashboard_screen.dart';

// ═══════════════════════════════════════════════════════════════════════════
// Transaction Details — Screen 3 of the Pay redesign.
//
// Header:   back arrow + "Transaction Details" title
// Status:   dark green banner with checkmark, status text, description
// Breakdown: Amount / Fee / Total rows
// Recipient: avatar, name, bank, account, country
// Technical: collapsible Order ID, Payment Intent ID, Provider Ref, Ref
// Actions:  Download Receipt | Send Receipt | Contact Support
// ═══════════════════════════════════════════════════════════════════════════

class PayTransactionDetailsScreen extends ConsumerStatefulWidget {
  const PayTransactionDetailsScreen({
    super.key,
    required this.transactionId,
  });

  final String transactionId;

  @override
  ConsumerState<PayTransactionDetailsScreen> createState() =>
      _PayTransactionDetailsScreenState();
}

class _PayTransactionDetailsScreenState
    extends ConsumerState<PayTransactionDetailsScreen> {
  bool _technicalDetailsExpanded = false;

  // Same dark charcoal gradient as the dashboard.
  static const LinearGradient _backgroundGradient = LinearGradient(
    colors: <Color>[
      Color(0xFF242223),
      Color(0xFF191718),
      Color(0xFF0F0D0E),
    ],
    stops: <double>[0, 0.46, 1],
    begin: Alignment.topCenter,
    end: Alignment.bottomCenter,
  );

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final textTheme = Theme.of(context).textTheme;
    final detailAsync =
        ref.watch(payTransactionDetailProvider(widget.transactionId));

    return PayaboWarmScaffold(
      backgroundDecoration: const BoxDecoration(gradient: _backgroundGradient),
      bottomNavigationBar: const PayaboPrimaryAppShell(
        destination: PayaboPrimaryDestination.pay,
      ),
      body: SafeArea(
        bottom: false,
        child: Column(
          children: <Widget>[
            // ── Header: back + title ──────────────────────────
            Padding(
              padding: const EdgeInsets.fromLTRB(
                PayaboSpacing.sm,
                PayaboSpacing.md,
                PayaboSpacing.lg,
                PayaboSpacing.md,
              ),
              child: Row(
                children: <Widget>[
                  IconButton(
                    onPressed: () => context.pop(),
                    icon: const Icon(Icons.arrow_back, color: Colors.white),
                    splashRadius: 22,
                  ),
                  Expanded(
                    child: Text(
                      'Transaction Details',
                      style: textTheme.titleLarge?.copyWith(
                        color: Colors.white,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                  ),
                ],
              ),
            ),

            // ── Scrollable body ───────────────────────────────
            Expanded(
              child: detailAsync.when(
                loading: () => const Center(
                  child: CircularProgressIndicator(),
                ),
                error: (_, __) => Center(
                  child: Text(
                    'Unable to load transaction details',
                    style: textTheme.bodyLarge?.copyWith(
                      color: Colors.white.withValues(alpha: 0.5),
                    ),
                  ),
                ),
                data: (PayTransactionDetail? detail) {
                  if (detail == null) {
                    return Center(
                      child: Text(
                        'Transaction not found',
                        style: textTheme.bodyLarge?.copyWith(
                          color: Colors.white.withValues(alpha: 0.5),
                        ),
                      ),
                    );
                  }

                  return ListView(
                    padding: const EdgeInsets.fromLTRB(
                      PayaboSpacing.xl,
                      0,
                      PayaboSpacing.xl,
                      PayaboSpacing.x4,
                    ),
                    physics: const BouncingScrollPhysics(
                      parent: AlwaysScrollableScrollPhysics(),
                    ),
                    children: <Widget>[
                      // ── Status banner ─────────────────────────
                      _StatusBanner(
                        colors: c,
                        textTheme: textTheme,
                        status: detail.status,
                        statusDescription: detail.statusDescription,
                      ),
                      const SizedBox(height: PayaboSpacing.x2),

                      // ── Amount breakdown ──────────────────────
                      _AmountBreakdown(
                        colors: c,
                        textTheme: textTheme,
                        amountLabel: detail.amountLabel,
                        feeLabel: detail.feeLabel,
                        totalLabel: detail.totalLabel,
                      ),
                      const SizedBox(height: PayaboSpacing.x2),

                      // ── Recipient section ─────────────────────
                      _RecipientSection(
                        colors: c,
                        textTheme: textTheme,
                        recipient: detail.recipient,
                      ),
                      const SizedBox(height: PayaboSpacing.x2),

                      // ── Technical details (expandable) ────────
                      _TechnicalDetails(
                        colors: c,
                        textTheme: textTheme,
                        expanded: _technicalDetailsExpanded,
                        onToggle: () {
                          setState(() {
                            _technicalDetailsExpanded =
                                !_technicalDetailsExpanded;
                          });
                        },
                        orderId: detail.orderId,
                        paymentIntentId: detail.paymentIntentId,
                        providerReference: detail.providerReference,
                        reference: detail.reference,
                      ),
                      const SizedBox(height: PayaboSpacing.x3),

                      // ── Action buttons ────────────────────────
                      _ActionButtons(colors: c, textTheme: textTheme),
                    ],
                  );
                },
              ),
            ),
          ],
        ),
      ),
    );
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// Status banner — dark green container with checkmark, status, description
// ═══════════════════════════════════════════════════════════════════════════

class _StatusBanner extends StatelessWidget {
  const _StatusBanner({
    required this.colors,
    required this.textTheme,
    required this.status,
    required this.statusDescription,
  });

  final PayaboColorResolver colors;
  final TextTheme textTheme;
  final String status;
  final String statusDescription;

  @override
  Widget build(BuildContext context) {
    final Color statusColor = resolveStatusColor(colors, status);
    final Color bannerBg = statusColor.withValues(alpha: 0.15);
    final Color checkBg = statusColor.withValues(alpha: 0.25);

    final IconData statusIcon;
    switch (status.toLowerCase()) {
      case 'completed':
      case 'sent':
        statusIcon = Icons.check_rounded;
        break;
      case 'processing':
        statusIcon = Icons.schedule_rounded;
        break;
      case 'failed':
        statusIcon = Icons.close_rounded;
        break;
      default:
        statusIcon = Icons.info_outline_rounded;
    }

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(PayaboSpacing.lg),
      decoration: BoxDecoration(
        color: bannerBg,
        borderRadius: PayaboRadii.radiusLg,
        border: Border.all(
          color: statusColor.withValues(alpha: 0.25),
          width: 0.5,
        ),
      ),
      child: Column(
        children: <Widget>[
          // Status icon circle
          Container(
            width: 44,
            height: 44,
            decoration: BoxDecoration(
              color: checkBg,
              shape: BoxShape.circle,
            ),
            child: Icon(
              statusIcon,
              color: statusColor,
              size: 26,
            ),
          ),
          const SizedBox(height: PayaboSpacing.md),

          // Status text
          Text(
            status,
            style: textTheme.titleMedium?.copyWith(
              color: statusColor,
              fontWeight: FontWeight.w700,
            ),
          ),
          const SizedBox(height: PayaboSpacing.xs),

          // Description
          Text(
            statusDescription,
            style: textTheme.bodyMedium?.copyWith(
              color: Colors.white.withValues(alpha: 0.60),
            ),
          ),
        ],
      ),
    );
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// Amount breakdown — Amount / Fee / Total
// ═══════════════════════════════════════════════════════════════════════════

class _AmountBreakdown extends StatelessWidget {
  const _AmountBreakdown({
    required this.colors,
    required this.textTheme,
    required this.amountLabel,
    required this.feeLabel,
    required this.totalLabel,
  });

  final PayaboColorResolver colors;
  final TextTheme textTheme;
  final String amountLabel;
  final String feeLabel;
  final String totalLabel;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(PayaboSpacing.lg),
      decoration: BoxDecoration(
        color: Colors.white.withValues(alpha: 0.06),
        borderRadius: PayaboRadii.radiusLg,
        border: Border.all(
          color: Colors.white.withValues(alpha: 0.08),
          width: 0.5,
        ),
      ),
      child: Column(
        children: <Widget>[
          _BreakdownRow(
            label: 'Amount',
            value: amountLabel,
            textTheme: textTheme,
          ),
          const SizedBox(height: PayaboSpacing.md),
          _BreakdownRow(
            label: 'Fee',
            value: feeLabel,
            textTheme: textTheme,
          ),
          const SizedBox(height: PayaboSpacing.md),
          Divider(
            height: 1,
            color: Colors.white.withValues(alpha: 0.10),
          ),
          const SizedBox(height: PayaboSpacing.md),
          _BreakdownRow(
            label: 'Total',
            value: totalLabel,
            textTheme: textTheme,
            isBold: true,
          ),
        ],
      ),
    );
  }
}

class _BreakdownRow extends StatelessWidget {
  const _BreakdownRow({
    required this.label,
    required this.value,
    required this.textTheme,
    this.isBold = false,
  });

  final String label;
  final String value;
  final TextTheme textTheme;
  final bool isBold;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: <Widget>[
        Text(
          label,
          style: textTheme.bodyMedium?.copyWith(
            color: Colors.white.withValues(alpha: 0.55),
            fontWeight: isBold ? FontWeight.w600 : FontWeight.w400,
          ),
        ),
        Text(
          value,
          style: textTheme.bodyMedium?.copyWith(
            color: Colors.white,
            fontWeight: isBold ? FontWeight.w700 : FontWeight.w500,
          ),
        ),
      ],
    );
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// Recipient section — avatar, name, bank, account, country
// ═══════════════════════════════════════════════════════════════════════════

class _RecipientSection extends StatelessWidget {
  const _RecipientSection({
    required this.colors,
    required this.textTheme,
    required this.recipient,
  });

  final PayaboColorResolver colors;
  final TextTheme textTheme;
  final PayTransactionRecipient recipient;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(PayaboSpacing.lg),
      decoration: BoxDecoration(
        color: Colors.white.withValues(alpha: 0.06),
        borderRadius: PayaboRadii.radiusLg,
        border: Border.all(
          color: Colors.white.withValues(alpha: 0.08),
          width: 0.5,
        ),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(
            'Recipient',
            style: textTheme.labelMedium?.copyWith(
              color: Colors.white.withValues(alpha: 0.45),
              fontWeight: FontWeight.w600,
              letterSpacing: 0.5,
            ),
          ),
          const SizedBox(height: PayaboSpacing.lg),

          // Recipient row with avatar
          Row(
            children: <Widget>[
              // Avatar
              Container(
                width: 44,
                height: 44,
                decoration: BoxDecoration(
                  color: colors.primary.withValues(alpha: 0.15),
                  shape: BoxShape.circle,
                ),
                child: Center(
                  child: Text(
                    recipient.initials,
                    style: textTheme.titleSmall?.copyWith(
                      color: colors.primary,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ),
              ),
              const SizedBox(width: PayaboSpacing.md),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text(
                      recipient.name,
                      style: textTheme.titleSmall?.copyWith(
                        color: Colors.white,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                    const SizedBox(height: 2),
                    Text(
                      recipient.bankName,
                      style: textTheme.bodySmall?.copyWith(
                        color: Colors.white.withValues(alpha: 0.55),
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: PayaboSpacing.lg),

          // Account details
          _RecipientDetail(
            label: 'Account Number',
            value: recipient.maskedAccountNumber,
            textTheme: textTheme,
          ),
          const SizedBox(height: PayaboSpacing.md),
          _RecipientDetail(
            label: 'Country',
            value: recipient.country,
            textTheme: textTheme,
          ),
        ],
      ),
    );
  }
}

class _RecipientDetail extends StatelessWidget {
  const _RecipientDetail({
    required this.label,
    required this.value,
    required this.textTheme,
  });

  final String label;
  final String value;
  final TextTheme textTheme;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: <Widget>[
        Text(
          label,
          style: textTheme.bodySmall?.copyWith(
            color: Colors.white.withValues(alpha: 0.45),
          ),
        ),
        Text(
          value,
          style: textTheme.bodySmall?.copyWith(
            color: Colors.white.withValues(alpha: 0.80),
            fontWeight: FontWeight.w500,
          ),
        ),
      ],
    );
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// Technical details — expandable section
// ═══════════════════════════════════════════════════════════════════════════

class _TechnicalDetails extends StatelessWidget {
  const _TechnicalDetails({
    required this.colors,
    required this.textTheme,
    required this.expanded,
    required this.onToggle,
    required this.orderId,
    required this.paymentIntentId,
    required this.providerReference,
    required this.reference,
  });

  final PayaboColorResolver colors;
  final TextTheme textTheme;
  final bool expanded;
  final VoidCallback onToggle;
  final String orderId;
  final String paymentIntentId;
  final String providerReference;
  final String reference;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      decoration: BoxDecoration(
        color: Colors.white.withValues(alpha: 0.06),
        borderRadius: PayaboRadii.radiusLg,
        border: Border.all(
          color: Colors.white.withValues(alpha: 0.08),
          width: 0.5,
        ),
      ),
      child: Column(
        children: <Widget>[
          // Toggle header
          InkWell(
            onTap: onToggle,
            borderRadius: PayaboRadii.radiusLg,
            child: Padding(
              padding: const EdgeInsets.all(PayaboSpacing.lg),
              child: Row(
                children: <Widget>[
                  Expanded(
                    child: Text(
                      'Technical Details',
                      style: textTheme.labelMedium?.copyWith(
                        color: Colors.white.withValues(alpha: 0.45),
                        fontWeight: FontWeight.w600,
                        letterSpacing: 0.5,
                      ),
                    ),
                  ),
                  AnimatedRotation(
                    turns: expanded ? 0.5 : 0.0,
                    duration: const Duration(milliseconds: 200),
                    child: Icon(
                      Icons.keyboard_arrow_down_rounded,
                      color: Colors.white.withValues(alpha: 0.45),
                      size: 22,
                    ),
                  ),
                ],
              ),
            ),
          ),

          // Expandable content
          AnimatedCrossFade(
            firstChild: const SizedBox(width: double.infinity, height: 0),
            secondChild: Padding(
              padding: const EdgeInsets.fromLTRB(
                PayaboSpacing.lg,
                0,
                PayaboSpacing.lg,
                PayaboSpacing.lg,
              ),
              child: Column(
                children: <Widget>[
                  Divider(
                    height: 1,
                    color: Colors.white.withValues(alpha: 0.08),
                  ),
                  const SizedBox(height: PayaboSpacing.md),
                  _TechnicalRow(
                    label: 'Order ID',
                    value: orderId,
                    textTheme: textTheme,
                  ),
                  const SizedBox(height: PayaboSpacing.md),
                  _TechnicalRow(
                    label: 'Payment Intent ID',
                    value: paymentIntentId,
                    textTheme: textTheme,
                  ),
                  const SizedBox(height: PayaboSpacing.md),
                  _TechnicalRow(
                    label: 'Provider Ref',
                    value: providerReference,
                    textTheme: textTheme,
                  ),
                  const SizedBox(height: PayaboSpacing.md),
                  _TechnicalRow(
                    label: 'Reference',
                    value: reference,
                    textTheme: textTheme,
                  ),
                ],
              ),
            ),
            crossFadeState: expanded
                ? CrossFadeState.showSecond
                : CrossFadeState.showFirst,
            duration: const Duration(milliseconds: 200),
          ),
        ],
      ),
    );
  }
}

class _TechnicalRow extends StatelessWidget {
  const _TechnicalRow({
    required this.label,
    required this.value,
    required this.textTheme,
  });

  final String label;
  final String value;
  final TextTheme textTheme;

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        SizedBox(
          width: 130,
          child: Text(
            label,
            style: textTheme.bodySmall?.copyWith(
              color: Colors.white.withValues(alpha: 0.45),
            ),
          ),
        ),
        Expanded(
          child: Text(
            value,
            style: textTheme.bodySmall?.copyWith(
              color: Colors.white.withValues(alpha: 0.80),
              fontWeight: FontWeight.w500,
            ),
            textAlign: TextAlign.end,
          ),
        ),
      ],
    );
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// Action buttons — Download Receipt | Send Receipt | Contact Support
// ═══════════════════════════════════════════════════════════════════════════

class _ActionButtons extends StatelessWidget {
  const _ActionButtons({required this.colors, required this.textTheme});

  final PayaboColorResolver colors;
  final TextTheme textTheme;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: <Widget>[
        _ActionButton(
          icon: Icons.download_rounded,
          label: 'Download Receipt',
          colors: colors,
          textTheme: textTheme,
          onTap: () {
            // TODO: implement download receipt
          },
        ),
        const SizedBox(height: PayaboSpacing.md),
        _ActionButton(
          icon: Icons.send_rounded,
          label: 'Send Receipt',
          colors: colors,
          textTheme: textTheme,
          onTap: () {
            // TODO: implement send receipt
          },
        ),
        const SizedBox(height: PayaboSpacing.md),
        _ActionButton(
          icon: Icons.support_agent_rounded,
          label: 'Contact Support',
          colors: colors,
          textTheme: textTheme,
          onTap: () {
            // TODO: implement contact support
          },
        ),
      ],
    );
  }
}

class _ActionButton extends StatelessWidget {
  const _ActionButton({
    required this.icon,
    required this.label,
    required this.colors,
    required this.textTheme,
    required this.onTap,
  });

  final IconData icon;
  final String label;
  final PayaboColorResolver colors;
  final TextTheme textTheme;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        borderRadius: PayaboRadii.radiusLg,
        child: Ink(
          width: double.infinity,
          padding: const EdgeInsets.symmetric(
            vertical: PayaboSpacing.lg,
          ),
          decoration: BoxDecoration(
            borderRadius: PayaboRadii.radiusLg,
            border: Border.all(
              color: colors.primary.withValues(alpha: 0.50),
              width: 1.5,
            ),
          ),
          child: Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: <Widget>[
              Icon(icon, color: colors.primary, size: 20),
              const SizedBox(width: PayaboSpacing.sm),
              Text(
                label,
                style: textTheme.titleSmall?.copyWith(
                  color: colors.primary,
                  fontWeight: FontWeight.w700,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
