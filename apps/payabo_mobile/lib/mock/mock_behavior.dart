class MockBehavior {
  const MockBehavior._();

  static const int latencyMs =
      int.fromEnvironment('MOCK_LATENCY_MS', defaultValue: 250);
  static const bool forceFailure =
      bool.fromEnvironment('MOCK_FORCE_FAILURE', defaultValue: false);

  static Duration get latency {
    if (latencyMs <= 0) {
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

  static void throwIfEnabled(String operation) {
    if (forceFailure) {
      throw StateError('Forced mock failure: $operation');
    }
  }
}
