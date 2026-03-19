import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/legacy.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'demo_mode.dart';

const String _demoDataModeKey = 'app.demoDataMode';

enum DemoDataMode {
  fresh,
  populated,
}

extension DemoDataModeCopy on DemoDataMode {
  String get label {
    switch (this) {
      case DemoDataMode.fresh:
        return 'Fresh demo state';
      case DemoDataMode.populated:
        return 'Populated demo data';
    }
  }

  String get profileMenuSubtitle {
    switch (this) {
      case DemoDataMode.fresh:
        return 'Start from a clean empty demo state';
      case DemoDataMode.populated:
        return 'Use the seeded sample data';
    }
  }

  String get description {
    switch (this) {
      case DemoDataMode.fresh:
        return 'User profile present, empty dashboard content, and no saved cards or friends in supported demo flows.';
      case DemoDataMode.populated:
        return 'Use the seeded sample data that the app currently ships with in supported demo flows.';
    }
  }
}

Future<DemoDataMode> loadInitialDemoDataMode() async {
  final prefs = await SharedPreferences.getInstance();
  return demoDataModeFromStorage(prefs.getString(_demoDataModeKey));
}

DemoDataMode demoDataModeFromStorage(String? rawValue) {
  switch (rawValue) {
    case 'fresh':
      return DemoDataMode.fresh;
    case 'populated':
    default:
      return DemoDataMode.populated;
  }
}

final Provider<DemoDataMode> initialDemoDataModeProvider =
    Provider<DemoDataMode>(
  (Ref ref) => DemoDataMode.populated,
);

class DemoDataModeController extends StateNotifier<DemoDataMode> {
  DemoDataModeController(super.state);

  Future<void> setMode(DemoDataMode mode) async {
    if (state == mode) {
      return;
    }

    state = mode;
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_demoDataModeKey, mode.name);
  }
}

final StateNotifierProvider<DemoDataModeController, DemoDataMode>
    demoDataModePreferenceProvider =
    StateNotifierProvider<DemoDataModeController, DemoDataMode>(
  (Ref ref) => DemoDataModeController(ref.read(initialDemoDataModeProvider)),
);

final Provider<DemoDataMode> demoDataModeProvider = Provider<DemoDataMode>(
  (Ref ref) {
    final storedMode = ref.watch(demoDataModePreferenceProvider);
    final isDemo = ref.watch(isDemoProvider);

    return isDemo ? storedMode : DemoDataMode.populated;
  },
);
