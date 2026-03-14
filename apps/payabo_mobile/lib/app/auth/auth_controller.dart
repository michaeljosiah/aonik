import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/legacy.dart';

import '../../data/api/api_exception.dart';
import '../../data/repositories/auth_repository.dart';
import '../../data/repositories/repository_providers.dart';
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
        state = const AuthState(
          isInitialized: true,
          isAuthenticated: false,
          isBusy: false,
        );
        return;
      }

      state = const AuthState(
        isInitialized: true,
        isAuthenticated: true,
        isBusy: false,
      );
    }
  }

  Future<void> signInWithPassword({
    required String email,
    required String password,
  }) async {
    state = state.copyWith(isBusy: true);

    try {
      final repository = _ref.read(authRepositoryProvider);
      final token = await repository.signInWithPassword(
        email: email,
        password: password,
      );

      await _persistSession(token);

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
    } catch (error) {
      state = state.copyWith(isBusy: false);
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
}

final StateNotifierProvider<AuthController, AuthState> authControllerProvider =
    StateNotifierProvider<AuthController, AuthState>(
  AuthController.new,
);
