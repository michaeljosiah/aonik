import 'package:flutter_riverpod/flutter_riverpod.dart';

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
