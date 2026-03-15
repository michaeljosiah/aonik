import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/legacy.dart';

import '../../data/api/api_exception.dart';

/// A reported API error waiting to be shown to the user.
class ApiError {
  const ApiError({
    required this.message,
    required this.detail,
  });

  /// Short user-facing summary (e.g. "Payabo is having trouble right now").
  final String message;

  /// Raw detail string for developer context (status code, operation, etc.).
  final String? detail;
}

/// Holds the queue of [ApiError]s that have not yet been acknowledged.
///
/// Controllers that encounter API failures they would otherwise swallow silently
/// should call [ApiErrorNotifier.report] so the error surfaces in the UI.
/// The app-level listener shows a dialog for each queued error and calls
/// [dismiss] when the user taps OK.
class ApiErrorNotifier extends StateNotifier<List<ApiError>> {
  ApiErrorNotifier() : super(const []);

  /// Add [error] to the display queue.
  void report(Object error) {
    final ApiError entry;

    if (error is ApiException) {
      entry = ApiError(
        message: error.message,
        detail: error.statusCode != null ? 'HTTP ${error.statusCode}' : null,
      );
    } else {
      entry = ApiError(
        message: 'An unexpected error occurred.',
        detail: error.toString(),
      );
    }

    state = [...state, entry];
  }

  /// Remove the oldest queued error (call after the user dismisses the dialog).
  void dismiss() {
    if (state.isNotEmpty) {
      state = state.sublist(1);
    }
  }
}

final StateNotifierProvider<ApiErrorNotifier, List<ApiError>>
    apiErrorNotifierProvider =
    StateNotifierProvider<ApiErrorNotifier, List<ApiError>>(
  (Ref ref) => ApiErrorNotifier(),
);
