import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_radii.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_card.dart';
import 'widgets/category_selection_sheet.dart';

// ─────────────────────────────────────────────────────────
//  Transaction detail screen
// ─────────────────────────────────────────────────────────

class TransactionDetailScreen extends ConsumerStatefulWidget {
  const TransactionDetailScreen({
    super.key,
    required this.transactionId,
    this.merchant,
    this.category,
    this.amountLabel,
    this.amountMajor,
    this.amountMinor,
    this.currencySymbol,
    this.isCredit,
    this.iconText,
    this.icon,
    this.date,
  });

  final String transactionId;
  final String? merchant;
  final String? category;
  final String? amountLabel;
  final String? amountMajor;
  final String? amountMinor;
  final String? currencySymbol;
  final bool? isCredit;
  final String? iconText;
  final IconData? icon;
  final DateTime? date;

  @override
  ConsumerState<TransactionDetailScreen> createState() =>
      _TransactionDetailScreenState();
}

class _TransactionDetailScreenState
    extends ConsumerState<TransactionDetailScreen> {
  bool _excludeFromBudget = false;
  late String _currentCategory;

  @override
  void initState() {
    super.initState();
    _currentCategory = widget.category ?? 'General';
  }

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    final String merchant = widget.merchant ?? 'Unknown';
    final String amountMajor = widget.amountMajor ?? '0';
    final String amountMinor = widget.amountMinor ?? '.00';
    final String currencySymbol = widget.currencySymbol ?? '\u00A3';
    final bool isCredit = widget.isCredit ?? false;
    final DateTime date = widget.date ?? DateTime.now();

    return Scaffold(
      backgroundColor: c.surfaceWarm,
      body: DecoratedBox(
        decoration: BoxDecoration(gradient: c.warmScreenGradient),
        child: SafeArea(
          bottom: false,
          child: Column(
            children: <Widget>[
              // ── Back button ──────────────────────────────
              Padding(
                padding: const EdgeInsets.fromLTRB(
                  PayaboSpacing.lg,
                  PayaboSpacing.sm,
                  PayaboSpacing.lg,
                  0,
                ),
                child: Align(
                  alignment: Alignment.centerLeft,
                  child: InkWell(
                    onTap: () => context.pop(),
                    borderRadius: BorderRadius.circular(20),
                    child: Icon(
                      Icons.arrow_back,
                      size: 24,
                      color: c.accentBrown,
                    ),
                  ),
                ),
              ),

              // ── Body ─────────────────────────────────────
              Expanded(
                child: ListView(
                  padding: const EdgeInsets.fromLTRB(
                    PayaboSpacing.xl,
                    PayaboSpacing.lg,
                    PayaboSpacing.xl,
                    PayaboSpacing.x4,
                  ),
                  children: <Widget>[
                    // ── Merchant header ─────────────────────
                    _TransactionHeader(
                      merchant: merchant,
                      date: date,
                      amountMajor: amountMajor,
                      amountMinor: amountMinor,
                      currencySymbol: currencySymbol,
                      isCredit: isCredit,
                      icon: widget.icon,
                      iconText: widget.iconText,
                    ),

                    const SizedBox(height: PayaboSpacing.xl),

                    // ── Status card ─────────────────────────
                    const _StatusCard(),

                    const SizedBox(height: PayaboSpacing.lg),

                    // ── Exclude from budget card ────────────
                    _ExcludeFromBudgetCard(
                      value: _excludeFromBudget,
                      onChanged: (bool value) {
                        setState(() => _excludeFromBudget = value);
                      },
                    ),

                    const SizedBox(height: PayaboSpacing.lg),

                    // ── Category card ───────────────────────
                    _CategoryCard(
                      category: _currentCategory,
                      onTap: () => _showCategorySheet(context),
                    ),

                    const SizedBox(height: PayaboSpacing.lg),

                    // ── History card ────────────────────────
                    _HistoryCard(merchant: merchant),

                    const SizedBox(height: PayaboSpacing.x3),

                    // ── Mark as duplicate ───────────────────
                    Center(
                      child: TextButton(
                        onPressed: () {
                          // TODO: implement mark as duplicate
                        },
                        style: TextButton.styleFrom(
                          foregroundColor: c.accentBrown,
                          textStyle:
                              Theme.of(context).textTheme.titleMedium?.copyWith(
                                    fontWeight: FontWeight.w700,
                                  ),
                        ),
                        child: const Text('Mark as duplicate'),
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  void _showCategorySheet(BuildContext context) async {
    final String? result = await showCategorySelectionSheet(
      context: context,
      currentCategory: _currentCategory,
    );
    if (result != null && mounted) {
      setState(() => _currentCategory = result);
    }
  }
}

// ─────────────────────────────────────────────────────────
//  Transaction header (icon + merchant + date + amount)
// ─────────────────────────────────────────────────────────

class _TransactionHeader extends StatelessWidget {
  const _TransactionHeader({
    required this.merchant,
    required this.date,
    required this.amountMajor,
    required this.amountMinor,
    required this.currencySymbol,
    required this.isCredit,
    this.icon,
    this.iconText,
  });

  final String merchant;
  final DateTime date;
  final String amountMajor;
  final String amountMinor;
  final String currencySymbol;
  final bool isCredit;
  final IconData? icon;
  final String? iconText;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final textTheme = Theme.of(context).textTheme;

    // Icon circle: use merchant icon or first-letter avatar
    final Widget iconContent;
    if (icon != null) {
      iconContent = Icon(icon, color: c.primary, size: 28);
    } else {
      iconContent = Text(
        iconText ?? merchant[0],
        style: textTheme.headlineMedium?.copyWith(
          color: c.spendingMerchantIconDark,
          fontWeight: FontWeight.w700,
        ),
      );
    }

    final Color iconBg = icon != null
        ? c.primary.withValues(alpha: 0.12)
        : c.spendingMerchantIconWarmSurface;

    // Format date
    final String dateLabel = _formatDate(date);

    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        // ── Icon circle ──────────────────────────
        Container(
          width: 56,
          height: 56,
          decoration: BoxDecoration(
            color: iconBg,
            shape: BoxShape.circle,
          ),
          alignment: Alignment.center,
          child: iconContent,
        ),

        const SizedBox(width: PayaboSpacing.lg),

        // ── Merchant + date ─────────────────────
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Text(
                merchant,
                style: textTheme.headlineMedium?.copyWith(
                  color: c.accentBrown,
                  fontWeight: FontWeight.w700,
                ),
              ),
              const SizedBox(height: PayaboSpacing.xxs),
              Text(
                dateLabel,
                style: textTheme.bodyMedium?.copyWith(
                  color: c.muted,
                ),
              ),
            ],
          ),
        ),

        // ── Amount ──────────────────────────────
        RichText(
          text: TextSpan(
            children: <InlineSpan>[
              TextSpan(
                text: currencySymbol,
                style: textTheme.titleLarge?.copyWith(
                  color: isCredit ? c.success : c.accentBrown,
                  fontWeight: FontWeight.w700,
                ),
              ),
              TextSpan(
                text: amountMajor,
                style: textTheme.displayLarge?.copyWith(
                  color: isCredit ? c.success : c.accentBrown,
                  fontWeight: FontWeight.w800,
                  height: 1,
                ),
              ),
              TextSpan(
                text: amountMinor,
                style: textTheme.titleMedium?.copyWith(
                  color: (isCredit ? c.success : c.accentBrown)
                      .withValues(alpha: 0.6),
                  fontWeight: FontWeight.w600,
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }

  String _formatDate(DateTime date) {
    const List<String> days = <String>[
      'Monday',
      'Tuesday',
      'Wednesday',
      'Thursday',
      'Friday',
      'Saturday',
      'Sunday',
    ];
    const List<String> months = <String>[
      'January',
      'February',
      'March',
      'April',
      'May',
      'June',
      'July',
      'August',
      'September',
      'October',
      'November',
      'December',
    ];
    return '${days[date.weekday - 1]}, ${date.day} ${months[date.month - 1]}';
  }
}

// ─────────────────────────────────────────────────────────
//  Status card
// ─────────────────────────────────────────────────────────

class _StatusCard extends StatelessWidget {
  const _StatusCard();

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final textTheme = Theme.of(context).textTheme;

    return PayaboCard(
      backgroundColor: c.spendingCardWarmElevated,
      padding: const EdgeInsets.all(PayaboSpacing.xl),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(
            'Status',
            style: textTheme.titleMedium?.copyWith(
              color: c.accentBrown,
              fontWeight: FontWeight.w700,
            ),
          ),
          const SizedBox(height: PayaboSpacing.sm),
          Text(
            'This transaction is now complete and cannot be reversed',
            style: textTheme.bodyMedium?.copyWith(
              color: c.muted,
              height: 1.4,
            ),
          ),
          const SizedBox(height: PayaboSpacing.md),
          Container(
            padding: const EdgeInsets.symmetric(
              horizontal: PayaboSpacing.md,
              vertical: PayaboSpacing.sm,
            ),
            decoration: BoxDecoration(
              color: c.successSoft,
              borderRadius: PayaboRadii.radiusPill,
            ),
            child: Text(
              'Completed',
              style: textTheme.labelLarge?.copyWith(
                color: c.success,
                fontWeight: FontWeight.w700,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Exclude from budget card
// ─────────────────────────────────────────────────────────

class _ExcludeFromBudgetCard extends StatelessWidget {
  const _ExcludeFromBudgetCard({
    required this.value,
    required this.onChanged,
  });

  final bool value;
  final ValueChanged<bool> onChanged;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final textTheme = Theme.of(context).textTheme;

    return PayaboCard(
      backgroundColor: c.spendingCardWarmElevated,
      padding: const EdgeInsets.all(PayaboSpacing.xl),
      child: Row(
        children: <Widget>[
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  'Exclude from budget',
                  style: textTheme.titleMedium?.copyWith(
                    color: c.accentBrown,
                    fontWeight: FontWeight.w700,
                  ),
                ),
                const SizedBox(height: PayaboSpacing.xs),
                Text(
                  'Excluding this transaction will remove it from all budget calculations',
                  style: textTheme.bodyMedium?.copyWith(
                    color: c.muted,
                    height: 1.4,
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(width: PayaboSpacing.md),
          Switch.adaptive(
            value: value,
            onChanged: onChanged,
            activeThumbColor: c.primary,
            inactiveThumbColor: c.borderStrong,
            inactiveTrackColor: c.surfaceMuted,
          ),
        ],
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Category card
// ─────────────────────────────────────────────────────────

class _CategoryCard extends StatelessWidget {
  const _CategoryCard({
    required this.category,
    required this.onTap,
  });

  final String category;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final textTheme = Theme.of(context).textTheme;

    final IconData categoryIcon = _categoryIcon(category);

    return GestureDetector(
      onTap: onTap,
      child: PayaboCard(
        backgroundColor: c.spendingCardWarmElevated,
        padding: const EdgeInsets.symmetric(
          horizontal: PayaboSpacing.xl,
          vertical: PayaboSpacing.lg,
        ),
        child: Row(
          children: <Widget>[
            Expanded(
              child: Text(
                'Category',
                style: textTheme.titleMedium?.copyWith(
                  color: c.accentBrown,
                  fontWeight: FontWeight.w700,
                ),
              ),
            ),
            Container(
              padding: const EdgeInsets.symmetric(
                horizontal: PayaboSpacing.md,
                vertical: PayaboSpacing.sm,
              ),
              decoration: BoxDecoration(
                color: c.surfaceWarmAccent,
                borderRadius: PayaboRadii.radiusPill,
              ),
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: <Widget>[
                  Icon(
                    categoryIcon,
                    size: 18,
                    color: c.accentBrown,
                  ),
                  const SizedBox(width: PayaboSpacing.sm),
                  Text(
                    category,
                    style: textTheme.titleSmall?.copyWith(
                      color: c.accentBrown,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────
//  History card
// ─────────────────────────────────────────────────────────

class _HistoryCard extends StatelessWidget {
  const _HistoryCard({required this.merchant});

  final String merchant;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final textTheme = Theme.of(context).textTheme;

    return PayaboCard(
      backgroundColor: c.spendingCardWarmElevated,
      padding: const EdgeInsets.all(PayaboSpacing.xl),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            children: <Widget>[
              Expanded(
                child: Text(
                  'History',
                  style: textTheme.titleMedium?.copyWith(
                    color: c.accentBrown,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ),
              Icon(
                Icons.chevron_right_rounded,
                color: c.accentBrown,
                size: 22,
              ),
            ],
          ),
          const SizedBox(height: PayaboSpacing.lg),
          const _HistoryRow(
            label: 'Number of transactions',
            value: '100',
          ),
          const SizedBox(height: PayaboSpacing.md),
          const _HistoryRow(
            label: 'Average spend',
            value: '\u00A326.97',
          ),
          const SizedBox(height: PayaboSpacing.md),
          const _HistoryRow(
            label: 'Total spent',
            value: '\u00A32,697.50',
            isBold: true,
          ),
        ],
      ),
    );
  }
}

class _HistoryRow extends StatelessWidget {
  const _HistoryRow({
    required this.label,
    required this.value,
    this.isBold = false,
  });

  final String label;
  final String value;
  final bool isBold;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final textTheme = Theme.of(context).textTheme;

    return Row(
      children: <Widget>[
        Expanded(
          child: Text(
            label,
            style: (isBold ? textTheme.titleSmall : textTheme.bodyMedium)
                ?.copyWith(
              color: isBold ? c.accentBrown : c.muted,
              fontWeight: isBold ? FontWeight.w700 : FontWeight.w400,
            ),
          ),
        ),
        Text(
          value,
          style: (isBold ? textTheme.titleSmall : textTheme.bodyMedium)
              ?.copyWith(
            color: c.accentBrown,
            fontWeight: isBold ? FontWeight.w800 : FontWeight.w600,
          ),
        ),
      ],
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Category icon mapping
// ─────────────────────────────────────────────────────────

IconData _categoryIcon(String category) {
  switch (category.toLowerCase()) {
    case 'housing':
      return Icons.home_outlined;
    case 'groceries':
      return Icons.shopping_cart_outlined;
    case 'eating out':
      return Icons.restaurant_outlined;
    case 'transport':
      return Icons.directions_car_outlined;
    case 'shopping':
      return Icons.shopping_bag_outlined;
    case 'entertainment':
      return Icons.movie_outlined;
    case 'bills':
      return Icons.receipt_long_outlined;
    case 'health':
      return Icons.favorite_outline;
    case 'education':
      return Icons.school_outlined;
    case 'personal care':
      return Icons.spa_outlined;
    case 'gifts':
      return Icons.card_giftcard_outlined;
    case 'travel':
      return Icons.flight_outlined;
    case 'savings':
      return Icons.savings_outlined;
    case 'subscriptions':
      return Icons.subscriptions_outlined;
    case 'charity':
      return Icons.volunteer_activism_outlined;
    case 'fitness':
      return Icons.fitness_center_outlined;
    case 'pets':
      return Icons.pets_outlined;
    case 'investments':
      return Icons.trending_up_outlined;
    default:
      return Icons.category_outlined;
  }
}
