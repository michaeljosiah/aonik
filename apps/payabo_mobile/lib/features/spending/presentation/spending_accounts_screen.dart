import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../app/demo/demo_data_mode.dart';
import '../../../app/environment/environment_provider.dart';
import '../../../data/repositories/account_links_repository.dart';
import '../../../features/profile/presentation/profile_state.dart';
import '../../../shared/reference/payabo_country_reference.dart';
import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_radii.dart';
import '../../../shared/theme/payabo_shadows.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_app_header.dart';
import '../../../shared/widgets/payabo_button.dart';
import '../../../shared/widgets/payabo_card.dart';
import '../../../shared/widgets/payabo_modal_sheet.dart';
import '../../../shared/widgets/payabo_primary_app_shell.dart';
import 'spending_accounts_state.dart';
import 'widgets/spending_section_pills.dart';

const List<SpendingSection> _visibleSpendingSections = <SpendingSection>[
  SpendingSection.transactions,
  SpendingSection.budgets,
  SpendingSection.accounts,
];

class SpendingAccountsScreen extends ConsumerWidget {
  const SpendingAccountsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.colors;
    final AsyncValue<AccountLinksSummary> summaryValue =
        ref.watch(accountLinksSummaryProvider);
    final AccountLinkFlowState flowState =
        ref.watch(accountLinkFlowControllerProvider);
    final bool isFreshDemo =
        ref.watch(demoDataModeProvider) == DemoDataMode.fresh;
    final bool isRefreshingSummary = summaryValue.isRefreshing;

