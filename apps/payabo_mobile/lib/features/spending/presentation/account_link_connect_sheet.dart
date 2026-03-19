import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../data/repositories/account_links_repository.dart';
import '../../../shared/reference/payabo_country_reference.dart';
import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_radii.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_button.dart';
import '../../../shared/widgets/payabo_modal_sheet.dart';
import '../../profile/presentation/profile_state.dart';
import 'spending_accounts_state.dart';

const List<PayaboCountryReference> _plaidSupportedCountries =
    <PayaboCountryReference>[
  payaboCountryUnitedKingdom,
  payaboCountryUnitedStates,
];

Future<AccountLinkExchangeResult?> showAccountLinkConnectSheet(
  BuildContext context,
  WidgetRef ref, {
  required String provider,
  required String mode,
  required String title,
  String? connectionId,
}) {
  return showPayaboModalSheet<AccountLinkExchangeResult>(
    context: context,
    title: title,
    child: AccountLinkConnectSheet(
      provider: provider,
      mode: mode,
      connectionId: connectionId,
    ),
  );
}

class AccountLinkConnectSheet extends ConsumerStatefulWidget {
  const AccountLinkConnectSheet({
    super.key,
    required this.provider,
    required this.mode,
    this.connectionId,
  });

  final String provider;
  final String mode;
  final String? connectionId;

  @override
  ConsumerState<AccountLinkConnectSheet> createState() =>
      _AccountLinkConnectSheetState();
}

class _AccountLinkConnectSheetState
    extends ConsumerState<AccountLinkConnectSheet> {
  String? _selectedCountryCode;

  bool get _requiresCountrySelection =>
      widget.mode == 'connect' && widget.provider.toLowerCase() == 'plaid';

  @override
  void initState() {
    super.initState();
    ref.read(accountLinkFlowControllerProvider.notifier).reset();

    final String profileCountryCode =
        ref.read(profileCoreProvider).countryCode.trim().toUpperCase();
    final bool hasProfileCountry = _plaidSupportedCountries.any(
      (PayaboCountryReference country) => country.code == profileCountryCode,
    );
    _selectedCountryCode = hasProfileCountry ? profileCountryCode : 'GB';
  }

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
            : 'Choose the bank country first, then open a secure ${launcher.experienceLabel} session to connect the right institution. Payabo exchanges the temporary result with AONIK so Spend can refresh linked accounts safely.'
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
                  'Choose where the bank account is held before Payabo opens Plaid. The secure bank-link flow currently supports United Kingdom and United States institutions only.',
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
                  items: _plaidSupportedCountries
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
