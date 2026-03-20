import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../../data/repositories/repository_providers.dart';
import '../../../data/repositories/statement_import_repository.dart';
import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_radii.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_button.dart';
import '../../../shared/widgets/payabo_screen_title_bar.dart';
import '../../../shared/widgets/payabo_warm_scaffold.dart';
import 'spending_accounts_state.dart';

class StatementReviewScreen extends ConsumerStatefulWidget {
  const StatementReviewScreen({super.key, required this.importId});

  final String importId;

  @override
  ConsumerState<StatementReviewScreen> createState() =>
      _StatementReviewScreenState();
}

class _StatementReviewScreenState extends ConsumerState<StatementReviewScreen> {
  bool _isLoading = true;
  bool _isApplying = false;
  String? _errorMessage;
  StatementImportItem? _importItem;
  List<StatementImportRowItem> _rows = <StatementImportRowItem>[];

  @override
  void initState() {
    super.initState();
    _loadData();
  }

  Future<void> _loadData() async {
    setState(() {
      _isLoading = true;
      _errorMessage = null;
    });

    try {
      final StatementImportRepository repo =
          ref.read(statementImportRepositoryProvider);

      final Future<StatementImportItem?> importFuture =
          repo.getImport(widget.importId);
      final Future<List<StatementImportRowItem>> rowsFuture =
          repo.listImportRows(widget.importId);

      final StatementImportItem? importItem = await importFuture;
      final List<StatementImportRowItem> rows = await rowsFuture;

      if (!mounted) return;

      if (importItem == null) {
        setState(() {
          _isLoading = false;
          _errorMessage = 'Import not found. It may have been deleted.';
        });
        return;
      }

      setState(() {
        _isLoading = false;
        _importItem = importItem;
        _rows = rows;
      });
    } catch (error) {
      if (!mounted) return;
      setState(() {
        _isLoading = false;
        _errorMessage = error.toString();
      });
    }
  }

