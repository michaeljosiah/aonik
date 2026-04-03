import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../data/repositories/repository_providers.dart';
import '../../../data/repositories/spending_repository.dart';
import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_radii.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/attachment_picker_sheet.dart';
import '../../../shared/widgets/payabo_button.dart';
import '../../../shared/widgets/payabo_screen_title_bar.dart';
import '../../../shared/widgets/payabo_warm_scaffold.dart';
import 'spending_accounts_state.dart';
import 'widgets/category_selection_sheet.dart';

class ManualTransactionCreateScreen extends ConsumerStatefulWidget {
  const ManualTransactionCreateScreen({
    required this.accountId,
    required this.currencySymbol,
    required this.currencyCode,
    required this.accountName,
    super.key,
  });

  final String accountId;
  final String currencySymbol;
  final String currencyCode;
  final String accountName;

  @override
  ConsumerState<ManualTransactionCreateScreen> createState() =>
      _ManualTransactionCreateScreenState();
}

class _ManualTransactionCreateScreenState
    extends ConsumerState<ManualTransactionCreateScreen> {
  final GlobalKey<FormState> _formKey = GlobalKey<FormState>();
  final TextEditingController _merchantController = TextEditingController();
  final TextEditingController _amountController = TextEditingController();
  final TextEditingController _notesController = TextEditingController();

  bool _isCredit = false;
  String _category = 'other';
  DateTime _date = DateTime.now();
  bool _isSubmitting = false;
  String? _errorMessage;

  /// Files selected by the user to attach after the transaction is created.
  final List<AttachmentPickerResult> _pendingAttachments =
      <AttachmentPickerResult>[];

  @override
  void dispose() {
    _merchantController.dispose();
    _amountController.dispose();
    _notesController.dispose();
    super.dispose();
  }

  Future<void> _pickCategory() async {
    final String? selected = await showCategorySelectionSheet(
      context: context,
      currentCategory: _category,
    );
    if (selected != null && mounted) {
      setState(() => _category = selected);
    }
  }

  Future<void> _pickDate() async {
    final DateTime? selected = await showDatePicker(
      context: context,
      initialDate: _date,
      firstDate: DateTime(2020),
      lastDate: DateTime.now(),
    );
    if (selected != null && mounted) {
      setState(() => _date = selected);
    }
  }

  Future<void> _handleSubmit() async {
    if (!_formKey.currentState!.validate()) {
      return;
    }

    setState(() {
      _isSubmitting = true;
      _errorMessage = null;
    });

    try {
      final double amount = double.parse(_amountController.text.trim());
      final String? notes = _notesController.text.trim().isNotEmpty
          ? _notesController.text.trim()
          : null;

      final SpendingRepository repository =
          ref.read(spendingRepositoryProvider);

      final SpendingTransaction created = await repository.addTransaction(
        widget.accountId,
        CreateTransactionRequest(
          merchant: _merchantController.text.trim(),
          amount: amount,
          currency: widget.currencyCode,
          category: _category,
          isCredit: _isCredit,
          date: _date,
          notes: notes,
        ),
      );

      // Upload any pending attachments to the newly created transaction.
      if (_pendingAttachments.isNotEmpty) {
        final attachmentRepo = ref.read(attachmentRepositoryProvider);
        await Future.wait(
          _pendingAttachments.map(
            (pending) => attachmentRepo.addTransactionAttachment(
              created.id,
              pending.filePath,
              pending.fileName,
            ),
          ),
        );
      }

      // Invalidate the transaction list so it re-fetches from the repository.
      ref.invalidate(accountLinksSummaryProvider);

      if (!mounted) {
        return;
      }

      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(
          SnackBar(
            content: Text(
              'Transaction added to ${widget.accountName}.',
            ),
          ),
        );

      context.pop();
    } catch (error) {
      if (!mounted) {
        return;
      }

      setState(() {
        _isSubmitting = false;
        _errorMessage = error.toString();
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    const List<String> monthNames = <String>[
      'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
      'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec',
    ];
    final String dateLabel =
        '${_date.day} ${monthNames[_date.month - 1]} ${_date.year}';

    return PayaboWarmScaffold(
      body: Column(
        children: <Widget>[
          PayaboScreenTitleBar(
            title: 'Add transaction',
            onBack: () => context.pop(),
          ),
          Expanded(
            child: ListView(
              padding: const EdgeInsets.symmetric(
                horizontal: PayaboSpacing.xl,
              ),
              children: <Widget>[
                _IntroCard(accountName: widget.accountName),
                const SizedBox(height: PayaboSpacing.xl),
                Form(
                  key: _formKey,
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      // ── Merchant name ─────────────────────
                      _FormField(
                        label: 'Merchant / description',
                        child: TextFormField(
                          controller: _merchantController,
                          enabled: !_isSubmitting,
                          textInputAction: TextInputAction.next,
                          decoration: _inputDecoration(
                            context,
                            hintText: 'e.g. Tesco, Salary, Rent',
                          ),
                          style: _inputTextStyle(context),
                          validator: (String? value) {
                            if (value == null || value.trim().isEmpty) {
                              return 'Enter a merchant or description';
                            }
                            return null;
                          },
                        ),
                      ),

                      const SizedBox(height: PayaboSpacing.lg),

                      // ── Amount ────────────────────────────
                      _FormField(
                        label: 'Amount',
                        child: TextFormField(
                          controller: _amountController,
                          enabled: !_isSubmitting,
                          keyboardType: const TextInputType.numberWithOptions(
                            decimal: true,
                          ),
                          textInputAction: TextInputAction.next,
                          inputFormatters: <TextInputFormatter>[
                            FilteringTextInputFormatter.allow(
                              RegExp(r'^\d*\.?\d{0,2}'),
                            ),
                          ],
                          decoration: _inputDecoration(
                            context,
                            hintText: '0.00',
                            prefixText: '${widget.currencySymbol} ',
                          ),
                          style: _inputTextStyle(context),
                          validator: (String? value) {
                            if (value == null || value.trim().isEmpty) {
                              return 'Enter an amount';
                            }
                            final double? parsed =
                                double.tryParse(value.trim());
                            if (parsed == null || parsed <= 0) {
                              return 'Enter a valid positive amount';
                            }
                            return null;
                          },
                        ),
                      ),

                      const SizedBox(height: PayaboSpacing.lg),

                      // ── Income / Expense toggle ───────────
                      _FormField(
                        label: 'Type',
                        child: Row(
                          children: <Widget>[
                            Expanded(
                              child: _SegmentButton(
                                label: 'Expense',
                                icon: Icons.arrow_upward_rounded,
                                isSelected: !_isCredit,
                                onTap: _isSubmitting
                                    ? null
                                    : () =>
                                        setState(() => _isCredit = false),
                              ),
                            ),
                            const SizedBox(width: PayaboSpacing.md),
                            Expanded(
                              child: _SegmentButton(
                                label: 'Income',
                                icon: Icons.arrow_downward_rounded,
                                isSelected: _isCredit,
                                onTap: _isSubmitting
                                    ? null
                                    : () =>
                                        setState(() => _isCredit = true),
                              ),
                            ),
                          ],
                        ),
                      ),

                      const SizedBox(height: PayaboSpacing.lg),

                      // ── Category ──────────────────────────
                      _FormField(
                        label: 'Category',
                        child: InkWell(
                          onTap: _isSubmitting ? null : _pickCategory,
                          borderRadius: PayaboRadii.radiusSm,
                          child: InputDecorator(
                            decoration: _inputDecoration(context),
                            child: Row(
                              children: <Widget>[
                                Expanded(
                                  child: Text(
                                    categoryDisplayName(_category),
                                    style: _inputTextStyle(context),
                                  ),
                                ),
                                Icon(
                                  Icons.chevron_right_rounded,
                                  color: c.textSecondary,
                                  size: 20,
                                ),
                              ],
                            ),
                          ),
                        ),
                      ),

                      const SizedBox(height: PayaboSpacing.lg),

                      // ── Date ──────────────────────────────
                      _FormField(
                        label: 'Date',
                        child: InkWell(
                          onTap: _isSubmitting ? null : _pickDate,
                          borderRadius: PayaboRadii.radiusSm,
                          child: InputDecorator(
                            decoration: _inputDecoration(context),
                            child: Row(
                              children: <Widget>[
                                Icon(
                                  Icons.calendar_today_outlined,
                                  color: c.textSecondary,
                                  size: 18,
                                ),
                                const SizedBox(width: PayaboSpacing.sm),
                                Expanded(
                                  child: Text(
                                    dateLabel,
                                    style: _inputTextStyle(context),
                                  ),
                                ),
                                Icon(
                                  Icons.chevron_right_rounded,
                                  color: c.textSecondary,
                                  size: 20,
                                ),
                              ],
                            ),
                          ),
                        ),
                      ),

                      const SizedBox(height: PayaboSpacing.lg),

                      // ── Notes (optional) ──────────────────
                      _FormField(
                        label: 'Notes (optional)',
                        child: TextFormField(
                          controller: _notesController,
                          enabled: !_isSubmitting,
                          textInputAction: TextInputAction.done,
                          maxLines: 3,
                          decoration: _inputDecoration(
                            context,
                            hintText: 'Any extra details...',
                          ),
                          style: _inputTextStyle(context),
                        ),
                      ),

                      const SizedBox(height: PayaboSpacing.lg),

                      // ── Attach receipt (optional) ─────────
                      _FormField(
                        label: 'Attachments (optional)',
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: <Widget>[
                            if (_pendingAttachments.isNotEmpty) ...<Widget>[
                              Wrap(
                                spacing: PayaboSpacing.sm,
                                runSpacing: PayaboSpacing.sm,
                                children: _pendingAttachments
                                    .asMap()
                                    .entries
                                    .map(
                                      (entry) => _PendingAttachmentChip(
                                        fileName: entry.value.fileName,
                                        onRemove: _isSubmitting
                                            ? null
                                            : () {
                                                setState(() {
                                                  _pendingAttachments
                                                      .removeAt(entry.key);
                                                });
                                              },
                                      ),
                                    )
                                    .toList(growable: false),
                              ),
                              const SizedBox(height: PayaboSpacing.sm),
                            ],
                            InkWell(
                              onTap: _isSubmitting
                                  ? null
                                  : () async {
                                      final result =
                                          await showAttachmentPickerSheet(
                                        context: context,
                                      );
                                      if (result != null && mounted) {
                                        setState(() {
                                          _pendingAttachments.add(result);
                                        });
                                      }
                                    },
                              borderRadius: PayaboRadii.radiusSm,
                              child: Container(
                                width: double.infinity,
                                padding: const EdgeInsets.symmetric(
                                  horizontal: PayaboSpacing.lg,
                                  vertical: PayaboSpacing.md,
                                ),
                                decoration: BoxDecoration(
                                  border: Border.all(color: c.borderWarm),
                                  borderRadius: PayaboRadii.radiusSm,
                                ),
                                child: Row(
                                  mainAxisAlignment: MainAxisAlignment.center,
                                  children: <Widget>[
                                    Icon(
                                      Icons.attach_file_outlined,
                                      size: 18,
                                      color: c.textSecondary,
                                    ),
                                    const SizedBox(width: PayaboSpacing.sm),
                                    Text(
                                      _pendingAttachments.isEmpty
                                          ? 'Attach receipt or document'
                                          : 'Attach another file',
                                      style: Theme.of(context)
                                          .textTheme
                                          .titleSmall
                                          ?.copyWith(
                                            color: c.textSecondary,
                                            fontWeight: FontWeight.w500,
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
                ),

                // ── Error message ─────────────────────────
                if (_errorMessage != null) ...<Widget>[
                  const SizedBox(height: PayaboSpacing.lg),
                  Container(
                    width: double.infinity,
                    decoration: BoxDecoration(
                      color: c.warning.withValues(alpha: 0.12),
                      borderRadius: BorderRadius.circular(PayaboRadii.lg),
                      border: Border.all(
                        color: c.warning.withValues(alpha: 0.3),
                      ),
                    ),
                    padding: const EdgeInsets.all(PayaboSpacing.md),
                    child: Text(
                      _errorMessage!,
                      style: Theme.of(context).textTheme.bodySmall?.copyWith(
                            color: c.accentBrown,
                            height: 1.4,
                          ),
                    ),
                  ),
                ],

                const SizedBox(height: PayaboSpacing.xl),

                // ── Submitting indicator ──────────────────
                if (_isSubmitting) ...<Widget>[
                  Row(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: <Widget>[
                      const SizedBox(
                        width: 18,
                        height: 18,
                        child: CircularProgressIndicator(strokeWidth: 2.2),
                      ),
                      const SizedBox(width: PayaboSpacing.sm),
                      Text(
                        'Adding transaction...',
                        style: Theme.of(context).textTheme.bodySmall?.copyWith(
                              color: c.accentBrownMuted,
                            ),
                      ),
                    ],
                  ),
                  const SizedBox(height: PayaboSpacing.lg),
                ],

                // ── Submit button ─────────────────────────
                SizedBox(
                  width: double.infinity,
                  child: PayaboButton(
                    label: _isSubmitting ? 'Adding...' : 'Add transaction',
                    leading: _isSubmitting
                        ? null
                        : const Icon(Icons.add, size: 18),
                    onPressed: _isSubmitting ? null : _handleSubmit,
                  ),
                ),

                const SizedBox(height: PayaboSpacing.md),

                // ── Cancel button ─────────────────────────
                SizedBox(
                  width: double.infinity,
                  child: PayaboButton(
                    label: 'Cancel',
                    variant: PayaboButtonVariant.link,
                    onPressed: _isSubmitting ? null : () => context.pop(),
                  ),
                ),

                const SizedBox(height: PayaboSpacing.x4),
              ],
            ),
          ),
        ],
      ),
    );
  }

  InputDecoration _inputDecoration(
    BuildContext context, {
    String? hintText,
    String? prefixText,
  }) {
    final c = context.colors;

    return InputDecoration(
      filled: true,
      fillColor: c.surfaceWarm,
      hintText: hintText,
      prefixText: prefixText,
      contentPadding: const EdgeInsets.symmetric(
        horizontal: PayaboSpacing.lg,
        vertical: PayaboSpacing.md,
      ),
      border: OutlineInputBorder(
        borderRadius: PayaboRadii.radiusSm,
        borderSide: BorderSide(color: c.borderWarm),
      ),
      enabledBorder: OutlineInputBorder(
        borderRadius: PayaboRadii.radiusSm,
        borderSide: BorderSide(color: c.borderWarm),
      ),
      focusedBorder: OutlineInputBorder(
        borderRadius: PayaboRadii.radiusSm,
        borderSide: BorderSide(color: c.primary, width: 1.4),
      ),
      errorBorder: OutlineInputBorder(
        borderRadius: PayaboRadii.radiusSm,
        borderSide: BorderSide(color: c.danger),
      ),
      focusedErrorBorder: OutlineInputBorder(
        borderRadius: PayaboRadii.radiusSm,
        borderSide: BorderSide(color: c.danger, width: 1.4),
      ),
      hintStyle: Theme.of(context).textTheme.bodyLarge?.copyWith(
            color: c.textSecondary.withValues(alpha: 0.5),
          ),
    );
  }

  TextStyle? _inputTextStyle(BuildContext context) {
    final c = context.colors;
    return Theme.of(context).textTheme.bodyLarge?.copyWith(
          color: c.ink,
        );
  }
}

// ─────────────────────────────────────────────────────────
//  Intro card
// ─────────────────────────────────────────────────────────

class _IntroCard extends StatelessWidget {
  const _IntroCard({required this.accountName});

  final String accountName;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Container(
      decoration: BoxDecoration(
        color: c.spendingCardWarmElevated,
        borderRadius: PayaboRadii.radiusSm,
        border: Border.all(color: c.spendingQuickActionBorder),
      ),
      padding: const EdgeInsets.all(PayaboSpacing.xl),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            children: <Widget>[
              Container(
                width: 48,
                height: 48,
                decoration: BoxDecoration(
                  color: c.primary.withValues(alpha: 0.12),
                  borderRadius: BorderRadius.circular(18),
                ),
                child: Icon(
                  Icons.add_card_outlined,
                  color: c.primary,
                  size: 24,
                ),
              ),
              const SizedBox(width: PayaboSpacing.md),
              Expanded(
                child: Text(
                  'New transaction',
                  style: Theme.of(context).textTheme.titleLarge?.copyWith(
                        color: c.accentBrown,
                        fontWeight: FontWeight.w700,
                      ),
                ),
              ),
            ],
          ),
          const SizedBox(height: PayaboSpacing.md),
          Text(
            'Add an income or expense to $accountName. '
            'Manual entries help you track spending on accounts '
            'that aren\u2019t linked to a bank.',
            style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                  color: c.accentBrownMuted,
                  height: 1.45,
                ),
          ),
        ],
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Income / Expense segmented toggle button
// ─────────────────────────────────────────────────────────

class _SegmentButton extends StatelessWidget {
  const _SegmentButton({
    required this.label,
    required this.icon,
    required this.isSelected,
    this.onTap,
  });

  final String label;
  final IconData icon;
  final bool isSelected;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    final Color bg = isSelected
        ? c.primary.withValues(alpha: 0.12)
        : c.surfaceWarm;
    final Color border = isSelected ? c.primary : c.borderWarm;
    final Color fg = isSelected ? c.primary : c.textSecondary;

    return InkWell(
      onTap: onTap,
      borderRadius: PayaboRadii.radiusSm,
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 180),
        padding: const EdgeInsets.symmetric(
          horizontal: PayaboSpacing.lg,
          vertical: PayaboSpacing.md,
        ),
        decoration: BoxDecoration(
          color: bg,
          borderRadius: PayaboRadii.radiusSm,
          border: Border.all(color: border, width: isSelected ? 1.4 : 1),
        ),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            Icon(icon, size: 18, color: fg),
            const SizedBox(width: PayaboSpacing.sm),
            Text(
              label,
              style: Theme.of(context).textTheme.titleSmall?.copyWith(
                    color: fg,
                    fontWeight: isSelected ? FontWeight.w700 : FontWeight.w500,
                  ),
            ),
          ],
        ),
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Reusable form field wrapper
// ─────────────────────────────────────────────────────────

class _FormField extends StatelessWidget {
  const _FormField({
    required this.label,
    required this.child,
  });

  final String label;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Text(
          label,
          style: Theme.of(context).textTheme.labelMedium?.copyWith(
                color: c.accentBrown,
                fontWeight: FontWeight.w700,
              ),
        ),
        const SizedBox(height: PayaboSpacing.sm),
        child,
      ],
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Pending attachment chip (shown on the create screen)
// ─────────────────────────────────────────────────────────

class _PendingAttachmentChip extends StatelessWidget {
  const _PendingAttachmentChip({
    required this.fileName,
    this.onRemove,
  });

  final String fileName;
  final VoidCallback? onRemove;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final textTheme = Theme.of(context).textTheme;

    final String ext = fileName.split('.').last.toLowerCase();
    final bool isImage = const <String>{
      'jpg',
      'jpeg',
      'png',
      'gif',
      'webp',
    }.contains(ext);
    final IconData icon =
        isImage ? Icons.image_outlined : Icons.description_outlined;

    return Container(
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
          Icon(icon, size: 16, color: c.accentBrown),
          const SizedBox(width: PayaboSpacing.xs),
          ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 140),
            child: Text(
              fileName,
              style: textTheme.bodySmall?.copyWith(
                color: c.accentBrown,
                fontWeight: FontWeight.w500,
              ),
              overflow: TextOverflow.ellipsis,
              maxLines: 1,
            ),
          ),
          if (onRemove != null) ...<Widget>[
            const SizedBox(width: PayaboSpacing.xs),
            GestureDetector(
              onTap: onRemove,
              child: Icon(
                Icons.close,
                size: 16,
                color: c.accentBrown.withValues(alpha: 0.6),
              ),
            ),
          ],
        ],
      ),
    );
  }
}