    return Scaffold(
      backgroundColor: c.surfaceWarm,
      body: DecoratedBox(
        decoration: BoxDecoration(
          gradient: c.warmScreenGradient,
        ),
        child: SafeArea(
          child: Column(
            children: <Widget>[
              _AccountsHeader(
                onSectionSelected: (SpendingSection section) =>
                    _handleSectionSelected(context, section),
                onNotificationsTap: () => context.push('/notifications'),
                onProfileTap: () => context.go('/profile'),
              ),
              Expanded(
                child: Stack(
                  fit: StackFit.expand,
                  children: <Widget>[
                    summaryValue.when(
                      data: (AccountLinksSummary summary) {
                        return RefreshIndicator(
                          onRefresh: () async {
                            ref.invalidate(accountLinksSummaryProvider);
                            await ref.read(accountLinksSummaryProvider.future);
                          },
                          child: ListView(
                            physics: const AlwaysScrollableScrollPhysics(),
                            padding: const EdgeInsets.fromLTRB(
                              PayaboSpacing.xl,
                              PayaboSpacing.md,
                              PayaboSpacing.xl,
                              PayaboSpacing.x4,
                            ),
                            children: <Widget>[
                              _AccountsHeroCard(
                                summary: summary,
                                isFreshDemo: isFreshDemo,
                                onConnectTap: () {
                                  _showConnectSheet(context, ref);
                                },
                              ),
                              const SizedBox(height: PayaboSpacing.lg),
                              _QuickActionsRow(
                                onConnectTap: () {
                                  _showConnectSheet(context, ref);
                                },
                                onUploadTap: () => _showUploadMessage(context),
                                onAddManualTap: () =>
                                    _showManualMessage(context),
                              ),
                              const SizedBox(height: PayaboSpacing.xl),
                              if (!summary.hasAccounts)
                                isFreshDemo
                                    ? const _FreshAccountsStateCard()
                                    : const _UnlinkedAccountsStateCard()
                              else ...<Widget>[
                                const _AccountsSectionHeading(
                                  title: 'Accounts in Spend',
                                  subtitle:
                                      'Linked and manual sources powering budgets, categories, and merchant views.',
                                ),
                                const SizedBox(height: PayaboSpacing.md),
                                ...summary.accounts.map(
                                  (AccountLinkItem item) => Padding(
                                    padding: const EdgeInsets.only(
                                      bottom: PayaboSpacing.md,
                                    ),
                                    child: _AccountLinkCard(
                                      item: item,
                                      isBusy: flowState.isSubmitting &&
                                          flowState.activeConnectionId ==
                                              item.connectionId,
                                      onReconnectTap: () => _handleReconnect(
                                        context,
                                        ref,
                                        item,
                                      ),
                                      onRefreshTap: () =>
                                          _handleRefresh(context, ref, item),
                                      onDisconnectTap: () => _handleDisconnect(
                                        context,
                                        ref,
                                        item,
                                      ),
                                      onManageTap: () =>
                                          _showManageMessage(context, item),
                                    ),
                                  ),
                                ),
                              ],
                              const SizedBox(height: PayaboSpacing.xl),
                              const _AccountsExplainerCard(),
                            ],
                          ),
                        );
                      },
                      loading: () =>
                          const Center(child: CircularProgressIndicator()),
                      error: (Object error, StackTrace stackTrace) {
                        return Center(
                          child: Padding(
                            padding: const EdgeInsets.all(PayaboSpacing.xl),
                            child: _AccountsLoadErrorCard(
                              message: error.toString(),
                              onRetry: () =>
                                  ref.invalidate(accountLinksSummaryProvider),
                            ),
                          ),
                        );
                      },
                    ),
                    if (isRefreshingSummary) const _AccountsRefreshingOverlay(),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
      bottomNavigationBar: const PayaboPrimaryAppShell(
        destination: PayaboPrimaryDestination.spending,
      ),
    );
  }

  void _handleSectionSelected(BuildContext context, SpendingSection section) {
    switch (section) {
      case SpendingSection.overview:
      case SpendingSection.transactions:
        context.go('/spending');
        return;
      case SpendingSection.budgets:
        context.go('/spending/budgets');
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
    final AccountLinkExchangeResult? result =
        await showPayaboModalSheet<AccountLinkExchangeResult>(
      context: context,
      title: title,
      child: _AccountLinkConnectSheet(
        provider: provider,
        mode: mode,
        connectionId: connectionId,
      ),
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

  void _showManageMessage(BuildContext context, AccountLinkItem item) {
    _showMessage(
      context,
      '${item.name} details and account actions will be added in a follow-up step.',
    );
  }

  void _showMessage(BuildContext context, String message) {
    ScaffoldMessenger.of(context)
        .showSnackBar(SnackBar(content: Text(message)));
  }
}

class _AccountsHeader extends StatelessWidget {
  const _AccountsHeader({
    required this.onSectionSelected,
    required this.onNotificationsTap,
    required this.onProfileTap,
  });

  final ValueChanged<SpendingSection> onSectionSelected;
  final VoidCallback onNotificationsTap;
  final VoidCallback onProfileTap;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return PayaboAppHeader(
      title: 'Spend',
      titleStyle: Theme.of(context).textTheme.headlineLarge?.copyWith(
            fontWeight: FontWeight.w700,
            color: c.accentBrown,
          ),
      onNotificationsTap: onNotificationsTap,
      onProfileTap: onProfileTap,
      bottom: SpendingSectionPills(
        selectedSection: SpendingSection.accounts,
        sections: _visibleSpendingSections,
        onSelected: onSectionSelected,
      ),
    );
  }
}

class _AccountLinkConnectSheet extends ConsumerStatefulWidget {
  const _AccountLinkConnectSheet({
    required this.provider,
    required this.mode,
    this.connectionId,
  });

  final String provider;
  final String mode;
  final String? connectionId;

  @override
  ConsumerState<_AccountLinkConnectSheet> createState() =>
      _AccountLinkConnectSheetState();
}

class _AccountLinkConnectSheetState
    extends ConsumerState<_AccountLinkConnectSheet> {
  String? _selectedCountryCode;

  bool get _requiresCountrySelection =>
      widget.mode == 'connect' && widget.provider.toLowerCase() == 'plaid';

  @override
  void initState() {
    super.initState();
    ref.read(accountLinkFlowControllerProvider.notifier).reset();

    final String profileCountryCode =
        ref.read(profileCoreProvider).countryCode.trim().toUpperCase();
    final bool hasProfileCountry = payaboCountries.any(
      (PayaboCountryReference country) => country.code == profileCountryCode,
    );
    _selectedCountryCode = hasProfileCountry ? profileCountryCode : 'GB';
  }

  /// Uses the full-screen [PlaidLink.open()] approach via
  /// [AccountLinkFlowController.connect]. Shows consent pane first,
  /// then bank selection.
  Future<void> _handleConnect() async {
    try {
      final AccountLinkExchangeResult? result =
          await ref.read(accountLinkFlowControllerProvider.notifier).connect(
                provider: widget.provider,
                mode: widget.mode,
                connectionId: widget.connectionId,
                countryCode:
                    _requiresCountrySelection ? _selectedCountryCode : null,
              );

      if (!mounted || result == null) {
        return;
      }

      Navigator.of(context).pop(result);
    } catch (_) {
      // The controller already exposes a friendly message for the sheet.
    }
  }

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final AccountLinkFlowState flowState =
        ref.watch(accountLinkFlowControllerProvider);
    final AccountLinkLauncher launcher = ref.watch(accountLinkLauncherProvider);
    final bool isReconnect = widget.mode == 'update';
    final PayaboCountryReference selectedCountry = resolvePayaboCountry(
      _selectedCountryCode ?? 'GB',
    );

    final String introText = launcher.isNativeProviderFlow
        ? isReconnect
            ? 'Resume a secure ${launcher.experienceLabel} update session so Payabo can restore sync for this bank connection without exposing credentials in the app.'
            : 'Open a secure ${launcher.experienceLabel} session to connect your bank account, then exchange the temporary result with AONIK so Spend can refresh linked accounts safely.'
        : isReconnect
            ? 'Start a secure reconnect session so this linked account can return to active Spend sync.'
            : 'Start a secure connection session to bring live spending data into Payabo. This build uses a simulated provider handoff, then exchanges the temporary result with AONIK on the backend.';

    final String stepOneTitle = launcher.isNativeProviderFlow
        ? _requiresCountrySelection
            ? 'Launch ${launcher.experienceLabel} for ${selectedCountry.name}'
            : 'Launch ${launcher.experienceLabel}'
        : isReconnect
            ? 'Reconnect the existing link'
            : 'Short-lived mobile session';
    final String stepOneSubtitle = launcher.isNativeProviderFlow
        ? isReconnect
            ? 'The app opens the native provider update mode using a short-lived token from AONIK.'
            : 'The app opens the native ${launcher.experienceLabel} experience using a short-lived link token from AONIK.'
        : isReconnect
            ? 'Payabo uses a targeted update session so the existing link can be restored.'
            : 'The app receives only a temporary launch token for the provider handoff.';

    return Column(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Text(
          introText,
          style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                color: c.accentBrownMuted,
                height: 1.45,
              ),
        ),
        const SizedBox(height: PayaboSpacing.lg),
        _ConnectSheetStep(
          icon: Icons.shield_outlined,
          title: stepOneTitle,
          subtitle: stepOneSubtitle,
        ),
        if (_requiresCountrySelection) ...<Widget>[
          const SizedBox(height: PayaboSpacing.md),
          Container(
            width: double.infinity,
            decoration: BoxDecoration(
              color: c.primary.withValues(alpha: 0.08),
              borderRadius: BorderRadius.circular(PayaboRadii.lg),
              border: Border.all(color: c.primary.withValues(alpha: 0.18)),
            ),
            padding: const EdgeInsets.all(PayaboSpacing.md),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  'Bank country',
                  style: Theme.of(context).textTheme.titleSmall?.copyWith(
                        color: c.accentBrown,
                        fontWeight: FontWeight.w700,
                      ),
                ),
                const SizedBox(height: PayaboSpacing.xs),
                Text(
                  'Choose where the bank account is held before Payabo opens Plaid. This helps the secure institution list match the country-specific banks your customer expects to see.',
                  style: Theme.of(context).textTheme.bodySmall?.copyWith(
                        color: c.accentBrownMuted,
                        height: 1.45,
                      ),
                ),
                const SizedBox(height: PayaboSpacing.md),
                DropdownButtonFormField<String>(
                  key: const Key('accounts-country-dropdown'),
                  value: _selectedCountryCode,
                  decoration: InputDecoration(
                    filled: true,
                    fillColor: c.surfaceBase,
                    labelText: 'Country',
                    border: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(PayaboRadii.lg),
                    ),
                    enabledBorder: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(PayaboRadii.lg),
                      borderSide: BorderSide(color: c.borderStrong),
                    ),
                    focusedBorder: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(PayaboRadii.lg),
                      borderSide: BorderSide(color: c.primary, width: 1.4),
                    ),
                  ),
                  items: payaboCountries
                      .map(
                        (PayaboCountryReference country) =>
                            DropdownMenuItem<String>(
                          value: country.code,
                          child: Text(
                            '${country.flagEmoji} ${country.name}',
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                      )
                      .toList(growable: false),
                  onChanged: flowState.isSubmitting
                      ? null
                      : (String? value) {
                          setState(() {
                            _selectedCountryCode = value;
                          });
                        },
                ),
              ],
            ),
          ),
        ],
        const SizedBox(height: PayaboSpacing.md),
        const _ConnectSheetStep(
          icon: Icons.swap_horiz_outlined,
          title: 'Server-side exchange',
          subtitle:
              'AONIK exchanges the temporary result and stores long-lived provider references on the server.',
        ),
        const SizedBox(height: PayaboSpacing.md),
        const _ConnectSheetStep(
          icon: Icons.insights_outlined,
          title: 'Spend gets richer context',
          subtitle:
              'Linked accounts improve category, merchant, and account-level insight coverage.',
        ),
        if (flowState.errorMessage != null) ...<Widget>[
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
              flowState.errorMessage!,
              style: Theme.of(context).textTheme.bodySmall?.copyWith(
                    color: c.accentBrown,
                    height: 1.4,
                  ),
            ),
          ),
        ],
        if (flowState.isSubmitting) ...<Widget>[
          const SizedBox(height: PayaboSpacing.lg),
          Row(
            children: <Widget>[
              const SizedBox(
                width: 18,
                height: 18,
                child: CircularProgressIndicator(strokeWidth: 2.2),
              ),
              const SizedBox(width: PayaboSpacing.sm),
              Expanded(
                child: Text(
                  isReconnect
                      ? 'Reconnecting securely and exchanging the temporary code...'
                      : 'Connecting securely and exchanging the temporary code...',
                  style: Theme.of(context).textTheme.bodySmall?.copyWith(
                        color: c.accentBrownMuted,
                      ),
                ),
              ),
            ],
          ),
        ],
        const SizedBox(height: PayaboSpacing.xl),
        Row(
          children: <Widget>[
            Expanded(
              child: PayaboButton(
                key: const Key('accounts-connect-cancel'),
                label: 'Not now',
                variant: PayaboButtonVariant.link,
                onPressed: flowState.isSubmitting
                    ? null
                    : () => Navigator.of(context).pop(),
              ),
            ),
            const SizedBox(width: PayaboSpacing.sm),
            Expanded(
              child: PayaboButton(
                key: const Key('accounts-connect-continue'),
                label: flowState.isSubmitting ? 'Connecting...' : 'Continue',
                onPressed: flowState.isSubmitting ? null : _handleConnect,
              ),
            ),
          ],
        ),
      ],
    );
  }
}

