import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter/scheduler.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../app/demo/demo_data_mode.dart';
import '../../../app/environment/environment_provider.dart';
import '../../../data/repositories/account_links_repository.dart';
import '../../../data/repositories/repository_providers.dart';
import '../../../shared/reference/payabo_country_reference.dart';
import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_radii.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_app_header.dart';
import '../../../shared/widgets/payabo_button.dart';
import '../../../shared/widgets/payabo_primary_app_shell.dart';
import '../../../shared/widgets/payabo_warm_scaffold.dart';
import 'account_link_connect_sheet.dart';
import 'spending_accounts_state.dart';
import 'widgets/spending_section_pills.dart';

const List<SpendingSection> _visibleSpendingSections = <SpendingSection>[
  SpendingSection.transactions,
  SpendingSection.budgets,
  SpendingSection.bills,
  SpendingSection.accounts,
];

class SpendingAccountsScreen extends ConsumerStatefulWidget {
  const SpendingAccountsScreen({super.key});

  @override
  ConsumerState<SpendingAccountsScreen> createState() =>
      _SpendingAccountsScreenState();
}

class _SpendingAccountsScreenState
    extends ConsumerState<SpendingAccountsScreen> {
  final ValueNotifier<double> _statusBarProgress = ValueNotifier<double>(0.0);

  @override
  void dispose() {
    _statusBarProgress.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final AsyncValue<AccountLinksSummary> summaryValue =
        ref.watch(accountLinksSummaryProvider);
    final AccountLinkFlowState flowState =
        ref.watch(accountLinkFlowControllerProvider);
    final bool isFreshDemo =
        ref.watch(demoDataModeProvider) == DemoDataMode.fresh;
    final bool isRefreshingSummary = summaryValue.isRefreshing;

    return summaryValue.when(
      data: (AccountLinksSummary summary) {
        if (!summary.hasAccounts) {
          return Scaffold(
            backgroundColor: c.surfaceWarm,
            body: Stack(
              fit: StackFit.expand,
              children: <Widget>[
                _AccountsEmptyLayout(
                  isFreshDemo: isFreshDemo,
                  onSectionSelected: _handleSectionSelected,
                  onConnectTap: () => _showConnectSheet(context, ref),
                  onUploadTap: () => _showUploadMessage(context),
                  onAddManualTap: () => _showManualMessage(context),
                ),
                if (isRefreshingSummary) const _AccountsRefreshingOverlay(),
              ],
            ),
            bottomNavigationBar: const PayaboPrimaryAppShell(
              destination: PayaboPrimaryDestination.spending,
            ),
          );
        }

        return PayaboWarmScaffold(
          backgroundDecoration: BoxDecoration(
            gradient: LinearGradient(
              colors: c.isDark
                  ? const <Color>[Color(0xFF1A1A1A), Color(0xFF121212)]
                  : const <Color>[Color(0xFF2C1810), Color(0xFF1A0E08)],
              begin: Alignment.topCenter,
              end: Alignment.bottomCenter,
            ),
          ),
          statusBarColorNotifier: _statusBarProgress,
          bottomNavigationBar: const PayaboPrimaryAppShell(
            destination: PayaboPrimaryDestination.spending,
          ),
          body: Stack(
            fit: StackFit.expand,
            children: <Widget>[
              _AccountsHeroAndSheet(
                summary: summary,
                flowState: flowState,
                onSectionSelected: _handleSectionSelected,
                onConnectTap: () => _showConnectSheet(context, ref),
                onUploadTap: () => _showUploadMessage(context),
                onAddManualTap: () => _showManualMessage(context),
                onReconnectTap: (AccountLinkItem item) =>
                    _handleReconnect(context, ref, item),
                onRefreshTap: (AccountLinkItem item) =>
                    _handleRefresh(context, ref, item),
                onDisconnectTap: (AccountLinkItem item) =>
                    _handleDisconnect(context, ref, item),
                onDeleteTap: (AccountLinkItem item) =>
                    _handleDeleteManualAccount(context, ref, item),
                onManageTap: (AccountLinkItem item) =>
                    _showManageMessage(context, item),
                onRefreshAll: () async {
                  ref.invalidate(accountLinksSummaryProvider);
                  await ref.read(accountLinksSummaryProvider.future);
                },
                onSheetExtentChanged: (double extent) {
                  _statusBarProgress.value = extent;
                },
              ),
              if (isRefreshingSummary) const _AccountsRefreshingOverlay(),
            ],
          ),
        );
      },
      loading: () => Scaffold(
        backgroundColor: c.surfaceWarm,
        body: const Center(child: CircularProgressIndicator()),
        bottomNavigationBar: const PayaboPrimaryAppShell(
          destination: PayaboPrimaryDestination.spending,
        ),
      ),
      error: (Object error, StackTrace stackTrace) {
        return Scaffold(
          backgroundColor: c.surfaceWarm,
          body: Center(
            child: Padding(
              padding: const EdgeInsets.all(PayaboSpacing.xl),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: <Widget>[
                  Icon(
                    Icons.error_outline_rounded,
                    size: 48,
                    color: c.muted,
                  ),
                  const SizedBox(height: PayaboSpacing.md),
                  Text(
                    'Unable to load accounts right now.',
                    style: Theme.of(context)
                        .textTheme
                        .bodyMedium
                        ?.copyWith(color: c.muted),
                    textAlign: TextAlign.center,
                  ),
                  const SizedBox(height: PayaboSpacing.lg),
                  TextButton(
                    onPressed: () =>
                        ref.invalidate(accountLinksSummaryProvider),
                    child: const Text('Try again'),
                  ),
                ],
              ),
            ),
          ),
          bottomNavigationBar: const PayaboPrimaryAppShell(
            destination: PayaboPrimaryDestination.spending,
          ),
        );
      },
    );
  }

  void _handleSectionSelected(SpendingSection section) {
    switch (section) {
      case SpendingSection.overview:
        context.go('/spending/overview');
        return;
      case SpendingSection.transactions:
        context.go('/spending');
        return;
      case SpendingSection.budgets:
        context.go('/spending/budgets');
        return;
      case SpendingSection.bills:
        context.go('/spending/bills');
        return;
      case SpendingSection.accounts:
        return;
    }
  }

  Future<void> _showConnectSheet(BuildContext context, WidgetRef ref) async {
    final environment = ref.read(appEnvironmentProvider);

    await _showAccountLinkSheet(
      context,
      ref,
      provider: environment.resolvedAccountLinkProvider,
      mode: 'connect',
      title: 'Connect bank account',
    );
  }

  Future<void> _showAccountLinkSheet(
    BuildContext context,
    WidgetRef ref, {
    required String provider,
    required String mode,
    required String title,
    String? connectionId,
  }) async {
    final AccountLinkExchangeResult? result = await showAccountLinkConnectSheet(
      context,
      ref,
      provider: provider,
      mode: mode,
      title: title,
      connectionId: connectionId,
      countries: payaboCountries,
    );

    if (!context.mounted || result == null) {
      return;
    }

    try {
      await ref.read(accountLinksSummaryProvider.future);
    } catch (_) {
      if (!context.mounted) {
        return;
      }
    }

    if (!context.mounted) {
      return;
    }

    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(
        SnackBar(
          content: Text(
            'Connected ${result.linkedAccountCount} account${result.linkedAccountCount == 1 ? '' : 's'} from ${result.institutionName}.',
          ),
        ),
      );
  }

  void _showUploadMessage(BuildContext context) {
    context.push('/spending/accounts/upload-statement');
  }

  void _showManualMessage(BuildContext context) {
    context.push('/spending/accounts/create-manual');
  }

  Future<void> _handleReconnect(
    BuildContext context,
    WidgetRef ref,
    AccountLinkItem item,
  ) async {
    if (item.connectionId == null) {
      _showMessage(context, 'This linked account is missing a connection id.');
      return;
    }

    if (!item.hasProvider) {
      _showMessage(
        context,
        'This linked account is missing provider details. Refresh the account list and try again.',
      );
      return;
    }

    await _showAccountLinkSheet(
      context,
      ref,
      provider: item.providerCode!,
      mode: 'update',
      connectionId: item.connectionId,
      title: 'Reconnect bank account',
    );
  }

  Future<void> _handleRefresh(
    BuildContext context,
    WidgetRef ref,
    AccountLinkItem item,
  ) async {
    if (item.connectionId == null) {
      _showMessage(context, 'This linked account is missing a connection id.');
      return;
    }

    try {
      final AccountLinkActionResult? result = await ref
          .read(accountLinkFlowControllerProvider.notifier)
          .refreshConnection(item.connectionId!);

      if (!context.mounted || result == null) {
        return;
      }

      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(
          SnackBar(
            content: Text(
              'Refreshed ${result.linkedAccountCount} linked account${result.linkedAccountCount == 1 ? '' : 's'} from ${result.institutionName}.',
            ),
          ),
        );
    } catch (_) {
      if (!context.mounted) {
        return;
      }

      final String? message =
          ref.read(accountLinkFlowControllerProvider).errorMessage;
      if (message != null) {
        _showMessage(context, message);
      }
    }
  }

  Future<void> _handleDisconnect(
    BuildContext context,
    WidgetRef ref,
    AccountLinkItem item,
  ) async {
    if (item.connectionId == null) {
      _showMessage(context, 'This linked account is missing a connection id.');
      return;
    }

    final bool? confirmed = await showDialog<bool>(
      context: context,
      builder: (BuildContext context) {
        final c = context.colors;

        return AlertDialog(
          backgroundColor: c.surfaceBase,
          title: Text(
            'Disconnect ${item.name.toLowerCase()}?',
            style: Theme.of(context).textTheme.titleLarge?.copyWith(
                  color: c.accentBrown,
                  fontWeight: FontWeight.w700,
                ),
          ),
          content: Text(
            'Payabo will stop syncing ${item.institutionName} and remove this linked account from active Spend views.',
            style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                  color: c.accentBrownMuted,
                ),
          ),
          actions: <Widget>[
            TextButton(
              onPressed: () => Navigator.of(context).pop(false),
              child: const Text('Cancel'),
            ),
            TextButton(
              onPressed: () => Navigator.of(context).pop(true),
              child: const Text('Disconnect'),
            ),
          ],
        );
      },
    );

    if (confirmed != true || !context.mounted) {
      return;
    }

    try {
      final AccountLinkActionResult? result = await ref
          .read(accountLinkFlowControllerProvider.notifier)
          .disconnectConnection(item.connectionId!);

      if (!context.mounted || result == null) {
        return;
      }

      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(
          SnackBar(
            content: Text(
              'Disconnected ${result.linkedAccountCount} linked account${result.linkedAccountCount == 1 ? '' : 's'} from ${result.institutionName}.',
            ),
          ),
        );
    } catch (_) {
      if (!context.mounted) {
        return;
      }

      final String? message =
          ref.read(accountLinkFlowControllerProvider).errorMessage;
      if (message != null) {
        _showMessage(context, message);
      }
    }
  }

  Future<void> _handleDeleteManualAccount(
    BuildContext context,
    WidgetRef ref,
    AccountLinkItem item,
  ) async {
    final c = context.colors;

    final bool? confirmed = await showDialog<bool>(
      context: context,
      builder: (BuildContext context) {
        return AlertDialog(
          backgroundColor: c.surfaceBase,
          title: Text(
            'Delete ${item.name.toLowerCase()}?',
            style: Theme.of(context).textTheme.titleLarge?.copyWith(
                  color: c.accentBrown,
                  fontWeight: FontWeight.w700,
                ),
          ),
          content: Text(
            'This will permanently delete this manual account and all its transactions. This action cannot be undone.',
            style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                  color: c.accentBrownMuted,
                ),
          ),
          actions: <Widget>[
            TextButton(
              onPressed: () => Navigator.of(context).pop(false),
              child: const Text('Cancel'),
            ),
            TextButton(
              onPressed: () => Navigator.of(context).pop(true),
              style: TextButton.styleFrom(foregroundColor: c.danger),
              child: const Text('Delete'),
            ),
          ],
        );
      },
    );

    if (confirmed != true || !context.mounted) {
      return;
    }

    try {
      final AccountLinksRepository repository =
          ref.read(accountLinksRepositoryProvider);
      await repository.deleteManualAccount(item.id);

      ref.invalidate(accountLinksSummaryProvider);

      if (!context.mounted) {
        return;
      }

      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(
          SnackBar(
            content: Text('Deleted ${item.name} and all its transactions.'),
          ),
        );
    } catch (_) {
      if (!context.mounted) {
        return;
      }

      _showMessage(
        context,
        'Unable to delete this account right now. Please try again.',
      );
    }
  }

  void _showManageMessage(BuildContext context, AccountLinkItem item) {
    _showMessage(
      context,
      'Account management for ${item.name} is coming soon.',
    );
  }

  void _showMessage(BuildContext context, String message) {
    ScaffoldMessenger.of(context)
        .showSnackBar(SnackBar(content: Text(message)));
  }
}

