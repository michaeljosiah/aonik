class MockBehavior {
  const MockBehavior._();

  /// Set to `true` in test helpers so that [delay] and [shortDelay] resolve
  /// synchronously, preventing stale `Future.delayed` timers from lingering
  /// inside `FakeAsync` zones.
  static bool skipDelayForTest = false;

  static const int latencyMs =
      int.fromEnvironment('MOCK_LATENCY_MS', defaultValue: 250);
  static const bool forceFailure =
      bool.fromEnvironment('MOCK_FORCE_FAILURE', defaultValue: false);

  static Duration get latency {
    if (skipDelayForTest || latencyMs <= 0) {
      return Duration.zero;
    }

    return const Duration(milliseconds: latencyMs);
  }

  static Future<void> delay() async {
    final duration = latency;
    if (duration == Duration.zero) {
      return;
    }

    await Future<void>.delayed(duration);
  }

  /// A shorter delay used for streaming animations. Respects
  /// [skipDelayForTest] so tests never leave pending timers.
  static Future<void> shortDelay([int ms = 15]) async {
    if (skipDelayForTest) return;
    await Future<void>.delayed(Duration(milliseconds: ms));
  }

  static void throwIfEnabled(String operation) {
    if (forceFailure) {
      throw StateError('Forced mock failure: $operation');
    }
  }
}
