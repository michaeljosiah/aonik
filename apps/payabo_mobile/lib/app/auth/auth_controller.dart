import 'dart:async';

import 'package:firebase_analytics/firebase_analytics.dart';
import 'package:firebase_core/firebase_core.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/legacy.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../../data/api/api_exception.dart';
import '../../data/repositories/auth_repository.dart';
import '../../data/repositories/repository_providers.dart';
import '../../features/payments/presentation/payment_flow_persistence.dart';
import '../../features/payments/presentation/payment_flow_state.dart';
import '../../features/profile/presentation/profile_state.dart';
import '../../features/setup_journey/application/setup_journey_controller.dart';
import '../../features/spending/presentation/spending_account_link_persistence.dart';
import '../../features/spending/presentation/spending_accounts_state.dart';
import 'auth_session_store.dart';

class AuthUser {
  const AuthUser({
    required this.userId,
    required this.email,
    this.firstName,
    this.lastName,
  });

  final String userId;
  final String email;
  final String? firstName;
  final String? lastName;

  String get displayName {
    final parts = <String>[
      if (firstName != null && firstName!.trim().isNotEmpty) firstName!.trim(),
      if (lastName != null && lastName!.trim().isNotEmpty) lastName!.trim(),
    ];

    if (parts.isNotEmpty) {
      return parts.join(' ');
    }

    return email;
  }
}

class AuthState {
  const AuthState({
    required this.isInitialized,
    required this.isAuthenticated,
    required this.isBusy,
    this.user,
    this.onboarding,
  });

  final bool isInitialized;
  final bool isAuthenticated;
  final bool isBusy;
  final AuthUser? user;
  final AuthOnboardingSnapshot? onboarding;

  factory AuthState.initial() {
    return const AuthState(
      isInitialized: false,
      isAuthenticated: false,
      isBusy: false,
      user: null,
      onboarding: null,
    );
  }

  AuthState copyWith({
    bool? isInitialized,
    bool? isAuthenticated,
    bool? isBusy,
    AuthUser? user,
    AuthOnboardingSnapshot? onboarding,
    bool clearUser = false,
    bool clearOnboarding = false,
  }) {
    return AuthState(
      isInitialized: isInitialized ?? this.isInitialized,
      isAuthenticated: isAuthenticated ?? this.isAuthenticated,
      isBusy: isBusy ?? this.isBusy,
      user: clearUser ? null : user ?? this.user,
      onboarding: clearOnboarding ? null : onboarding ?? this.onboarding,
    );
  }
}

class AuthController extends StateNotifier<AuthState> {
  AuthController(this._ref) : super(AuthState.initial()) {
    unawaited(_hydrateSession());
  }

  final Ref _ref;

  bool get _hasFirebaseApp => Firebase.apps.isNotEmpty;

  Future<void> _hydrateSession() async {
    final session = await _ref.read(authSessionStoreProvider).read();
    if (session == null || !session.hasAccessToken) {
      state = const AuthState(
        isInitialized: true,
        isAuthenticated: false,
        isBusy: false,
      );
      return;
    }

    if (session.isExpired &&
        (session.refreshToken == null ||
            session.refreshToken!.trim().isEmpty)) {
      await _clearSession();
      state = const AuthState(
        isInitialized: true,
        isAuthenticated: false,
        isBusy: false,
      );
      return;
    }

    state = state.copyWith(
      isInitialized: false,
      isAuthenticated: true,
      isBusy: true,
    );

    try {
      final userInfo = await _ref.read(authRepositoryProvider).getUserInfo();
      final onboarding = await _resolveOnboardingSnapshot();
      state = AuthState(
        isInitialized: true,
        isAuthenticated: true,
        isBusy: false,
        user: _mapUser(userInfo),
        onboarding: onboarding,
      );
    } on ApiException catch (exception) {
      if (_isAuthenticationFailure(exception.statusCode)) {
        await _clearSession();
        await _clearUserScopedState();
        state = const AuthState(
          isInitialized: true,
          isAuthenticated: false,
          isBusy: false,
        );
        return;
      }

      // Non-auth API error (e.g. 500): the token may still be valid,
      // but we cannot populate user info. Keep authenticated but surface
      // a null user so callers know to retry later.
      state = AuthState(
        isInitialized: true,
        isAuthenticated: true,
        isBusy: false,
        user: state.user,
      );
    }
  }

