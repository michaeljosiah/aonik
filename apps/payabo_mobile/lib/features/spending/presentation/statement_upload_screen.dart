import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../data/repositories/account_links_repository.dart';
import '../../../data/repositories/repository_providers.dart';
import '../../../data/repositories/statement_import_repository.dart';
import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_radii.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_button.dart';
import '../../../shared/widgets/payabo_screen_title_bar.dart';
import '../../../shared/widgets/payabo_warm_scaffold.dart';
import 'spending_accounts_state.dart';

class StatementUploadScreen extends ConsumerStatefulWidget {
  const StatementUploadScreen({super.key, this.preselectedAccountId});

  final String? preselectedAccountId;

  @override
  ConsumerState<StatementUploadScreen> createState() =>
      _StatementUploadScreenState();
}

class _StatementUploadScreenState
    extends ConsumerState<StatementUploadScreen> {
  String? _selectedAccountId;
  PlatformFile? _selectedFile;
  bool _isUploading = false;
  String? _errorMessage;

  @override
  void initState() {
    super.initState();
    _selectedAccountId = widget.preselectedAccountId;
  }

  Future<void> _pickFile() async {
    try {
      final FilePickerResult? result = await FilePicker.platform.pickFiles(
        type: FileType.custom,
        allowedExtensions: <String>['csv', 'CSV'],
        allowMultiple: false,
      );

      if (result != null && result.files.isNotEmpty) {
        setState(() {
          _selectedFile = result.files.first;
          _errorMessage = null;
        });
      }
    } catch (error) {
      setState(() {
        _errorMessage = 'Could not open file picker: $error';
      });
    }
  }

  Future<void> _handleUpload() async {
    if (_selectedAccountId == null) {
      setState(() {
        _errorMessage = 'Please select an account.';
      });
      return;
    }

    if (_selectedFile == null || _selectedFile!.path == null) {
      setState(() {
        _errorMessage = 'Please select a CSV file.';
      });
      return;
    }

    setState(() {
      _isUploading = true;
      _errorMessage = null;
    });

    try {
      final StatementImportRepository repository =
          ref.read(statementImportRepositoryProvider);

      final StatementImportItem result = await repository.uploadStatement(
        personalAccountId: _selectedAccountId!,
        filePath: _selectedFile!.path!,
        fileName: _selectedFile!.name,
      );

      if (!mounted) return;

      if (result.isFailed) {
        setState(() {
          _isUploading = false;
          _errorMessage = result.failureReason ??
              'The file could not be parsed. Check the CSV format.';
        });
        return;
      }

      context.push(
        '/spending/accounts/upload-statement/${result.statementImportId}/review',
      );
    } catch (error) {
      if (!mounted) return;
      setState(() {
        _isUploading = false;
        _errorMessage = error.toString();
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final summaryAsync = ref.watch(accountLinksSummaryProvider);

    return PayaboWarmScaffold(
      body: Column(
        children: <Widget>[
          PayaboScreenTitleBar(
            title: 'Upload statement',
            onBack: () => context.go('/spending/accounts'),
          ),
          Expanded(
            child: ListView(
              padding: const EdgeInsets.symmetric(
                horizontal: PayaboSpacing.xl,
              ),
              children: <Widget>[
                const _IntroCard(),
                const SizedBox(height: PayaboSpacing.xl),

                // ── Account selector ───────────────────────
                _SectionLabel(label: 'Account'),
                const SizedBox(height: PayaboSpacing.sm),
                summaryAsync.when(
                  loading: () => const Center(
                    child: Padding(
                      padding: EdgeInsets.all(PayaboSpacing.lg),
                      child: CircularProgressIndicator(strokeWidth: 2.2),
                    ),
                  ),
                  error: (Object error, _) => _ErrorCard(
                    message: 'Could not load accounts: $error',
                  ),
                  data: (AccountLinksSummary summary) {
                    final List<AccountLinkItem> accounts = summary.accounts;

                    if (accounts.isEmpty) {
                      return _ErrorCard(
                        message:
                            'You have no accounts yet. Create or link an account first.',
                      );
                    }

                    // Validate preselected account still exists
                    if (_selectedAccountId != null &&
                        !accounts.any(
                          (AccountLinkItem a) => a.id == _selectedAccountId,
                        )) {
                      _selectedAccountId = null;
                    }

                    return Container(
                      decoration: BoxDecoration(
                        color: c.surfaceWarm,
                        borderRadius: PayaboRadii.radiusSm,
                        border: Border.all(color: c.borderWarm),
                      ),
                      padding: const EdgeInsets.symmetric(
                        horizontal: PayaboSpacing.lg,
                      ),
                      child: DropdownButtonHideUnderline(
                        child: DropdownButton<String>(
                          value: _selectedAccountId,
                          hint: Text(
                            'Select an account',
                            style: Theme.of(context)
                                .textTheme
                                .bodyLarge
                                ?.copyWith(
                                  color:
                                      c.textSecondary.withValues(alpha: 0.5),
                                ),
                          ),
                          isExpanded: true,
                          dropdownColor: c.surfaceWarm,
                          style: Theme.of(context)
                              .textTheme
                              .bodyLarge
                              ?.copyWith(color: c.ink),
                          items: accounts
                              .map(
                                (AccountLinkItem account) =>
                                    DropdownMenuItem<String>(
                                  value: account.id,
                                  child: Text(
                                    '${account.name} (${account.currencyCode})',
                                  ),
                                ),
                              )
                              .toList(growable: false),
                          onChanged: _isUploading
                              ? null
                              : (String? value) {
                                  setState(() {
                                    _selectedAccountId = value;
                                    _errorMessage = null;
                                  });
                                },
                        ),
                      ),
                    );
                  },
                ),

                const SizedBox(height: PayaboSpacing.xl),

                // ── File picker ────────────────────────────
                _SectionLabel(label: 'CSV file'),
                const SizedBox(height: PayaboSpacing.sm),

                if (_selectedFile != null) ...<Widget>[
                  _FileCard(
                    fileName: _selectedFile!.name,
                    fileSize: _selectedFile!.size,
                    onRemove: _isUploading
                        ? null
                        : () {
                            setState(() {
                              _selectedFile = null;
                            });
                          },
                  ),
                  const SizedBox(height: PayaboSpacing.md),
                ],

                SizedBox(
                  width: double.infinity,
                  child: PayaboButton(
                    label: _selectedFile == null
                        ? 'Select CSV file'
                        : 'Change file',
                    variant: PayaboButtonVariant.secondary,
                    leading: Icon(
                      _selectedFile == null
                          ? Icons.upload_file_outlined
                          : Icons.swap_horiz,
                      size: 18,
                    ),
                    onPressed: _isUploading ? null : _pickFile,
                  ),
                ),

                const SizedBox(height: PayaboSpacing.md),
                Text(
                  'Accepted format: CSV with columns for date, amount, and description. Merchant and currency columns are optional.',
                  style: Theme.of(context).textTheme.bodySmall?.copyWith(
                        color: c.accentBrownMuted,
                        height: 1.4,
                      ),
                ),

                // ── Error ──────────────────────────────────
                if (_errorMessage != null) ...<Widget>[
                  const SizedBox(height: PayaboSpacing.lg),
                  _ErrorCard(message: _errorMessage!),
                ],

                const SizedBox(height: PayaboSpacing.xl),

                // ── Upload button ──────────────────────────
                if (_isUploading) ...<Widget>[
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
                        'Uploading & parsing...',
                        style:
                            Theme.of(context).textTheme.bodySmall?.copyWith(
                                  color: c.accentBrownMuted,
                                ),
                      ),
                    ],
                  ),
                  const SizedBox(height: PayaboSpacing.lg),
                ],

                SizedBox(
                  width: double.infinity,
                  child: PayaboButton(
                    label:
                        _isUploading ? 'Uploading...' : 'Upload & parse',
                    leading: _isUploading
                        ? null
                        : const Icon(Icons.cloud_upload_outlined, size: 18),
                    onPressed: (_isUploading ||
                            _selectedAccountId == null ||
                            _selectedFile == null)
                        ? null
                        : _handleUpload,
                  ),
                ),
                const SizedBox(height: PayaboSpacing.md),
                SizedBox(
                  width: double.infinity,
                  child: PayaboButton(
                    label: 'Cancel',
                    variant: PayaboButtonVariant.link,
                    onPressed: _isUploading
                        ? null
                        : () => context.go('/spending/accounts'),
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
}

// ─────────────────────────────────────────────────────────────
//  Intro Card
// ─────────────────────────────────────────────────────────────

class _IntroCard extends StatelessWidget {
  const _IntroCard();

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
                  Icons.upload_file_outlined,
                  color: c.primary,
                  size: 24,
                ),
              ),
              const SizedBox(width: PayaboSpacing.md),
              Expanded(
                child: Text(
                  'Import statement',
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
            'Upload a CSV bank statement to import transactions into an account. '
            'The file will be parsed automatically and you can review '
            'each transaction before confirming the import.',
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

// ─────────────────────────────────────────────────────────────
//  File Card
// ─────────────────────────────────────────────────────────────

class _FileCard extends StatelessWidget {
  const _FileCard({
    required this.fileName,
    required this.fileSize,
    this.onRemove,
  });

  final String fileName;
  final int fileSize;
  final VoidCallback? onRemove;

  String get _formattedSize {
    if (fileSize < 1024) return '$fileSize B';
    if (fileSize < 1024 * 1024) {
      return '${(fileSize / 1024).toStringAsFixed(1)} KB';
    }
    return '${(fileSize / (1024 * 1024)).toStringAsFixed(1)} MB';
  }

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Container(
      decoration: BoxDecoration(
        color: c.surfaceWarm,
        borderRadius: PayaboRadii.radiusSm,
        border: Border.all(color: c.borderWarm),
      ),
      padding: const EdgeInsets.all(PayaboSpacing.lg),
      child: Row(
        children: <Widget>[
          Icon(Icons.description_outlined, color: c.primary, size: 28),
          const SizedBox(width: PayaboSpacing.md),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  fileName,
                  style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                        color: c.accentBrown,
                        fontWeight: FontWeight.w600,
                      ),
                  overflow: TextOverflow.ellipsis,
                ),
                const SizedBox(height: 2),
                Text(
                  _formattedSize,
                  style: Theme.of(context).textTheme.bodySmall?.copyWith(
                        color: c.accentBrownMuted,
                      ),
                ),
              ],
            ),
          ),
          if (onRemove != null)
            IconButton(
              icon: Icon(Icons.close, color: c.accentBrownMuted, size: 20),
              onPressed: onRemove,
              tooltip: 'Remove file',
            ),
        ],
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────────
//  Section Label
// ─────────────────────────────────────────────────────────────

class _SectionLabel extends StatelessWidget {
  const _SectionLabel({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Text(
      label,
      style: Theme.of(context).textTheme.labelMedium?.copyWith(
            color: c.accentBrown,
            fontWeight: FontWeight.w700,
          ),
    );
  }
}

// ─────────────────────────────────────────────────────────────
//  Error Card (reused in review screen too)
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