class _ConnectSheetStep extends StatelessWidget {
  const _ConnectSheetStep({
    required this.icon,
    required this.title,
    required this.subtitle,
  });

  final IconData icon;
  final String title;
  final String subtitle;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Container(
          width: 40,
          height: 40,
          decoration: BoxDecoration(
            color: c.primary.withValues(alpha: 0.12),
            borderRadius: BorderRadius.circular(14),
          ),
          child: Icon(icon, color: c.primary, size: 21),
        ),
        const SizedBox(width: PayaboSpacing.md),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Text(
                title,
                style: Theme.of(context).textTheme.titleSmall?.copyWith(
                      color: c.accentBrown,
                      fontWeight: FontWeight.w700,
                    ),
              ),
              const SizedBox(height: PayaboSpacing.xxs),
              Text(
                subtitle,
                style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color: c.accentBrownMuted,
                      height: 1.45,
                    ),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

class _AccountsHeroCard extends StatelessWidget {
  const _AccountsHeroCard({
    required this.summary,
    required this.isFreshDemo,
    required this.onConnectTap,
  });

  final AccountLinksSummary summary;
  final bool isFreshDemo;
  final VoidCallback onConnectTap;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final String description;

    if (isFreshDemo) {
      description =
          'Fresh demo mode starts this hub blank so you can plan your first secure account connection.';
    } else if (!summary.hasAccounts) {
      description =
          'Connect a bank once to keep budgets, merchants, and spend insights closer to real life.';
    } else if (summary.attentionCount > 0) {
      description =
          '${summary.attentionCount} account${summary.attentionCount == 1 ? '' : 's'} need attention before sync can fully resume.';
    } else {
      description =
          'Your linked and manual accounts are ready to feed richer spending coverage across Payabo.';
    }

