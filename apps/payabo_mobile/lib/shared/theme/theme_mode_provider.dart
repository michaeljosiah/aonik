import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/legacy.dart';
import 'package:shared_preferences/shared_preferences.dart';

const String _themeModeKey = 'app.themeMode';

/// Persisted theme-mode controller.
///
/// The user can toggle between light (warm) and dark themes.
/// The selection is stored in [SharedPreferences] so it survives restarts.
class ThemeModeController extends StateNotifier<ThemeMode> {
  ThemeModeController(super.initial);

  Future<void> setMode(ThemeMode mode) async {
    if (state == mode) return;
    state = mode;
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_themeModeKey, mode.name);
  }

  Future<void> toggle() async {
    final next =
        state == ThemeMode.dark ? ThemeMode.light : ThemeMode.dark;
    await setMode(next);
  }
}

/// Read the persisted theme mode before the app starts.
Future<ThemeMode> loadInitialThemeMode() async {
  final prefs = await SharedPreferences.getInstance();
  final raw = prefs.getString(_themeModeKey);
  switch (raw) {
    case 'dark':
      return ThemeMode.dark;
    case 'light':
      return ThemeMode.light;
    default:
      return ThemeMode.light;
  }
}

/// Override this in [ProviderScope.overrides] during bootstrap.
final Provider<ThemeMode> initialThemeModeProvider = Provider<ThemeMode>(
  (Ref ref) => ThemeMode.light,
);

/// The live theme-mode provider used by [PayaboApp].
final StateNotifierProvider<ThemeModeController, ThemeMode>
    themeModeProvider =
    StateNotifierProvider<ThemeModeController, ThemeMode>(
  (Ref ref) => ThemeModeController(ref.read(initialThemeModeProvider)),
);
