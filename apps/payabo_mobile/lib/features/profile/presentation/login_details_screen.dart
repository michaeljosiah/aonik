import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:local_auth/local_auth.dart';

import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_card.dart';
import '../../../shared/widgets/payabo_list_row.dart';
import 'profile_scaffold.dart';
import 'profile_state.dart';

class ProfileLoginDetailsScreen extends ConsumerStatefulWidget {
  const ProfileLoginDetailsScreen({super.key});

  @override
  ConsumerState<ProfileLoginDetailsScreen> createState() =>
      _ProfileLoginDetailsScreenState();
}

class _ProfileLoginDetailsScreenState
    extends ConsumerState<ProfileLoginDetailsScreen> {
  final LocalAuthentication _localAuth = LocalAuthentication();

  Future<void> _onTouchIdChanged(bool value) async {
    if (value) {
      // When enabling, verify biometrics first.
      try {
        final canCheck = await _localAuth.canCheckBiometrics;
        final isDeviceSupported = await _localAuth.isDeviceSupported();

        if (!canCheck || !isDeviceSupported) {
          if (mounted) {
            ScaffoldMessenger.of(context)
              ..hideCurrentSnackBar()
              ..showSnackBar(
                const SnackBar(
                  content: Text(
                      'Biometric authentication is not available on this device.'),
                ),
              );
          }
          return;
        }

        final didAuthenticate = await _localAuth.authenticate(
          localizedReason: 'Authenticate to enable Touch ID for Payabo',
          biometricOnly: true,
          persistAcrossBackgrounding: true,
        );

        if (!didAuthenticate) {
          return;
        }
      } catch (_) {
        if (mounted) {
          ScaffoldMessenger.of(context)
            ..hideCurrentSnackBar()
            ..showSnackBar(
              const SnackBar(
                content: Text('Biometric authentication failed.'),
              ),
            );
        }
        return;
      }
    }

    await ref.read(biometricPreferenceProvider.notifier).setTouchId(value);
  }

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final state = ref.watch(profileLoginDetailsProvider);

    return ProfileScaffold(
      title: 'Login details',
      backRoute: '/profile',
      child: Column(
        children: <Widget>[
          PayaboListRow(
            title: 'Email',
            subtitle: state.email,
            onTap: () => context.go('/profile/login-details/email'),
          ),
          const SizedBox(height: PayaboSpacing.sm),
          PayaboListRow(
            title: 'Password',
            subtitle: '\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022',
            onTap: () => context.go('/profile/login-details/password'),
          ),
          const SizedBox(height: PayaboSpacing.sm),
          PayaboCard(
            child: Row(
              children: <Widget>[
                const Expanded(child: Text('Touch ID')),
                Switch.adaptive(
                  value: state.touchIdEnabled,
                  onChanged: _onTouchIdChanged,
                  activeThumbColor: c.surfaceBase,
                  activeTrackColor: c.success,
                  inactiveThumbColor: c.surfaceBase,
                  inactiveTrackColor: c.background,
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