    return Container(
      decoration: BoxDecoration(
        color: c.surfaceCardElevated,
        borderRadius: PayaboRadii.radiusSm,
        border: Border.all(color: c.spendingQuickActionBorder),
        boxShadow: c.isDark ? PayaboShadows.soft : PayaboShadows.soft,
      ),
      child: Padding(
        padding: const EdgeInsets.all(PayaboSpacing.xl),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Text(
                        'Connected accounts',
                        style:
                            Theme.of(context).textTheme.titleMedium?.copyWith(
                                  color: c.accentBrownMuted,
                                ),
                      ),
                      const SizedBox(height: PayaboSpacing.xs),
                      Text(
                        description,
                        style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                              color: c.muted,
                              height: 1.45,
                            ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(width: PayaboSpacing.md),
                Container(
                  width: 56,
                  height: 56,
                  decoration: BoxDecoration(
                    color: c.primary.withValues(alpha: c.isDark ? 0.14 : 0.12),
                    borderRadius: BorderRadius.circular(18),
                  ),
                  child: Icon(
                    Icons.account_balance_outlined,
                    color: c.primary,
                    size: 28,
                  ),
                ),
              ],
            ),
            const SizedBox(height: PayaboSpacing.xl),
            Text(
              '${summary.linkedCount}',
              style: Theme.of(context).textTheme.displayMedium?.copyWith(
                    color: c.accentBrown,
                    height: 1,
                    fontWeight: FontWeight.w800,
                  ),
            ),
            const SizedBox(height: PayaboSpacing.xs),
            Text(
              'linked account${summary.linkedCount == 1 ? '' : 's'} ready for Spend',
              style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                    color: c.accentBrownMuted,
                  ),
            ),
            const SizedBox(height: PayaboSpacing.lg),
            Wrap(
              spacing: PayaboSpacing.sm,
              runSpacing: PayaboSpacing.sm,
              children: <Widget>[
                _MetricChip(label: 'Linked', value: '${summary.linkedCount}'),
                _MetricChip(label: 'Manual', value: '${summary.manualCount}'),
                _MetricChip(
                  label: 'Needs attention',
                  value: '${summary.attentionCount}',
                  valueColor:
                      summary.attentionCount > 0 ? c.warning : c.accentBrown,
                ),
              ],
            ),
            const SizedBox(height: PayaboSpacing.lg),
            SizedBox(
              width: double.infinity,
              child: PayaboButton(
                key: const Key('accounts-connect-primary'),
                label: 'Connect bank account',
                leading: const Icon(Icons.add_link_outlined, size: 18),
                onPressed: onConnectTap,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _MetricChip extends StatelessWidget {
  const _MetricChip({
    required this.label,
    required this.value,
    this.valueColor,
  });

  final String label;
  final String value;
  final Color? valueColor;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return DecoratedBox(
      decoration: BoxDecoration(
        color: c.spendingCardWarm,
        borderRadius: BorderRadius.circular(PayaboRadii.pill),
        border: Border.all(color: c.spendingQuickActionBorder),
      ),
      child: Padding(
        padding: const EdgeInsets.symmetric(
          horizontal: PayaboSpacing.md,
          vertical: PayaboSpacing.sm,
        ),
        child: RichText(
          text: TextSpan(
            children: <InlineSpan>[
              TextSpan(
                text: '$label ',
                style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color: c.muted,
                      fontWeight: FontWeight.w600,
                    ),
              ),
              TextSpan(
                text: value,
                style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color: valueColor ?? c.accentBrown,
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

class _QuickActionsRow extends StatelessWidget {
  const _QuickActionsRow({
    required this.onConnectTap,
    required this.onUploadTap,
    required this.onAddManualTap,
  });

  final VoidCallback onConnectTap;
  final VoidCallback onUploadTap;
  final VoidCallback onAddManualTap;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (BuildContext context, BoxConstraints constraints) {
        final bool stack = constraints.maxWidth < 960;
        final List<Widget> actions = <Widget>[
          PayaboButton(
            key: const Key('accounts-connect-quick-action'),
            label: 'Connect bank',
            variant: PayaboButtonVariant.secondary,
            expand: true,
            leading: const Icon(Icons.account_balance_outlined, size: 18),
            onPressed: onConnectTap,
          ),
          PayaboButton(
            label: 'Upload statement',
            variant: PayaboButtonVariant.link,
            expand: true,
            leading: const Icon(Icons.upload_file_outlined, size: 18),
            onPressed: onUploadTap,
          ),
          PayaboButton(
            label: 'Add manual',
            variant: PayaboButtonVariant.link,
            expand: true,
            leading: const Icon(Icons.edit_note_outlined, size: 18),
            onPressed: onAddManualTap,
          ),
        ];

        if (stack) {
          return Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: <Widget>[
              actions[0],
              const SizedBox(height: PayaboSpacing.sm),
              actions[1],
              const SizedBox(height: PayaboSpacing.sm),
              actions[2],
            ],
          );
        }

        return Row(
          children: <Widget>[
            Expanded(child: actions[0]),
            const SizedBox(width: PayaboSpacing.sm),
            Expanded(child: actions[1]),
            const SizedBox(width: PayaboSpacing.sm),
            Expanded(child: actions[2]),
          ],
        );
      },
    );
  }
}

class _AccountsSectionHeading extends StatelessWidget {
  const _AccountsSectionHeading({
    required this.title,
    required this.subtitle,
  });

  final String title;
  final String subtitle;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Text(
          title,
          style: Theme.of(context).textTheme.titleLarge?.copyWith(
                color: c.accentBrown,
                fontWeight: FontWeight.w700,
              ),
        ),
        const SizedBox(height: PayaboSpacing.xs),
        Text(
          subtitle,
          style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                color: c.accentBrownMuted,
              ),
        ),
      ],
    );
  }
}

class _UnlinkedAccountsStateCard extends StatelessWidget {
  const _UnlinkedAccountsStateCard();

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Container(
      decoration: BoxDecoration(
        color: c.spendingCardWarmElevated,
        borderRadius: PayaboRadii.radiusSm,
        border: Border.all(color: c.spendingQuickActionBorder),
        boxShadow: PayaboShadows.soft,
      ),
      padding: const EdgeInsets.all(PayaboSpacing.xl),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Container(
            width: 56,
            height: 56,
            decoration: BoxDecoration(
              color: c.primary.withValues(alpha: 0.14),
              borderRadius: BorderRadius.circular(20),
            ),
            child: Icon(
              Icons.link_outlined,
              color: c.primary,
              size: 28,
            ),
          ),
          const SizedBox(height: PayaboSpacing.lg),
          Text(
            'No linked accounts yet',
            style: Theme.of(context).textTheme.titleLarge?.copyWith(
                  color: c.accentBrown,
                  fontWeight: FontWeight.w700,
                ),
          ),
          const SizedBox(height: PayaboSpacing.sm),
          Text(
            'Connect a bank or add a manual account to widen spend coverage, improve merchant rollups, and keep budgets closer to reality.',
            style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                  color: c.muted,
                  height: 1.45,
                ),
          ),
          const SizedBox(height: PayaboSpacing.lg),
          const _SetupStepRow(
            icon: Icons.account_balance_outlined,
            title: 'Link a bank securely',
            subtitle:
                'Use a provider-backed session without exposing credentials in the app.',
          ),
          const SizedBox(height: PayaboSpacing.md),
          const _SetupStepRow(
            icon: Icons.insights_outlined,
            title: 'Improve spending insight quality',
            subtitle:
                'Bring in richer category, merchant, and trend coverage for Spend.',
          ),
          const SizedBox(height: PayaboSpacing.md),
          const _SetupStepRow(
            icon: Icons.upload_file_outlined,
            title: 'Fallback with statements or manual accounts',
            subtitle:
                'Keep the page useful before full live-bank linking is switched on.',
          ),
        ],
      ),
    );
  }
}