  Future<void> signInWithPassword({
    required String email,
    required String password,
  }) async {
    await _logAnalyticsEvent(
      'login_attempt',
      <String, Object>{
        'method': 'password',
      },
    );
    state = state.copyWith(isBusy: true);

    try {
      final repository = _ref.read(authRepositoryProvider);
      final token = await repository.signInWithPassword(
        email: email,
        password: password,
      );

      await _persistSession(token);

      // Reset any stale user-scoped state from the previous session before
      // the new user's profile and feature data are hydrated.
      await _clearUserScopedState();

      // Clear any stale setup state from a previous user on this device.
      await _clearSetupState();

      final user = await _resolveUserInfo(
        fallbackEmail: email.trim().toLowerCase(),
      );
      final onboarding = await _resolveOnboardingSnapshot();

      state = AuthState(
        isInitialized: true,
        isAuthenticated: true,
        isBusy: false,
        user: user,
        onboarding: onboarding,
      );
      await _logAnalyticsEvent(
        'login_success',
        <String, Object>{
          'method': 'password',
        },
      );
      await _setAnalyticsUser(user);
    } catch (error) {
      state = state.copyWith(isBusy: false);
      await _logAnalyticsEvent(
        'login_failure',
        <String, Object>{
          'method': 'password',
          'error_type': error.runtimeType.toString(),
        },
      );
      rethrow;
    }
  }

  Future<void> registerIndividual(RegisterIndividualRequest request) async {
    state = state.copyWith(isBusy: true);

    try {
      final repository = _ref.read(authRepositoryProvider);
      final onboarding = await repository.registerIndividual(request);

      final token = await repository.signInWithPassword(
        email: request.email,
        password: request.password,
      );

      await _persistSession(token);

      // Reset any stale user-scoped state from the previous session before
      // the new user's profile and feature data are hydrated.
      await _clearUserScopedState();

      // Clear any stale setup state from a previous user on this device
      // before resolving the new user's info. This prevents the router
      // from reading a cached setup-completed flag and skipping onboarding.
      await _clearSetupState();

      final user = await _resolveUserInfo(
        fallbackEmail: request.email.trim().toLowerCase(),
      );
      final resolvedOnboarding =
          onboarding ?? await _resolveOnboardingSnapshot();

      state = AuthState(
        isInitialized: true,
        isAuthenticated: true,
        isBusy: false,
        user: user,
        onboarding: resolvedOnboarding,
      );
    } catch (error) {
      state = state.copyWith(isBusy: false);
      rethrow;
    }
  }

  Future<void> sendPasswordResetEmail(String email) async {
    state = state.copyWith(isBusy: true);

    try {
      await _ref.read(authRepositoryProvider).sendPasswordResetEmail(email);
      state = state.copyWith(isBusy: false);
    } catch (error) {
      state = state.copyWith(isBusy: false);
      rethrow;
    }
  }

  Future<void> signOut() async {
    await _clearSession();
    await _clearUserScopedState();
    await _clearSetupState();
    await _clearAnalyticsUser();
    state = const AuthState(
      isInitialized: true,
      isAuthenticated: false,
      isBusy: false,
      onboarding: null,
    );
  }

  Future<void> _persistSession(AuthTokenResult token) async {
    final expiresAt = token.expiresIn <= 0
        ? null
        : DateTime.now().add(Duration(seconds: token.expiresIn));

    await _ref.read(authSessionStoreProvider).write(
          AuthSession(
            accessToken: token.accessToken,
            tokenType: token.tokenType,
            refreshToken: token.refreshToken,
            expiresAt: expiresAt,
          ),
        );
  }

