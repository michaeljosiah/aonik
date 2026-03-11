import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Runtime flag activated when the API health check fails at startup.
/// When true, all repository providers fall back to mock implementations
/// and the user is notified they are operating in demo mode.
///
/// This is independent of the compile-time `USE_MOCKS` flag; it handles
/// the case where the app was built for a live backend but the backend
/// is currently unreachable.
final StateProvider<bool> offlineModeProvider = StateProvider<bool>(
  (Ref ref) => false,
);