class _FreshAccountsStateCard extends StatelessWidget {
  const _FreshAccountsStateCard();

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Container(
      decoration: BoxDecoration(
        color: c.spendingCardWarmElevated,
        borderRadius: PayaboRadii.radiusSm,
        border: Border.all(color: c.spendingQuickActionBorder),
        boxShadow: PayaboShadows.soft,
      ),
      padding: const EdgeInsets.all(PayaboSpacing.xl),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Container(
            width: 56,
            height: 56,
            decoration: BoxDecoration(
              color: c.primary.withValues(alpha: c.isDark ? 0.14 : 0.12),
              borderRadius: BorderRadius.circular(20),
            ),
            child: Icon(
              Icons.wallet_outlined,
              color: c.primary,
              size: 28,
            ),
          ),
          const SizedBox(height: PayaboSpacing.lg),
          Text(
            'Fresh accounts state',
            style: Theme.of(context).textTheme.titleLarge?.copyWith(
                  color: c.accentBrown,
                  fontWeight: FontWeight.w700,
                ),
          ),
          const SizedBox(height: PayaboSpacing.sm),
          Text(
            'Fresh demo mode removes the seeded account showcase so this page starts empty and ready for your first secure connection or manual account.',
            style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                  color: c.muted,
                  height: 1.45,
                ),
          ),
          const SizedBox(height: PayaboSpacing.lg),
          Text(
            'Switch back to Populated demo data in Profile if you want to review linked-account examples again.',
            style: Theme.of(context).textTheme.bodySmall?.copyWith(
                  color: c.chatTextSecondary,
                ),
          ),
        ],
      ),
    );
  }
}

