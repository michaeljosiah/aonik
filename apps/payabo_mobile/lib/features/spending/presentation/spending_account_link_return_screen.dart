import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../shared/theme/payabo_colors.dart';
import '../../../shared/theme/payabo_gradients.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_button.dart';
import '../../../shared/widgets/payabo_primary_app_shell.dart';
import 'spending_accounts_state.dart';

enum _AccountLinkReturnStatus {
  loading,
  success,
  cancelled,
  error,
}

class SpendingAccountLinkReturnScreen extends ConsumerStatefulWidget {
  const SpendingAccountLinkReturnScreen({
    super.key,
    required this.redirectUri,
  });

  final String redirectUri;

  @override
  ConsumerState<SpendingAccountLinkReturnScreen> createState() =>
      _SpendingAccountLinkReturnScreenState();
}

class _SpendingAccountLinkReturnScreenState
    extends ConsumerState<SpendingAccountLinkReturnScreen> {
  _AccountLinkReturnStatus _status = _AccountLinkReturnStatus.loading;
  String? _message;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _resume());
  }

  Future<void> _resume() async {
    try {
      final result = await ref
          .read(accountLinkFlowControllerProvider.notifier)
          .resumeOAuthRedirect(widget.redirectUri);

      if (!mounted) {
        return;
      }

      setState(() {
        if (result == null) {
          _status = _AccountLinkReturnStatus.cancelled;
          _message =
              'The secure bank connection was cancelled before it completed.';
        } else {
          _status = _AccountLinkReturnStatus.success;
          _message =
              'Connected ${result.linkedAccountCount} account${result.linkedAccountCount == 1 ? '' : 's'} from ${result.institutionName}.';
        }
      });
    } catch (_) {
      if (!mounted) {
        return;
      }

      setState(() {
        _status = _AccountLinkReturnStatus.error;
        _message = ref.read(accountLinkFlowControllerProvider).errorMessage ??
            'We could not resume the secure bank-link session.';
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: PayaboColors.surfaceWarm,
      body: DecoratedBox(
        decoration: const BoxDecoration(
          gradient: PayaboGradients.warmScreen,
        ),
        child: SafeArea(
          child: Padding(
            padding: const EdgeInsets.all(PayaboSpacing.xl),
            child: Center(
              child: ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 420),
                child: DecoratedBox(
                  decoration: BoxDecoration(
                    color: PayaboColors.white,
                    borderRadius: BorderRadius.circular(28),
                    border: Border.all(
                      color: PayaboColors.spendingQuickActionBorder,
                    ),
                  ),
                  child: Padding(
                    padding: const EdgeInsets.all(PayaboSpacing.xl),
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: <Widget>[
                        Text(
                          'Resume bank connection',
                          style: Theme.of(context)
                              .textTheme
                              .headlineSmall
                              ?.copyWith(
                                color: PayaboColors.accentBrown,
                                fontWeight: FontWeight.w700,
                              ),
                        ),
                        const SizedBox(height: PayaboSpacing.md),
                        ..._buildBody(context),
                      ],
                    ),
                  ),
                ),
              ),
            ),
          ),
        ),
      ),
      bottomNavigationBar: const PayaboPrimaryAppShell(
        destination: PayaboPrimaryDestination.spending,
      ),
    );
  }

  List<Widget> _buildBody(BuildContext context) {
    switch (_status) {
      case _AccountLinkReturnStatus.loading:
        return <Widget>[
          Text(
            'Finishing the secure provider handoff and syncing your linked accounts back into Spend.',
            style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                  color: PayaboColors.accentBrownMuted,
                  height: 1.45,
                ),
          ),
          const SizedBox(height: PayaboSpacing.lg),
          const Center(child: CircularProgressIndicator()),
        ];
      case _AccountLinkReturnStatus.success:
        return _buildResultBody(context, actionLabel: 'Open accounts');
      case _AccountLinkReturnStatus.cancelled:
        return _buildResultBody(context, actionLabel: 'Back to accounts');
      case _AccountLinkReturnStatus.error:
        return _buildResultBody(context,
            actionLabel: 'Try again from accounts');
    }
  }

  List<Widget> _buildResultBody(
    BuildContext context, {
    required String actionLabel,
  }) {
    return <Widget>[
      Text(
        _message ?? '',
        style: Theme.of(context).textTheme.bodyMedium?.copyWith(
              color: PayaboColors.accentBrownMuted,
              height: 1.45,
            ),
      ),
      const SizedBox(height: PayaboSpacing.lg),
      PayaboButton(
        label: actionLabel,
        onPressed: () => context.go('/spending/accounts'),
      ),
    ];
  }
}
