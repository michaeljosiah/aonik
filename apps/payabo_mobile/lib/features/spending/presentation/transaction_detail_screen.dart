import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../app/demo/demo_data_mode.dart';
import '../../../data/repositories/personal_transactions_repository.dart';
import '../../../data/repositories/repository_providers.dart';
import '../../../data/repositories/spending_repository.dart';
import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_radii.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/attachment_picker_sheet.dart';
import '../../../shared/widgets/payabo_card.dart';
import 'spending_accounts_state.dart';
import 'widgets/category_selection_sheet.dart';

// ─────────────────────────────────────────────────────────
//  Providers
// ─────────────────────────────────────────────────────────

/// Provides merchant history stats from the spending repository.
///
/// Watches [accountLinksSummaryProvider] so that connect / disconnect
/// actions automatically invalidate this provider.
final _merchantHistoryFutureProvider =
    FutureProvider.family<SpendingMerchantHistory, String>(
  (Ref ref, String merchantName) async {
    ref.watch(demoDataModeProvider);
    ref.watch(accountLinksSummaryProvider);
    final repository = ref.watch(spendingRepositoryProvider);
    return repository.getMerchantHistory(merchantName);
  },
);

/// Lazily loads attachments for a single transaction on the detail screen.
final _transactionAttachmentsFutureProvider =
    FutureProvider.family<List<Attachment>, String>(
  (Ref ref, String transactionId) async {
    final repository = ref.watch(attachmentRepositoryProvider);
    return repository.getTransactionAttachments(transactionId);
  },
);

// ─────────────────────────────────────────────────────────
//  Transaction detail screen
// ─────────────────────────────────────────────────────────

class TransactionDetailScreen extends ConsumerStatefulWidget {
  const TransactionDetailScreen({
    super.key,
    required this.transactionId,
    this.merchant,
    this.category,
    this.subCategory,
    this.amountLabel,
    this.amountMajor,
    this.amountMinor,
    this.currencySymbol,
    this.isCredit,
    this.iconText,
    this.iconCodePoint,
    this.iconFontFamily,
    this.date,
    this.notes,
  });

  final String transactionId;
  final String? merchant;
  final String? category;
  final String? subCategory;
  final String? amountLabel;
  final String? amountMajor;
  final String? amountMinor;
  final String? currencySymbol;
  final bool? isCredit;
  final String? iconText;
  final int? iconCodePoint;
  final String? iconFontFamily;
  final DateTime? date;
  final String? notes;

  @override
  ConsumerState<TransactionDetailScreen> createState() =>
      _TransactionDetailScreenState();
}