  Future<AuthUser> _resolveUserInfo({required String fallbackEmail}) async {
    try {
      final userInfo = await _ref.read(authRepositoryProvider).getUserInfo();
      return _mapUser(userInfo);
    } on ApiException catch (exception) {
      if (_isAuthenticationFailure(exception.statusCode)) {
        await _clearSession();
        await _clearUserScopedState();
        rethrow;
      }

      return AuthUser(
        userId: '',
        email: fallbackEmail,
      );
    }
  }

  Future<AuthOnboardingSnapshot?> _resolveOnboardingSnapshot() async {
    try {
      return await _ref.read(authRepositoryProvider).getOnboardingSnapshot();
    } on ApiException {
      return null;
    }
  }

  AuthUser _mapUser(AuthUserInfo userInfo) {
    return AuthUser(
      userId: userInfo.userId,
      email: userInfo.email,
      firstName: userInfo.firstName,
      lastName: userInfo.lastName,
    );
  }

  bool _isAuthenticationFailure(int? statusCode) {
    return statusCode == 401 || statusCode == 403;
  }

  Future<void> _clearSession() {
    return _ref.read(authSessionStoreProvider).clear();
  }

  Future<void> _clearUserScopedState() async {
    await _ref.read(paymentFlowPersistenceProvider).clear();
    await _ref.read(accountLinkSessionPersistenceProvider).clear();

    _ref.invalidate(profileCoreProvider);
    _ref.invalidate(profileNotificationsProvider);
    _ref.invalidate(profileMarketingProvider);
    _ref.invalidate(chatControllerProvider);
    _ref.invalidate(paymentFlowControllerProvider);
    _ref.invalidate(accountLinksSummaryProvider);
    _ref.invalidate(accountLinkFlowControllerProvider);

    _ref.invalidate(accountLinksRepositoryProvider);
    _ref.invalidate(attachmentRepositoryProvider);
    _ref.invalidate(budgetRepositoryProvider);
    _ref.invalidate(catalogRepositoryProvider);
    _ref.invalidate(chatRepositoryProvider);
    _ref.invalidate(communityRepositoryProvider);
    _ref.invalidate(dashboardRepositoryProvider);
    _ref.invalidate(notificationRepositoryProvider);
    _ref.invalidate(orderRepositoryProvider);
    _ref.invalidate(payActivityRepositoryProvider);
    _ref.invalidate(paymentRepositoryProvider);
    _ref.invalidate(personalTransactionsRepositoryProvider);
    _ref.invalidate(profileRepositoryProvider);
    _ref.invalidate(spendingCategoryRepositoryProvider);
    _ref.invalidate(spendingRepositoryProvider);
    _ref.invalidate(statementImportRepositoryProvider);
  }

  /// Removes the cached setup-completed flag from SharedPreferences and
  /// resets the in-memory setup journey state so that a subsequent login
  /// or registration starts with a clean slate.
  Future<void> _clearSetupState() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove(SetupJourneyController.setupCompletedKey);
    _ref.read(setupJourneyControllerProvider.notifier).reset();
    _ref.invalidate(setupCompletedProvider);
  }

  Future<void> _setAnalyticsUser(AuthUser user) async {
    if (!_hasFirebaseApp) {
      return;
    }

    await FirebaseAnalytics.instance.setUserId(id: user.userId);
  }

  Future<void> _clearAnalyticsUser() async {
    if (!_hasFirebaseApp) {
      return;
    }

    await FirebaseAnalytics.instance.setUserId();
  }

  Future<void> _logAnalyticsEvent(
    String name,
    Map<String, Object> parameters,
  ) async {
    if (!_hasFirebaseApp) {
      return;
    }

    await FirebaseAnalytics.instance.logEvent(
      name: name,
      parameters: parameters,
    );
  }
}

final StateNotifierProvider<AuthController, AuthState> authControllerProvider =
    StateNotifierProvider<AuthController, AuthState>(
  AuthController.new,
);