class _SetupStepRow extends StatelessWidget {
  const _SetupStepRow({
    required this.icon,
    required this.title,
    required this.subtitle,
  });

  final IconData icon;
  final String title;
  final String subtitle;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Container(
          width: 42,
          height: 42,
          decoration: BoxDecoration(
            color: c.surfaceBase,
            borderRadius: BorderRadius.circular(16),
            border: Border.all(color: c.spendingQuickActionBorder),
          ),
          child: Icon(icon, color: c.primary, size: 22),
        ),
        const SizedBox(width: PayaboSpacing.md),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Text(
                title,
                style: Theme.of(context).textTheme.titleSmall?.copyWith(
                      color: c.accentBrown,
                      fontWeight: FontWeight.w700,
                    ),
              ),
              const SizedBox(height: PayaboSpacing.xxs),
              Text(
                subtitle,
                style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color: c.accentBrownMuted,
                      height: 1.45,
                    ),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

class _AccountLinkCard extends StatelessWidget {
  const _AccountLinkCard({
    required this.item,
    required this.isBusy,
    required this.onReconnectTap,
    required this.onRefreshTap,
    required this.onDisconnectTap,
    required this.onManageTap,
  });

  final AccountLinkItem item;
  final bool isBusy;
  final VoidCallback onReconnectTap;
  final VoidCallback onRefreshTap;
  final VoidCallback onDisconnectTap;
  final VoidCallback onManageTap;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final Color accentColor = _statusColor(context, item.status);