class _TransactionDetailScreenState
    extends ConsumerState<TransactionDetailScreen> {
  bool _excludeFromBudget = false;
  bool _didPersistCategoryChange = false;
  late String _currentCategory;
  String? _currentSubCategory;
  bool _isDeleting = false;

  // Deep-link fallback loading state
  bool _isLoadingFromApi = false;
  PersonalTransactionItem? _loadedTransaction;

  /// True when we have no display fields and must show a loading indicator
  /// until the API call completes.
  bool get _needsRemoteLoad => widget.merchant == null;

  /// Whether the current transaction was manually created and can be deleted.
  bool get _isManualTransaction => _loadedTransaction?.isManual ?? false;

  @override
  void initState() {
    super.initState();
    _currentCategory = widget.category ?? 'other';
    _currentSubCategory = widget.subCategory;

    // Always fetch the full transaction model so that fields not passed via
    // navigation extras (e.g. sourceType) are available.  When display fields
    // were already provided via extras, _needsRemoteLoad is false so the UI
    // renders instantly while this runs in the background.
    if (widget.transactionId.isNotEmpty) {
      _loadTransactionFromApi();
    }
  }

  Future<void> _loadTransactionFromApi() async {
    setState(() => _isLoadingFromApi = true);
    try {
      final repo = ref.read(personalTransactionsRepositoryProvider);
      final txn = await repo.getTransaction(widget.transactionId);
      if (!mounted) return;
      if (txn != null) {
        setState(() {
          _loadedTransaction = txn;
          // Only overwrite category from API when we had no display data
          // (deep-link case).  Normal navigation already passed the category.
          if (_needsRemoteLoad) {
            _currentCategory =
                txn.category.isNotEmpty ? txn.category : 'other';
            _currentSubCategory = txn.subCategory;
          }
        });
      }
    } catch (_) {
      // Fallback to placeholder values
    } finally {
      if (mounted) setState(() => _isLoadingFromApi = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    // Only show a full-screen loading indicator when we have no display fields
    // at all (deep-link scenario).  When navigating from the list, display
    // fields are available immediately and the API call runs in the background.
    if (_isLoadingFromApi && _needsRemoteLoad) {
      return Scaffold(
        backgroundColor: c.surfaceWarm,
        body: const Center(child: CircularProgressIndicator()),
      );
    }

    final txn = _loadedTransaction;
    final String merchant = widget.merchant ?? txn?.merchant ?? 'Unknown';
    final String amountMajor = widget.amountMajor ?? txn?.amountMajor ?? '0';
    final String amountMinor = widget.amountMinor ?? txn?.amountMinor ?? '.00';
    final String currencySymbol = widget.currencySymbol ?? '\u00A3';
    final bool isCredit = widget.isCredit ?? txn?.isCredit ?? false;
    final DateTime date = widget.date ?? txn?.occurredAt ?? DateTime.now();

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
                    onTap: () => context.pop(_didPersistCategoryChange),
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
                      iconCodePoint: widget.iconCodePoint,
                      iconFontFamily: widget.iconFontFamily,
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
                      subCategory: _currentSubCategory,
                      onTap: () => _showCategorySheet(context),
                    ),

                    // ── Notes card (hidden when empty) ─────
                    if (widget.notes != null &&
                        widget.notes!.isNotEmpty) ...<Widget>[
                      const SizedBox(height: PayaboSpacing.lg),
                      _NotesCard(notes: widget.notes!),
                    ],

                    const SizedBox(height: PayaboSpacing.lg),

                    // ── Attachments card ────────────────────
                    _AttachmentsCard(
                      transactionId: widget.transactionId,
                    ),

                    const SizedBox(height: PayaboSpacing.lg),

                    // ── History card ────────────────────────
                    _HistoryCard(merchant: merchant),

                    const SizedBox(height: PayaboSpacing.x3),

                    // ── Delete transaction (manual only) ────
                    if (_isManualTransaction) ...<Widget>[
                      Center(
                        child: TextButton.icon(
                          onPressed: _isDeleting
                              ? null
                              : () => _handleDeleteTransaction(context),
                          icon: _isDeleting
                              ? const SizedBox(
                                  width: 16,
                                  height: 16,
                                  child: CircularProgressIndicator(
                                    strokeWidth: 2,
                                  ),
                                )
                              : Icon(
                                  Icons.delete_outline,
                                  size: 20,
                                  color: c.danger,
                                ),
                          label: Text(
                            _isDeleting
                                ? 'Deleting...'
                                : 'Delete transaction',
                          ),
                          style: TextButton.styleFrom(
                            foregroundColor: c.danger,
                            textStyle: Theme.of(context)
                                .textTheme
                                .titleMedium
                                ?.copyWith(fontWeight: FontWeight.w700),
                          ),
                        ),
                      ),
                      const SizedBox(height: PayaboSpacing.md),
                    ],

                    // ── Mark as duplicate ───────────────────
                    Center(
                      child: TextButton(
                        onPressed: () {
                          ScaffoldMessenger.of(context)
                            ..hideCurrentSnackBar()
                            ..showSnackBar(
                              const SnackBar(
                                content: Text(
                                  'Mark as duplicate coming soon.',
                                ),
                              ),
                            );
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

  Future<void> _handleDeleteTransaction(BuildContext context) async {
    final colors = context.colors;
    final bool? confirmed = await showDialog<bool>(
      context: context,
      builder: (BuildContext dialogContext) => AlertDialog(
        title: const Text('Delete transaction?'),
        content: const Text(
          'This will permanently remove this transaction and reverse its effect on your account balance.',
        ),
        actions: <Widget>[
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(false),
            child: const Text('Cancel'),
          ),
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(true),
            style: TextButton.styleFrom(
              foregroundColor: colors.danger,
            ),
            child: const Text('Delete'),
          ),
        ],
      ),
    );

    if (confirmed != true || !mounted) return;

    setState(() => _isDeleting = true);
    try {
      final repo = ref.read(personalTransactionsRepositoryProvider);
      await repo.deleteTransaction(widget.transactionId);

      if (!mounted) return;

      // Refresh upstream providers so the list screen updates
      ref.invalidate(accountLinksSummaryProvider);

      // Navigate back, signalling that a change occurred
      context.pop(true);
    } catch (_) {
      if (!mounted) return;
      setState(() => _isDeleting = false);
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(
          const SnackBar(
            content: Text('Unable to delete this transaction right now.'),
          ),
        );
    }
  }

  void _showCategorySheet(BuildContext context) async {
    final String? result = await showCategorySelectionSheet(
      context: context,
      currentCategory: _currentCategory,
    );
    if (result != null && mounted) {
      setState(() {
        _currentCategory = result;
        // Subcategory is system-assigned; reset when the user overrides the
        // top-level category so we don't show a stale subcategory.
        _currentSubCategory = null;
      });
      // Persist the category change to the backend.
      try {
        await ref
            .read(spendingRepositoryProvider)
            .updateTransactionCategory(widget.transactionId, result);

        // Refresh spending/account-backed views so list rows pick up the
        // persisted category when the user navigates back.
        _didPersistCategoryChange = true;
        ref.invalidate(accountLinksSummaryProvider);
      } catch (_) {
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(
              content: Text('Could not save category. Change is local only.'),
            ),
          );
        }
      }
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
    this.iconCodePoint,
    this.iconFontFamily,
    this.iconText,
  });

  final String merchant;
  final DateTime date;
  final String amountMajor;
  final String amountMinor;
  final String currencySymbol;
  final bool isCredit;
  final int? iconCodePoint;
  final String? iconFontFamily;
  final String? iconText;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final textTheme = Theme.of(context).textTheme;

    // Resolve icon from code point + font family if available.
    final IconData? resolvedIcon = iconCodePoint != null
        ? IconData(iconCodePoint!, fontFamily: iconFontFamily)
        : null;

    // Icon circle: use merchant icon or first-letter avatar
    final Widget iconContent;
    if (resolvedIcon != null) {
      iconContent = Icon(resolvedIcon, color: c.primary, size: 28);
    } else {
      iconContent = Text(
        iconText ?? merchant[0],
        style: textTheme.headlineMedium?.copyWith(
          color: c.spendingMerchantIconDark,
          fontWeight: FontWeight.w700,
        ),
      );
    }

    final Color iconBg = resolvedIcon != null
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
    this.subCategory,
    required this.onTap,
  });

  final String category;
  final String? subCategory;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final textTheme = Theme.of(context).textTheme;

    final IconData icon = categoryIcon(category);

    // Build the display label: "Groceries · Supermarket" or just "Groceries"
    final String categoryLabel = categoryDisplayName(category);
    final String? subLabel = subCategoryDisplayName(category, subCategory);
    final String displayLabel =
        subLabel != null ? '$categoryLabel · $subLabel' : categoryLabel;

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
            Flexible(
              child: Container(
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
                      icon,
                      size: 18,
                      color: c.accentBrown,
                    ),
                    const SizedBox(width: PayaboSpacing.sm),
                    Flexible(
                      child: Text(
                        displayLabel,
                        style: textTheme.titleSmall?.copyWith(
                          color: c.accentBrown,
                          fontWeight: FontWeight.w600,
                        ),
                        overflow: TextOverflow.ellipsis,
                        maxLines: 1,
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Notes card
// ─────────────────────────────────────────────────────────

class _NotesCard extends StatelessWidget {
  const _NotesCard({required this.notes});

  final String notes;

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
              Icon(Icons.notes_outlined, size: 20, color: c.accentBrown),
              const SizedBox(width: PayaboSpacing.sm),
              Text(
                'Notes',
                style: textTheme.titleMedium?.copyWith(
                  color: c.accentBrown,
                  fontWeight: FontWeight.w700,
                ),
              ),
            ],
          ),
          const SizedBox(height: PayaboSpacing.md),
          Text(
            notes,
            style: textTheme.bodyMedium?.copyWith(
              color: c.muted,
              height: 1.5,
            ),
          ),
        ],
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Attachments card
// ─────────────────────────────────────────────────────────

class _AttachmentsCard extends ConsumerWidget {
  const _AttachmentsCard({required this.transactionId});

  final String transactionId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.colors;
    final textTheme = Theme.of(context).textTheme;
    final asyncAttachments =
        ref.watch(_transactionAttachmentsFutureProvider(transactionId));

    return PayaboCard(
      backgroundColor: c.spendingCardWarmElevated,
      padding: const EdgeInsets.all(PayaboSpacing.xl),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          // ── Header row ─────────────────────────────
          Row(
            children: <Widget>[
              Icon(Icons.attach_file_outlined, size: 20, color: c.accentBrown),
              const SizedBox(width: PayaboSpacing.sm),
              Expanded(
                child: Text(
                  'Attachments',
                  style: textTheme.titleMedium?.copyWith(
                    color: c.accentBrown,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ),
            ],
          ),

          const SizedBox(height: PayaboSpacing.md),

          // ── Async content ──────────────────────────
          asyncAttachments.when(
            loading: () => const Padding(
              padding: EdgeInsets.symmetric(vertical: PayaboSpacing.md),
              child: Center(
                child: SizedBox(
                  width: 20,
                  height: 20,
                  child: CircularProgressIndicator(strokeWidth: 2),
                ),
              ),
            ),
            error: (Object error, StackTrace stack) => Text(
              'Unable to load attachments.',
              style: textTheme.bodyMedium?.copyWith(color: c.muted),
            ),
            data: (List<Attachment> attachments) => Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                if (attachments.isNotEmpty)
                  Wrap(
                    spacing: PayaboSpacing.sm,
                    runSpacing: PayaboSpacing.sm,
                    children: attachments
                        .map((a) => _AttachmentChip(
                              attachment: a,
                              onDelete: () => _deleteAttachment(
                                context,
                                ref,
                                a.id,
                              ),
                            ))
                        .toList(growable: false),
                  ),
                if (attachments.isEmpty)
                  Text(
                    'No attachments yet',
                    style: textTheme.bodyMedium?.copyWith(color: c.muted),
                  ),
                const SizedBox(height: PayaboSpacing.md),
                // ── Attach file button ─────────────────
                InkWell(
                  onTap: () => _addAttachment(context, ref),
                  borderRadius: BorderRadius.circular(PayaboRadii.md),
                  child: Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: PayaboSpacing.lg,
                      vertical: PayaboSpacing.sm,
                    ),
                    decoration: BoxDecoration(
                      border: Border.all(color: c.borderStrong),
                      borderRadius: BorderRadius.circular(PayaboRadii.md),
                    ),
                    child: Row(
                      mainAxisSize: MainAxisSize.min,
                      children: <Widget>[
                        Icon(Icons.add, size: 18, color: c.accentBrown),
                        const SizedBox(width: PayaboSpacing.xs),
                        Text(
                          'Attach file',
                          style: textTheme.titleSmall?.copyWith(
                            color: c.accentBrown,
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                      ],
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

  Future<void> _addAttachment(BuildContext context, WidgetRef ref) async {
    final result = await showAttachmentPickerSheet(context: context);
    if (result == null) return;

    final repository = ref.read(attachmentRepositoryProvider);
    await repository.addTransactionAttachment(
      transactionId,
      result.filePath,
      result.fileName,
    );

    // Refresh the attachments list.
    ref.invalidate(_transactionAttachmentsFutureProvider(transactionId));
  }

  Future<void> _deleteAttachment(
    BuildContext context,
    WidgetRef ref,
    String attachmentId,
  ) async {
    final bool? confirmed = await showDialog<bool>(
      context: context,
      builder: (BuildContext dialogContext) => AlertDialog(
        title: const Text('Delete attachment?'),
        content: const Text(
          'This will permanently remove the attachment from this transaction.',
        ),
        actions: <Widget>[
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(false),
            child: const Text('Cancel'),
          ),
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(true),
            child: const Text('Delete'),
          ),
        ],
      ),
    );

    if (confirmed != true) return;

    final repository = ref.read(attachmentRepositoryProvider);
    await repository.deleteAttachment(attachmentId);

    // Refresh the attachments list.
    ref.invalidate(_transactionAttachmentsFutureProvider(transactionId));
  }
}

/// A compact chip representing a single attachment.
class _AttachmentChip extends StatelessWidget {
  const _AttachmentChip({
    required this.attachment,
    required this.onDelete,
  });

  final Attachment attachment;
  final VoidCallback onDelete;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final textTheme = Theme.of(context).textTheme;

    final IconData icon =
        attachment.isImage ? Icons.image_outlined : Icons.description_outlined;

    return GestureDetector(
      onLongPress: onDelete,
      child: Container(
        padding: const EdgeInsets.symmetric(
          horizontal: PayaboSpacing.md,
          vertical: PayaboSpacing.sm,
        ),
        decoration: BoxDecoration(
          color: c.surfaceWarmAccent,
          borderRadius: BorderRadius.circular(PayaboRadii.md),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Icon(icon, size: 18, color: c.accentBrown),
            const SizedBox(width: PayaboSpacing.xs),
            ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 140),
              child: Text(
                attachment.fileName,
                style: textTheme.bodySmall?.copyWith(
                  color: c.accentBrown,
                  fontWeight: FontWeight.w500,
                ),
                overflow: TextOverflow.ellipsis,
                maxLines: 1,
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

class _HistoryCard extends ConsumerWidget {
  const _HistoryCard({required this.merchant});

  final String merchant;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.colors;
    final textTheme = Theme.of(context).textTheme;
    final asyncHistory = ref.watch(_merchantHistoryFutureProvider(merchant));

    return PayaboCard(
      backgroundColor: c.spendingCardWarmElevated,
      padding: const EdgeInsets.all(PayaboSpacing.xl),
      child: asyncHistory.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (Object error, StackTrace stack) => Text(
          'Unable to load history.',
          style: textTheme.bodyMedium?.copyWith(color: c.muted),
        ),
        data: (SpendingMerchantHistory history) => Column(
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
            _HistoryRow(
              label: 'Number of transactions',
              value: history.transactionCountLabel,
            ),
            const SizedBox(height: PayaboSpacing.md),
            _HistoryRow(
              label: 'Average spend',
              value: history.averageSpendLabel,
            ),
            const SizedBox(height: PayaboSpacing.md),
            _HistoryRow(
              label: 'Total spent',
              value: history.totalSpentLabel,
              isBold: true,
            ),
          ],
        ),
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
          style:
              (isBold ? textTheme.titleSmall : textTheme.bodyMedium)?.copyWith(
            color: c.accentBrown,
            fontWeight: isBold ? FontWeight.w800 : FontWeight.w600,
          ),
        ),
      ],
    );
  }
}
