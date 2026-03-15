import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/legacy.dart';

import '../../setup_journey/application/setup_journey_controller.dart';
import '../../setup_journey/domain/setup_models.dart';

// ── Seed consumption ────────────────────────────────────────

/// Convenience re-export of the dashboard seed built from setup profile.
///
/// Dashboard widgets should read this provider to personalise layout,
/// greeting, and module ordering based on setup signals.
final Provider<DashboardSetupSeed> dashboardSeedProvider =
    Provider<DashboardSetupSeed>((Ref ref) {
  return ref.watch(setupDashboardSeedProvider);
});

// ── Simi card state ─────────────────────────────────────────

/// Whether the user has dismissed the Simi introduction card
/// during this session. Resets on app restart — intentionally
/// ephemeral so Simi re-appears if the user hasn't engaged.
final StateProvider<bool> simiCardDismissedProvider =
    StateProvider<bool>((Ref ref) => false);

/// Whether the Simi dashboard card should be visible.
///
/// The card shows when:
/// - Setup is completed (seed has meaningful data)
/// - User hasn't dismissed the card this session
final Provider<bool> simiCardVisibleProvider = Provider<bool>((Ref ref) {
  final profile = ref.watch(setupJourneyControllerProvider).profile;
  if (!profile.completed) return false;

  final dismissed = ref.watch(simiCardDismissedProvider);
  return !dismissed;
});