    return Container(
      key: Key('account-card-${item.id}'),
      decoration: BoxDecoration(
        color: c.surfaceBase,
        borderRadius: PayaboRadii.radiusSm,
        border: Border.all(
          color: item.needsReconnect
              ? c.spendingInsightBorder
              : c.spendingQuickActionBorder,
        ),
        boxShadow: PayaboShadows.soft,
      ),
      child: Padding(
        padding: const EdgeInsets.all(PayaboSpacing.lg),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                _AccountIcon(item: item, accentColor: accentColor),
                const SizedBox(width: PayaboSpacing.md),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Text(
                        item.institutionName,
                        style: Theme.of(context).textTheme.bodySmall?.copyWith(
                              color: c.accentBrownMuted,
                              fontWeight: FontWeight.w700,
                            ),
                      ),
                      const SizedBox(height: PayaboSpacing.xxs),
                      Text(
                        item.name,
                        style:
                            Theme.of(context).textTheme.titleMedium?.copyWith(
                                  color: c.accentBrown,
                                  fontWeight: FontWeight.w700,
                                ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(width: PayaboSpacing.sm),
                _StatusPill(
                  label: item.statusLabel,
                  color: accentColor,
                ),
              ],
            ),
            const SizedBox(height: PayaboSpacing.lg),
            Row(
              crossAxisAlignment: CrossAxisAlignment.end,
              children: <Widget>[
                Expanded(
                  child: Text(
                    item.balanceLabel ?? item.accountTypeLabel,
                    style: Theme.of(context).textTheme.displaySmall?.copyWith(
                          color: c.accentBrown,
                          fontSize: item.balanceLabel == null ? 28 : 32,
                          height: 1,
                        ),
                  ),
                ),
                if (item.balanceLabel != null)
                  Text(
                    item.accountTypeLabel,
                    style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                          color: c.accentBrownMuted,
                          fontWeight: FontWeight.w600,
                        ),
                  ),
              ],
            ),
            const SizedBox(height: PayaboSpacing.sm),
            Text(
              item.statusDetail,
              style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                    color: c.muted,
                    height: 1.45,
                  ),
            ),
            const SizedBox(height: PayaboSpacing.lg),
            Wrap(
              spacing: PayaboSpacing.sm,
              runSpacing: PayaboSpacing.sm,
              children: <Widget>[
                _MetaChip(label: item.sourceLabel),
                _MetaChip(label: item.accountTypeLabel),
                _MetaChip(label: item.currencyCode),
                if (item.maskedIdentifier != null)
                  _MetaChip(label: item.maskedIdentifier!),
                if (item.lastSyncedLabel != null)
                  _MetaChip(label: item.lastSyncedLabel!),
              ],
            ),
            const SizedBox(height: PayaboSpacing.lg),
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
                PayaboButton(
                  label: item.source == AccountLinkSource.linked
                      ? 'Manage'
                      : 'View details',
                  variant: PayaboButtonVariant.link,
                  size: PayaboButtonSize.sm,
                  expand: false,
                  onPressed: isBusy ? null : onManageTap,
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  Color _statusColor(BuildContext context, AccountLinkStatus status) {
    final c = context.colors;

    switch (status) {
      case AccountLinkStatus.connected:
        return c.success;
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

class _AccountIcon extends StatelessWidget {
  const _AccountIcon({
    required this.item,
    required this.accentColor,
  });

  final AccountLinkItem item;
  final Color accentColor;

  @override
  Widget build(BuildContext context) {
    final IconData icon;

    if (item.source == AccountLinkSource.manual) {
      icon = Icons.edit_note_outlined;
    } else if (item.accountTypeLabel.toLowerCase().contains('savings')) {
      icon = Icons.savings_outlined;
    } else if (item.accountTypeLabel.toLowerCase().contains('credit')) {
      icon = Icons.credit_card_outlined;
    } else {
      icon = Icons.account_balance_wallet_outlined;
    }

    return Container(
      width: 48,
      height: 48,
      decoration: BoxDecoration(
        color: accentColor.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(18),
      ),
      child: Icon(icon, color: accentColor, size: 24),
    );
  }
}

class _StatusPill extends StatelessWidget {
  const _StatusPill({
    required this.label,
    required this.color,
  });

  final String label;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return DecoratedBox(
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
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
                color: color,
                fontWeight: FontWeight.w700,
              ),
        ),
      ),
    );
  }
}

class _MetaChip extends StatelessWidget {
  const _MetaChip({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return DecoratedBox(
      decoration: BoxDecoration(
        color: c.spendingCardWarm,
        borderRadius: BorderRadius.circular(PayaboRadii.pill),
        border: Border.all(color: c.spendingQuickActionBorder),
      ),
      child: Padding(
        padding: const EdgeInsets.symmetric(
          horizontal: PayaboSpacing.md,
          vertical: PayaboSpacing.sm,
        ),
        child: Text(
          label,
          style: Theme.of(context).textTheme.bodySmall?.copyWith(
                color: c.accentBrownMuted,
                fontWeight: FontWeight.w600,
              ),
        ),
      ),
    );
  }
}

class _AccountsExplainerCard extends StatelessWidget {
  const _AccountsExplainerCard();

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Container(
      decoration: BoxDecoration(
        gradient: LinearGradient(
          colors: c.isDark
              ? <Color>[c.surfaceCardElevated, c.surfaceWarmElevated]
              : const <Color>[Color(0xFFFFFCF7), Color(0xFFFFF2E3)],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        borderRadius: PayaboRadii.radiusSm,
        border: Border.all(color: c.borderWarm),
        boxShadow: PayaboShadows.soft,
      ),
      child: Padding(
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
                    color: c.surfaceBase.withValues(alpha: 0.82),
                    borderRadius: BorderRadius.circular(18),
                  ),
                  child: Icon(
                    Icons.shield_outlined,
                    color: c.primary,
                  ),
                ),
                const SizedBox(width: PayaboSpacing.md),
                Expanded(
                  child: Text(
                    'How secure account connections will work',
                    style: Theme.of(context).textTheme.titleLarge?.copyWith(
                          color: c.accentBrown,
                          fontWeight: FontWeight.w700,
                        ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: PayaboSpacing.lg),
            Text(
              'Payabo will request a short-lived secure session, open the provider link flow, and hand the temporary result back to AONIK. Long-lived provider credentials stay on the server, not in the mobile app.',
              style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                    color: c.accentBrownMuted,
                    height: 1.5,
                  ),
            ),
            const SizedBox(height: PayaboSpacing.lg),
            const _SetupStepRow(
              icon: Icons.privacy_tip_outlined,
              title: 'Server-side secret handling',
              subtitle:
                  'Provider secrets and access tokens remain in protected backend storage only.',
            ),
            const SizedBox(height: PayaboSpacing.md),
            const _SetupStepRow(
              icon: Icons.sync_outlined,
              title: 'Provider-agnostic connection layer',
              subtitle:
                  'Plaid can be the first adapter without locking Payabo into a single provider.',
            ),
          ],
        ),
      ),
    );
  }
}

class _AccountsLoadErrorCard extends StatelessWidget {
  const _AccountsLoadErrorCard({
    required this.message,
    required this.onRetry,
  });

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return PayaboCard(
      backgroundColor: c.spendingCardWarmElevated,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(
            'Unable to load accounts right now',
            style: Theme.of(context).textTheme.titleLarge?.copyWith(
                  color: c.accentBrown,
                  fontWeight: FontWeight.w700,
                ),
          ),
          const SizedBox(height: PayaboSpacing.sm),
          Text(
            message,
            style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                  color: c.muted,
                  height: 1.45,
                ),
          ),
          const SizedBox(height: PayaboSpacing.lg),
          PayaboButton(
            label: 'Try again',
            variant: PayaboButtonVariant.secondary,
            onPressed: onRetry,
          ),
        ],
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
              boxShadow: PayaboShadows.soft,
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