// ─────────────────────────────────────────────────────────
//  Empty state — full screen, no hero/sheet
// ─────────────────────────────────────────────────────────

class _AccountsEmptyLayout extends StatelessWidget {
  const _AccountsEmptyLayout({
    required this.isFreshDemo,
    required this.onSectionSelected,
    required this.onConnectTap,
    required this.onUploadTap,
    required this.onAddManualTap,
  });

  final bool isFreshDemo;
  final ValueChanged<SpendingSection> onSectionSelected;
  final VoidCallback onConnectTap;
  final VoidCallback onUploadTap;
  final VoidCallback onAddManualTap;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final theme = Theme.of(context);

    return DecoratedBox(
      decoration: BoxDecoration(gradient: c.warmScreenGradient),
      child: SafeArea(
        child: Column(
          children: <Widget>[
            PayaboAppHeader(
              title: 'Spend',
              titleStyle: theme.textTheme.headlineLarge?.copyWith(
                    fontWeight: FontWeight.w700,
                    color: c.accentBrown,
                  ),
              bottom: SpendingSectionPills(
                selectedSection: SpendingSection.accounts,
                sections: _visibleSpendingSections,
                onSelected: onSectionSelected,
              ),
            ),
            Expanded(
              child: SingleChildScrollView(
                padding: const EdgeInsets.all(PayaboSpacing.x4),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: <Widget>[
                    Container(
                      width: 72,
                      height: 72,
                      decoration: BoxDecoration(
                        color: c.primary.withValues(alpha: 0.1),
                        shape: BoxShape.circle,
                      ),
                      child: Icon(
                        Icons.account_balance_outlined,
                        size: 36,
                        color: c.primary,
                      ),
                    ),
                    const SizedBox(height: PayaboSpacing.xl),
                    Text(
                      isFreshDemo
                          ? 'Fresh accounts state'
                          : 'No linked accounts yet',
                      style: theme.textTheme.titleLarge?.copyWith(
                        color: c.accentBrown,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                    const SizedBox(height: PayaboSpacing.sm),
                    Text(
                      isFreshDemo
                          ? 'Fresh demo mode starts this hub blank so you can plan your first secure account connection.'
                          : 'Connect a bank or add a manual account to widen spend coverage.',
                      style: theme.textTheme.bodyMedium?.copyWith(
                        color: c.muted,
                        height: 1.5,
                      ),
                      textAlign: TextAlign.center,
                    ),
                    const SizedBox(height: PayaboSpacing.xl),
                    SizedBox(
                      width: double.infinity,
                      child: PayaboButton(
                        key: const Key('accounts-connect-primary'),
                        label: 'Connect bank account',
                        leading:
                            const Icon(Icons.add_link_outlined, size: 18),
                        onPressed: onConnectTap,
                      ),
                    ),
                    const SizedBox(height: PayaboSpacing.sm),
                    Row(
                      children: <Widget>[
                        Expanded(
                          child: PayaboButton(
                            label: 'Upload statement',
                            variant: PayaboButtonVariant.link,
                            leading: const Icon(Icons.upload_file_outlined,
                                size: 18),
                            onPressed: onUploadTap,
                          ),
                        ),
                        const SizedBox(width: PayaboSpacing.sm),
                        Expanded(
                          child: PayaboButton(
                            label: 'Add manual',
                            variant: PayaboButtonVariant.link,
                            leading: const Icon(Icons.edit_note_outlined,
                                size: 18),
                            onPressed: onAddManualTap,
                          ),
                        ),
                      ],
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
//  Hero + Pinned Header + DraggableScrollableSheet
// ─────────────────────────────────────────────────────────

class _AccountsHeroAndSheet extends StatefulWidget {
  const _AccountsHeroAndSheet({
    required this.summary,
    required this.flowState,
    required this.onSectionSelected,
    required this.onConnectTap,
    required this.onUploadTap,
    required this.onAddManualTap,
    required this.onReconnectTap,
    required this.onRefreshTap,
    required this.onDisconnectTap,
    required this.onDeleteTap,
    required this.onManageTap,
    required this.onRefreshAll,
    this.onSheetExtentChanged,
  });

  static const double _maxSheetSize = 1.0;
  static const double _pinnedHeaderHeight = 76;
  static const double _sheetTopGap = 10;
  static const double _minHeroHeight = 200;
  static const double _maxHeroHeight = 248;

  final AccountLinksSummary summary;
  final AccountLinkFlowState flowState;
  final ValueChanged<SpendingSection> onSectionSelected;
  final VoidCallback onConnectTap;
  final VoidCallback onUploadTap;
  final VoidCallback onAddManualTap;
  final ValueChanged<AccountLinkItem> onReconnectTap;
  final ValueChanged<AccountLinkItem> onRefreshTap;
  final ValueChanged<AccountLinkItem> onDisconnectTap;
  final ValueChanged<AccountLinkItem> onDeleteTap;
  final ValueChanged<AccountLinkItem> onManageTap;
  final Future<void> Function() onRefreshAll;
  final ValueChanged<double>? onSheetExtentChanged;

  @override
  State<_AccountsHeroAndSheet> createState() => _AccountsHeroAndSheetState();
}

class _AccountsHeroAndSheetState extends State<_AccountsHeroAndSheet> {
  late final DraggableScrollableController _sheetController;
  late final ValueNotifier<double> _sheetExtentNotifier;

  @override
  void initState() {
    super.initState();
    _sheetController = DraggableScrollableController();
    _sheetExtentNotifier = ValueNotifier<double>(0);
    _sheetController.addListener(_syncSheetExtent);
  }

  void _syncSheetExtent() {
    if (!_sheetController.isAttached) return;

    final double nextExtent = _sheetController.size;
    if ((_sheetExtentNotifier.value - nextExtent).abs() > 0.001) {
      final SchedulerPhase phase = WidgetsBinding.instance.schedulerPhase;

      if (phase == SchedulerPhase.idle ||
          phase == SchedulerPhase.postFrameCallbacks) {
        _sheetExtentNotifier.value = nextExtent;
        widget.onSheetExtentChanged?.call(nextExtent);
        return;
      }

      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (!mounted || !_sheetController.isAttached) return;
        if ((_sheetExtentNotifier.value - nextExtent).abs() > 0.001) {
          _sheetExtentNotifier.value = nextExtent;
          widget.onSheetExtentChanged?.call(nextExtent);
        }
      });
    }
  }

  @override
  void dispose() {
    _sheetController.removeListener(_syncSheetExtent);
    _sheetController.dispose();
    _sheetExtentNotifier.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (BuildContext context, BoxConstraints constraints) {
        final double viewportHeight =
            constraints.maxHeight.isFinite ? constraints.maxHeight : 640;

        final double heroHeight = math.min(
          _AccountsHeroAndSheet._maxHeroHeight,
          math.max(
            _AccountsHeroAndSheet._minHeroHeight,
            viewportHeight * 0.37,
          ),
        );

        const double pinnedHeaderHeight =
            _AccountsHeroAndSheet._pinnedHeaderHeight;
        const double pinnedSheetTop =
            pinnedHeaderHeight + _AccountsHeroAndSheet._sheetTopGap;

        final double sheetViewportHeight =
            math.max(1, viewportHeight - pinnedHeaderHeight);

        final double collapsedSheetTop = math.max(
          pinnedSheetTop + 164,
          heroHeight + PayaboSpacing.sm,
        );

        final double initialSheetSize = (1 -
                ((collapsedSheetTop - pinnedHeaderHeight) /
                    sheetViewportHeight))
            .clamp(0.62, 0.76)
            .toDouble();
        final double minSheetSize =
            (initialSheetSize - 0.10).clamp(0.56, initialSheetSize).toDouble();

        final double heroBottomPadding = math.max(
          40,
          heroHeight - collapsedSheetTop + 28,
        );

        return Stack(
          children: <Widget>[
            // ── LAYER 1: Hero banner ──
            Positioned(
              top: 0,
              left: 0,
              right: 0,
              height: heroHeight,
              child: _AccountsHeroBanner(
                summary: widget.summary,
                bottomPadding: heroBottomPadding,
              ),
            ),

            // ── LAYER 2: Pinned header (profile + bell, 76px) ──
            Positioned(
              top: 0,
              left: 0,
              right: 0,
              height: pinnedHeaderHeight,
              child: ValueListenableBuilder<double>(
                valueListenable: _sheetExtentNotifier,
                builder: (
                  BuildContext context,
                  double sheetExtent,
                  Widget? _,
                ) {
                  final double eff =
                      (sheetExtent <= 0 ? initialSheetSize : sheetExtent)
                          .clamp(
                            minSheetSize,
                            _AccountsHeroAndSheet._maxSheetSize,
                          )
                          .toDouble();
                  const double fadeZone = 0.05;
                  final double fadeStart = math.max(
                    0.0,
                    _AccountsHeroAndSheet._maxSheetSize - fadeZone,
                  );
                  final double bgProgress = Curves.easeOut.transform(
                    ((eff - fadeStart) / fadeZone).clamp(0.0, 1.0).toDouble(),
                  );
                  return _AccountsPinnedHeader(
                    backgroundProgress: bgProgress,
                  );
                },
              ),
            ),

            // ── LAYER 3: Draggable sheet — pills + account list ──
            Positioned(
              top: pinnedHeaderHeight,
              left: 0,
              right: 0,
              bottom: 0,
              child: DraggableScrollableSheet(
                controller: _sheetController,
                initialChildSize: initialSheetSize,
                minChildSize: minSheetSize,
                maxChildSize: _AccountsHeroAndSheet._maxSheetSize,
                snap: true,
                snapSizes: <double>[
                  initialSheetSize,
                  _AccountsHeroAndSheet._maxSheetSize,
                ],
                builder: (
                  BuildContext context,
                  ScrollController scrollController,
                ) {
                  return ValueListenableBuilder<double>(
                    valueListenable: _sheetExtentNotifier,
                    builder: (
                      BuildContext context,
                      double extent,
                      Widget? child,
                    ) {
                      const double fadeZone = 0.05;
                      final double fadeFraction = ((extent -
                                  (_AccountsHeroAndSheet._maxSheetSize -
                                      fadeZone)) /
                              fadeZone)
                          .clamp(0.0, 1.0);
                      return _AccountsSheet(
                        scrollController: scrollController,
                        topBorderRadius: 24.0 * (1.0 - fadeFraction),
                        summary: widget.summary,
                        flowState: widget.flowState,
                        onSectionSelected: widget.onSectionSelected,
                        onConnectTap: widget.onConnectTap,
                        onReconnectTap: widget.onReconnectTap,
                        onRefreshTap: widget.onRefreshTap,
                        onDisconnectTap: widget.onDisconnectTap,
                        onDeleteTap: widget.onDeleteTap,
                        onManageTap: widget.onManageTap,
                      );
                    },
                  );
                },
              ),
            ),
          ],
        );
      },
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Pinned header — profile + notification bell (76px)
// ─────────────────────────────────────────────────────────

class _AccountsPinnedHeader extends StatelessWidget {
  const _AccountsPinnedHeader({required this.backgroundProgress});

  final double backgroundProgress;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Stack(
      children: <Widget>[
        Positioned.fill(
          child: Opacity(
            opacity: backgroundProgress,
            child: ColoredBox(color: c.surfaceBase),
          ),
        ),
        const Positioned.fill(
          child: PayaboAppHeader(
            padding: EdgeInsets.fromLTRB(
              PayaboSpacing.xl,
              PayaboSpacing.md,
              PayaboSpacing.xl,
              0,
            ),
          ),
        ),
      ],
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Hero banner — account summary on dark gradient
// ─────────────────────────────────────────────────────────

class _AccountsHeroBanner extends StatelessWidget {
  const _AccountsHeroBanner({
    required this.summary,
    this.bottomPadding = 40,
  });

  final AccountLinksSummary summary;
  final double bottomPadding;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final textTheme = Theme.of(context).textTheme;

    return SizedBox(
      width: double.infinity,
      child: Padding(
        padding: EdgeInsets.fromLTRB(
          PayaboSpacing.xl,
          0,
          PayaboSpacing.xl,
          bottomPadding,
        ),
        child: Align(
          alignment: Alignment.bottomLeft,
          child: LayoutBuilder(
            builder: (BuildContext context, BoxConstraints box) {
              final bool compact = box.maxHeight < 190;

              return Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  // ── Label ──
                  Row(
                    children: <Widget>[
                      Text(
                        'Connected accounts',
                        style: textTheme.bodyMedium?.copyWith(
                          color: Colors.white.withValues(alpha: 0.6),
                          fontWeight: FontWeight.w500,
                        ),
                      ),
                      if (summary.attentionCount > 0) ...<Widget>[
                        const SizedBox(width: PayaboSpacing.sm),
                        _AccountStatusPill(
                          label: '${summary.attentionCount} needs attention',
                          foregroundColor: c.warning,
                        ),
                      ],
                    ],
                  ),
                  SizedBox(
                    height: compact ? PayaboSpacing.sm : PayaboSpacing.md,
                  ),

                  // ── Account count as hero number ──
                  Text(
                    '${summary.linkedCount + summary.manualCount}',
                    style: (compact
                            ? textTheme.headlineLarge
                            : textTheme.displaySmall)
                        ?.copyWith(
                      color: Colors.white,
                      fontWeight: FontWeight.w800,
                      height: 1,
                    ),
                  ),
                  SizedBox(
                    height: compact ? PayaboSpacing.sm : PayaboSpacing.md,
                  ),

                  // ── Summary metrics ──
                  Row(
                    children: <Widget>[
                      Text(
                        '${summary.linkedCount} linked',
                        style: textTheme.bodyMedium?.copyWith(
                          color: Colors.white.withValues(alpha: 0.7),
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                      const SizedBox(width: PayaboSpacing.lg),
                      Text(
                        '${summary.manualCount} manual',
                        style: textTheme.bodyMedium?.copyWith(
                          color: Colors.white.withValues(alpha: 0.5),
                        ),
                      ),
                    ],
                  ),
                ],
              );
            },
          ),
        ),
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Sheet — section pills + account list
// ─────────────────────────────────────────────────────────

class _AccountsSheet extends StatelessWidget {
  const _AccountsSheet({
    required this.scrollController,
    required this.summary,
    required this.flowState,
    required this.onSectionSelected,
    required this.onConnectTap,
    required this.onReconnectTap,
    required this.onRefreshTap,
    required this.onDisconnectTap,
    required this.onDeleteTap,
    required this.onManageTap,
    this.topBorderRadius = 24.0,
  });

  final ScrollController scrollController;
  final AccountLinksSummary summary;
  final AccountLinkFlowState flowState;
  final ValueChanged<SpendingSection> onSectionSelected;
  final VoidCallback onConnectTap;
  final ValueChanged<AccountLinkItem> onReconnectTap;
  final ValueChanged<AccountLinkItem> onRefreshTap;
  final ValueChanged<AccountLinkItem> onDisconnectTap;
  final ValueChanged<AccountLinkItem> onDeleteTap;
  final ValueChanged<AccountLinkItem> onManageTap;
  final double topBorderRadius;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Container(
      width: double.infinity,
      decoration: BoxDecoration(
        color: c.surfaceBase,
        borderRadius: BorderRadius.only(
          topLeft: Radius.circular(topBorderRadius),
          topRight: Radius.circular(topBorderRadius),
        ),
        boxShadow: topBorderRadius > 0
            ? <BoxShadow>[
                BoxShadow(
                  color:
                      Colors.black.withValues(alpha: c.isDark ? 0.22 : 0.08),
                  blurRadius: 18,
                  offset: const Offset(0, -4),
                ),
              ]
            : const <BoxShadow>[],
      ),
      child: ListView(
        controller: scrollController,
        physics: const BouncingScrollPhysics(
          parent: AlwaysScrollableScrollPhysics(),
        ),
        padding: const EdgeInsets.fromLTRB(
          PayaboSpacing.xl,
          PayaboSpacing.md,
          PayaboSpacing.xl,
          PayaboSpacing.x4,
        ),
        children: <Widget>[
          // ── Drag handle ──
          Center(
            child: Container(
              width: 42,
              height: 5,
              decoration: BoxDecoration(
                color: c.borderStrong,
                borderRadius: BorderRadius.circular(999),
              ),
            ),
          ),
          const SizedBox(height: PayaboSpacing.lg),

          // ── Section pills ──
          SpendingSectionPills(
            selectedSection: SpendingSection.accounts,
            sections: _visibleSpendingSections,
            onSelected: onSectionSelected,
          ),
          const SizedBox(height: PayaboSpacing.lg),

          // ── Section heading ──
          Row(
            children: <Widget>[
              Expanded(
                child: Text(
                  'Accounts in Spend',
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                        color: c.accentBrown,
                        fontWeight: FontWeight.w700,
                      ),
                ),
              ),
              _AccountStatusPill(
                label: '${summary.accounts.length} active',
                foregroundColor: c.primary,
              ),
            ],
          ),
          const SizedBox(height: PayaboSpacing.sm),

          // ── Account rows ──
          for (int i = 0; i < summary.accounts.length; i++) ...[
            _AccountRow(
              item: summary.accounts[i],
              isBusy: flowState.isSubmitting &&
                  flowState.activeConnectionId ==
                      summary.accounts[i].connectionId,
              onReconnectTap: () => onReconnectTap(summary.accounts[i]),
              onRefreshTap: () => onRefreshTap(summary.accounts[i]),
              onDisconnectTap: () => onDisconnectTap(summary.accounts[i]),
              onDeleteTap: () => onDeleteTap(summary.accounts[i]),
              onManageTap: () => onManageTap(summary.accounts[i]),
            ),
            if (i < summary.accounts.length - 1)
              Divider(
                height: 1,
                color: c.borderStrong.withValues(alpha: 0.3),
              ),
          ],
          const SizedBox(height: PayaboSpacing.lg),

          // ── Connect button ──
          Center(
            child: PayaboButton(
              key: const Key('accounts-connect-sheet'),
              label: 'Connect bank account',
              variant: PayaboButtonVariant.secondary,
              size: PayaboButtonSize.lg,
              leading: const Icon(Icons.add_link_outlined, size: 20),
              onPressed: onConnectTap,
            ),
          ),
        ],
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Account row — mirrors transaction row styling
// ─────────────────────────────────────────────────────────

class _AccountRow extends StatelessWidget {
  const _AccountRow({
    required this.item,
    required this.isBusy,
    required this.onReconnectTap,
    required this.onRefreshTap,
    required this.onDisconnectTap,
    required this.onDeleteTap,
    required this.onManageTap,
  });

  final AccountLinkItem item;
  final bool isBusy;
  final VoidCallback onReconnectTap;
  final VoidCallback onRefreshTap;
  final VoidCallback onDisconnectTap;
  final VoidCallback onDeleteTap;
  final VoidCallback onManageTap;

  IconData _iconForAccount() {
    if (item.source == AccountLinkSource.manual) {
      return Icons.edit_note_outlined;
    } else if (item.accountTypeLabel.toLowerCase().contains('savings')) {
      return Icons.savings_outlined;
    } else if (item.accountTypeLabel.toLowerCase().contains('credit')) {
      return Icons.credit_card_outlined;
    } else {
      return Icons.account_balance_wallet_outlined;
    }
  }

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final theme = Theme.of(context);

    return InkWell(
      key: Key('account-card-${item.id}'),
      onTap: onManageTap,
      borderRadius: PayaboRadii.radiusSm,
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: PayaboSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Row(
              children: <Widget>[
                // ── Icon ─────────────────────────────
                Container(
                  width: 40,
                  height: 40,
                  decoration: BoxDecoration(
                    color: c.isDark
                        ? theme.colorScheme.surfaceContainerHighest
                        : c.spendingMerchantIconWarmSurface,
                    borderRadius: BorderRadius.circular(10),
                  ),
                  alignment: Alignment.center,
                  child: Icon(
                    _iconForAccount(),
                    color: c.spendingMerchantIconDark,
                    size: 18,
                  ),
                ),

                const SizedBox(width: PayaboSpacing.md),

                // ── Name + institution ──────────────
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Text(
                        item.name,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: theme.textTheme.titleSmall?.copyWith(
                          fontWeight: FontWeight.w600,
                          color: theme.colorScheme.onSurface,
                        ),
                      ),
                      const SizedBox(height: 2),
                      Text(
                        item.institutionName,
                        style: theme.textTheme.bodySmall?.copyWith(
                          color: c.muted,
                        ),
                      ),
                    ],
                  ),
                ),

                const SizedBox(width: PayaboSpacing.md),

                // ── Balance / type + status ─────────
                Column(
                  crossAxisAlignment: CrossAxisAlignment.end,
                  children: <Widget>[
                    if (item.balanceLabel != null)
                      Text(
                        item.balanceLabel!,
                        style: theme.textTheme.titleSmall?.copyWith(
                          fontWeight: FontWeight.w700,
                          color: theme.colorScheme.onSurface,
                        ),
                      )
                    else
                      Text(
                        item.accountTypeLabel,
                        style: theme.textTheme.titleSmall?.copyWith(
                          fontWeight: FontWeight.w700,
                          color: theme.colorScheme.onSurface,
                        ),
                      ),
                    const SizedBox(height: 2),
                    _AccountStatusPill(
                      label: item.statusLabel,
                      foregroundColor: _statusColor(c, item.status),
                    ),
                  ],
                ),
              ],
            ),

            // ── Action buttons (only if account needs attention) ──
            if (item.needsReconnect || item.canRefresh) ...<Widget>[
              const SizedBox(height: PayaboSpacing.sm),
              Wrap(
                spacing: PayaboSpacing.sm,
                runSpacing: PayaboSpacing.sm,
                children: <Widget>[
                  if (item.canReconnect)
                    PayaboButton(
                      label: 'Reconnect',
                      variant: PayaboButtonVariant.secondary,
                      size: PayaboButtonSize.sm,
                      expand: false,
                      onPressed: isBusy ? null : onReconnectTap,
                    ),
                  if (item.canRefresh)
                    PayaboButton(
                      label: 'Refresh',
                      variant: PayaboButtonVariant.secondary,
                      size: PayaboButtonSize.sm,
                      expand: false,
                      onPressed: isBusy ? null : onRefreshTap,
                    ),
                  if (item.canDisconnect)
                    PayaboButton(
                      label: 'Disconnect',
                      variant: PayaboButtonVariant.link,
                      size: PayaboButtonSize.sm,
                      expand: false,
                      onPressed: isBusy ? null : onDisconnectTap,
                    ),
                  if (item.source == AccountLinkSource.manual)
                    PayaboButton(
                      label: 'Delete',
                      variant: PayaboButtonVariant.link,
                      size: PayaboButtonSize.sm,
                      expand: false,
                      onPressed: isBusy ? null : onDeleteTap,
                    ),
                ],
              ),
            ],
          ],
        ),
      ),
    );
  }

  Color _statusColor(PayaboColorResolver c, AccountLinkStatus status) {
    switch (status) {
      case AccountLinkStatus.connected:
        return c.primary;
      case AccountLinkStatus.syncing:
        return c.info;
      case AccountLinkStatus.actionRequired:
        return c.warning;
      case AccountLinkStatus.manual:
        return c.accentBrown;
      case AccountLinkStatus.archived:
        return c.muted;
    }
  }
}

// ─────────────────────────────────────────────────────────
//  Shared small widgets
// ─────────────────────────────────────────────────────────

class _AccountStatusPill extends StatelessWidget {
  const _AccountStatusPill({
    required this.label,
    required this.foregroundColor,
  });

  final String label;
  final Color foregroundColor;

  @override
  Widget build(BuildContext context) {
    return DecoratedBox(
      decoration: BoxDecoration(
        color: foregroundColor.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(PayaboRadii.pill),
      ),
      child: Padding(
        padding: const EdgeInsets.symmetric(
          horizontal: PayaboSpacing.md,
          vertical: PayaboSpacing.sm,
        ),
        child: Text(
          label,
          style: Theme.of(context).textTheme.bodySmall?.copyWith(
                color: foregroundColor,
                fontWeight: FontWeight.w700,
              ),
        ),
      ),
    );
  }
}

class _AccountsRefreshingOverlay extends StatelessWidget {
  const _AccountsRefreshingOverlay();

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return AbsorbPointer(
      child: DecoratedBox(
        decoration: BoxDecoration(
          color: c.surfaceBase.withValues(alpha: 0.38),
        ),
        child: Center(
          child: Container(
            margin: const EdgeInsets.all(PayaboSpacing.xl),
            padding: const EdgeInsets.symmetric(
              horizontal: PayaboSpacing.lg,
              vertical: PayaboSpacing.md,
            ),
            decoration: BoxDecoration(
              color: c.surfaceCardElevated,
              borderRadius: BorderRadius.circular(PayaboRadii.lg),
              border: Border.all(color: c.borderWarm),
            ),
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: <Widget>[
                const SizedBox(
                  width: 20,
                  height: 20,
                  child: CircularProgressIndicator(strokeWidth: 2.2),
                ),
                const SizedBox(width: PayaboSpacing.sm),
                Text(
                  'Updating linked accounts...',
                  style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                        color: c.accentBrown,
                        fontWeight: FontWeight.w600,
                      ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