  Future<void> _handleApply() async {
    setState(() {
      _isApplying = true;
      _errorMessage = null;
    });

    try {
      final StatementImportRepository repo =
          ref.read(statementImportRepositoryProvider);

      final StatementImportApplyResult result =
          await repo.applyImport(widget.importId);

      ref.invalidate(accountLinksSummaryProvider);

      if (!mounted) return;

      context.push(
        '/spending/accounts/upload-statement/${result.statementImportId}/complete',
        extra: <String, dynamic>{
          'rowsImported': result.rowsImported,
          'rowsDuplicate': result.rowsDuplicate,
          'rowsFailed': result.rowsFailed,
          'status': result.status,
          'fileName': _importItem?.fileName ?? '',
        },
      );
    } catch (error) {
      if (!mounted) return;
      setState(() {
        _isApplying = false;
        _errorMessage = error.toString();
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return PayaboWarmScaffold(
      body: Column(
        children: <Widget>[
          PayaboScreenTitleBar(
            title: 'Review import',
            onBack: _isApplying ? null : () => context.pop(),
          ),
          if (_isLoading)
            const Expanded(
              child: Center(
                child: CircularProgressIndicator(strokeWidth: 2.2),
              ),
            )
          else if (_errorMessage != null && _importItem == null)
            Expanded(
              child: Padding(
                padding: const EdgeInsets.symmetric(
                  horizontal: PayaboSpacing.xl,
                ),
                child: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: <Widget>[
                    _ErrorCard(message: _errorMessage!),
                    const SizedBox(height: PayaboSpacing.xl),
                    SizedBox(
                      width: double.infinity,
                      child: PayaboButton(
                        label: 'Go back',
                        variant: PayaboButtonVariant.secondary,
                        onPressed: () => context.pop(),
                      ),
                    ),
                  ],
                ),
              ),
            )
          else
            Expanded(
              child: Column(
                children: <Widget>[
                  // ── Summary header ──────────────────────
                  _ImportSummaryCard(importItem: _importItem!),

                  // ── Row list ────────────────────────────
                  Expanded(
                    child: _rows.isEmpty
                        ? Center(
                            child: Text(
                              'No rows were parsed from this file.',
                              style: Theme.of(context)
                                  .textTheme
                                  .bodyMedium
                                  ?.copyWith(color: c.accentBrownMuted),
                            ),
                          )
                        : ListView.separated(
                            padding: const EdgeInsets.symmetric(
                              horizontal: PayaboSpacing.xl,
                              vertical: PayaboSpacing.md,
                            ),
                            itemCount: _rows.length,
                            separatorBuilder: (_, __) =>
                                const SizedBox(height: PayaboSpacing.sm),
                            itemBuilder: (BuildContext context, int index) {
                              return _RowTile(row: _rows[index]);
                            },
                          ),
                  ),

                  // ── Error + Actions ────────────────────
                  Container(
                    decoration: BoxDecoration(
                      color: c.surfaceWarm,
                      border: Border(
                        top: BorderSide(color: c.borderWarm),
                      ),
                    ),
                    padding: const EdgeInsets.fromLTRB(
                      PayaboSpacing.xl,
                      PayaboSpacing.lg,
                      PayaboSpacing.xl,
                      PayaboSpacing.x4,
                    ),
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      children: <Widget>[
                        if (_errorMessage != null) ...<Widget>[
                          _ErrorCard(message: _errorMessage!),
                          const SizedBox(height: PayaboSpacing.md),
                        ],
                        if (_isApplying) ...<Widget>[
                          Row(
                            mainAxisAlignment: MainAxisAlignment.center,
                            children: <Widget>[
                              const SizedBox(
                                width: 18,
                                height: 18,
                                child: CircularProgressIndicator(
                                  strokeWidth: 2.2,
                                ),
                              ),
                              const SizedBox(width: PayaboSpacing.sm),
                              Text(
                                'Importing transactions...',
                                style: Theme.of(context)
                                    .textTheme
                                    .bodySmall
                                    ?.copyWith(color: c.accentBrownMuted),
                              ),
                            ],
                          ),
                          const SizedBox(height: PayaboSpacing.md),
                        ],
                        SizedBox(
                          width: double.infinity,
                          child: PayaboButton(
                            label: _isApplying
                                ? 'Importing...'
                                : 'Import ${_importItem!.importableRows} transaction${_importItem!.importableRows == 1 ? '' : 's'}',
                            leading: _isApplying
                                ? null
                                : const Icon(Icons.check, size: 18),
                            onPressed: (_isApplying ||
                                    _importItem!.importableRows == 0)
                                ? null
                                : _handleApply,
                          ),
                        ),
                        const SizedBox(height: PayaboSpacing.sm),
                        SizedBox(
                          width: double.infinity,
                          child: PayaboButton(
                            label: 'Cancel',
                            variant: PayaboButtonVariant.link,
                            onPressed:
                                _isApplying ? null : () => context.pop(),
                          ),
                        ),
                      ],
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

// ─────────────────────────────────────────────────────────────
//  Import Summary Card
// ─────────────────────────────────────────────────────────────

class _ImportSummaryCard extends StatelessWidget {
  const _ImportSummaryCard({required this.importItem});

  final StatementImportItem importItem;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Container(
      margin: const EdgeInsets.symmetric(
        horizontal: PayaboSpacing.xl,
        vertical: PayaboSpacing.md,
      ),
      decoration: BoxDecoration(
        color: c.spendingCardWarmElevated,
        borderRadius: PayaboRadii.radiusSm,
        border: Border.all(color: c.spendingQuickActionBorder),
      ),
      padding: const EdgeInsets.all(PayaboSpacing.lg),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            children: <Widget>[
              Icon(Icons.description_outlined, color: c.primary, size: 22),
              const SizedBox(width: PayaboSpacing.sm),
              Expanded(
                child: Text(
                  importItem.fileName,
                  style: Theme.of(context).textTheme.titleSmall?.copyWith(
                        color: c.accentBrown,
                        fontWeight: FontWeight.w700,
                      ),
                  overflow: TextOverflow.ellipsis,
                ),
              ),
            ],
          ),
          const SizedBox(height: PayaboSpacing.md),
          Row(
            children: <Widget>[
              _StatChip(
                label: 'Parsed',
                count: importItem.rowsParsed,
                color: c.success,
              ),
              const SizedBox(width: PayaboSpacing.sm),
              _StatChip(
                label: 'Duplicate',
                count: importItem.rowsDuplicate,
                color: c.warning,
              ),
              const SizedBox(width: PayaboSpacing.sm),
              _StatChip(
                label: 'Failed',
                count: importItem.rowsFailed,
                color: c.danger,
              ),
            ],
          ),
          if (importItem.importableRows > 0) ...<Widget>[
            const SizedBox(height: PayaboSpacing.sm),
            Text(
              '${importItem.importableRows} transaction${importItem.importableRows == 1 ? '' : 's'} ready to import',
              style: Theme.of(context).textTheme.bodySmall?.copyWith(
                    color: c.accentBrownMuted,
                  ),
            ),
          ],
        ],
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────────
//  Stat Chip
// ─────────────────────────────────────────────────────────────

class _StatChip extends StatelessWidget {
  const _StatChip({
    required this.label,
    required this.count,
    required this.color,
  });

  final String label;
  final int count;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: PayaboSpacing.md,
        vertical: PayaboSpacing.xs,
      ),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(PayaboRadii.md),
      ),
      child: Text(
        '$count $label',
        style: Theme.of(context).textTheme.labelSmall?.copyWith(
              color: color,
              fontWeight: FontWeight.w600,
            ),
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────────
//  Row Tile
// ─────────────────────────────────────────────────────────────

class _RowTile extends StatelessWidget {
  const _RowTile({required this.row});

  final StatementImportRowItem row;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    final Color statusColor;
    final String statusLabel;
    final IconData statusIcon;

    if (row.isDuplicate) {
      statusColor = c.warning;
      statusLabel = 'Duplicate';
      statusIcon = Icons.content_copy;
    } else if (row.isFailed) {
      statusColor = c.danger;
      statusLabel = 'Failed';
      statusIcon = Icons.error_outline;
    } else {
      statusColor = c.success;
      statusLabel = 'Ready';
      statusIcon = Icons.check_circle_outline;
    }

    final String description =
        row.normalizedDescription ?? row.descriptionRaw ?? '—';
    final String amountText;
    if (row.normalizedAmount != null) {
      final String currency = row.normalizedCurrency ?? '';
      amountText =
          '${currency.isNotEmpty ? '$currency ' : ''}${row.normalizedAmount!.toStringAsFixed(2)}';
    } else {
      amountText = row.amountRaw ?? '—';
    }

    final String dateText;
    if (row.normalizedOccurredAt != null) {
      dateText = DateFormat('dd MMM yyyy').format(row.normalizedOccurredAt!);
    } else {
      dateText = row.occurredAtRaw ?? '—';
    }

    return Container(
      decoration: BoxDecoration(
        color: c.surfaceWarm,
        borderRadius: PayaboRadii.radiusSm,
        border: Border.all(color: c.borderWarm),
      ),
      padding: const EdgeInsets.all(PayaboSpacing.md),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          // Status icon
          Container(
            width: 28,
            height: 28,
            decoration: BoxDecoration(
              color: statusColor.withValues(alpha: 0.12),
              borderRadius: BorderRadius.circular(8),
            ),
            child: Icon(statusIcon, color: statusColor, size: 16),
          ),
          const SizedBox(width: PayaboSpacing.md),
          // Content
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  description,
                  style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                        color: c.accentBrown,
                        fontWeight: FontWeight.w600,
                      ),
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                ),
                const SizedBox(height: 2),
                Text(
                  dateText,
                  style: Theme.of(context).textTheme.bodySmall?.copyWith(
                        color: c.accentBrownMuted,
                      ),
                ),
                if (row.isFailed && row.errorMessage != null) ...<Widget>[
                  const SizedBox(height: 4),
                  Text(
                    row.errorMessage!,
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                          color: c.danger,
                          fontStyle: FontStyle.italic,
                        ),
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                  ),
                ],
              ],
            ),
          ),
          const SizedBox(width: PayaboSpacing.sm),
          // Amount + status label
          Column(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: <Widget>[
              Text(
                amountText,
                style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                      color: c.accentBrown,
                      fontWeight: FontWeight.w700,
                    ),
              ),
              const SizedBox(height: 2),
              Text(
                statusLabel,
                style: Theme.of(context).textTheme.labelSmall?.copyWith(
                      color: statusColor,
                      fontWeight: FontWeight.w600,
                    ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────────
//  Error Card
// ─────────────────────────────────────────────────────────────

class _ErrorCard extends StatelessWidget {
  const _ErrorCard({required this.message});

  final String message;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Container(
      width: double.infinity,
      decoration: BoxDecoration(
        color: c.warning.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(PayaboRadii.lg),
        border: Border.all(color: c.warning.withValues(alpha: 0.3)),
      ),
      padding: const EdgeInsets.all(PayaboSpacing.md),
      child: Text(
        message,
        style: Theme.of(context).textTheme.bodySmall?.copyWith(
              color: c.accentBrown,
              height: 1.4,
            ),
      ),
    );
  }
}
