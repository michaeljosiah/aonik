import 'dart:developer' as developer;

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/legacy.dart';
import 'package:local_auth/local_auth.dart';

import '../../features/profile/presentation/profile_state.dart';
import '../demo/demo_mode.dart';
import 'auth_controller.dart';

/// Duration the app must be backgrounded before re-locking.
const Duration kBackgroundLockDelay = Duration(seconds: 30);

class AppLockState {
  const AppLockState({
    required this.isLocked,
    required this.isCheckingBiometric,
    this.errorMessage,
  });

  final bool isLocked;
  final bool isCheckingBiometric;
  final String? errorMessage;

  factory AppLockState.initial() {
    return const AppLockState(
      isLocked: false,
      isCheckingBiometric: false,
    );
  }

  AppLockState copyWith({
    bool? isLocked,
    bool? isCheckingBiometric,
    String? errorMessage,
    bool clearError = false,
  }) {
    return AppLockState(
      isLocked: isLocked ?? this.isLocked,
      isCheckingBiometric: isCheckingBiometric ?? this.isCheckingBiometric,
      errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
    );
  }
}

class AppLockController extends StateNotifier<AppLockState> {
  AppLockController(this._ref) : super(AppLockState.initial()) {
    _init();
  }

  final Ref _ref;
  final LocalAuthentication _localAuth = LocalAuthentication();

  static const String _logName = 'Payabo.AppLock';

  void _init() {
    // Check the current auth state immediately — if the session was already
    // hydrated before this controller was created, the listener below would
    // miss the uninitialized → initialized transition.
    final current = _ref.read(authControllerProvider);
    if (current.isInitialized && current.isAuthenticated) {
      _lockIfBiometricEnabled();
    }

    // Watch for future auth transitions.
    _ref.listen<AuthState>(authControllerProvider, (previous, next) {
      // Auth just finished hydrating with a valid session → lock.
      if (previous != null &&
          !previous.isInitialized &&
          next.isInitialized &&
          next.isAuthenticated) {
        _lockIfBiometricEnabled();
      }

      // Auto-unlock when the user signs out.
      if (previous != null &&
          previous.isAuthenticated &&
          !next.isAuthenticated) {
        unlock();
      }
    });

    // The biometric preference loads asynchronously from secure storage.
    // Its initial value is false; once the real value arrives, re-evaluate.
    _ref.listen<bool>(biometricPreferenceProvider, (previous, next) {
      if (next && !state.isLocked) {
        final auth = _ref.read(authControllerProvider);
        if (auth.isInitialized && auth.isAuthenticated) {
          final bool isDemo = _ref.read(isDemoProvider);
          if (!isDemo) {
            lock();
          }
        }
      }
    });
  }

  void _lockIfBiometricEnabled() {
    final bool isDemo = _ref.read(isDemoProvider);
    if (isDemo) return;

    final bool biometricEnabled = _ref.read(biometricPreferenceProvider);
    if (biometricEnabled) {
      lock();
    }
  }

  void lock() {
    developer.log('Locking app', name: _logName);
    state = state.copyWith(isLocked: true, clearError: true);
  }

  void unlock() {
    developer.log('Unlocking app', name: _logName);
    state = state.copyWith(
      isLocked: false,
      isCheckingBiometric: false,
      clearError: true,
    );
  }

  /// Called when the app returns from background after exceeding the timeout.
  void lockIfEnabled() {
    final authState = _ref.read(authControllerProvider);
    if (!authState.isAuthenticated) return;

    _lockIfBiometricEnabled();
  }

  Future<void> attemptBiometricUnlock() async {
    if (state.isCheckingBiometric) return;

    state = state.copyWith(isCheckingBiometric: true, clearError: true);

    try {
      final bool canCheck = await _localAuth.canCheckBiometrics;
      final bool isSupported = await _localAuth.isDeviceSupported();

      if (!canCheck && !isSupported) {
        state = state.copyWith(
          isCheckingBiometric: false,
          errorMessage:
              'Biometric authentication is not available on this device.',
        );
        return;
      }

      final bool didAuthenticate = await _localAuth.authenticate(
        localizedReason: 'Unlock Payabo',
        biometricOnly: false,
        persistAcrossBackgrounding: true,
      );

      if (didAuthenticate) {
        unlock();
      } else {
        state = state.copyWith(
          isCheckingBiometric: false,
          errorMessage: 'Authentication cancelled. Tap to try again.',
        );
      }
    } catch (e) {
      developer.log('Biometric error: $e', name: _logName);
      state = state.copyWith(
        isCheckingBiometric: false,
        errorMessage: 'Authentication failed. Tap to try again.',
      );
    }
  }
}

final StateNotifierProvider<AppLockController, AppLockState>
    appLockControllerProvider =
    StateNotifierProvider<AppLockController, AppLockState>(
  AppLockController.new,
);
