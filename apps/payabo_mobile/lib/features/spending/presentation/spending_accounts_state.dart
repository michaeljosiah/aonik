import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/legacy.dart';
import 'package:plaid_flutter/plaid_flutter.dart';

import '../../../app/demo/demo_mode.dart';
import '../../../app/environment/app_environment.dart';
import '../../../app/environment/environment_provider.dart';
import '../../../data/api/api_exception.dart';
import '../../../data/repositories/account_links_repository.dart';
import '../../../data/repositories/repository_providers.dart';
import '../../profile/presentation/profile_state.dart';
import 'spending_account_link_persistence.dart';

final FutureProvider<AccountLinksSummary> accountLinksSummaryProvider =
    FutureProvider<AccountLinksSummary>((Ref ref) async {
  final AccountLinksRepository repository =
      ref.watch(accountLinksRepositoryProvider);
  return repository.getSummary();
});

class AccountLinkLaunchRequest {
  const AccountLinkLaunchRequest({required this.session});

  final AccountLinkSession session;
}

class AccountLinkResumeRequest {
  const AccountLinkResumeRequest({
    required this.session,
    required this.redirectUri,
  });

  final AccountLinkSession session;
  final String redirectUri;
}

class AccountLinkLaunchResult {
  const AccountLinkLaunchResult({required this.temporaryCode});

  final String temporaryCode;
}

abstract class AccountLinkLauncher {
  bool get isNativeProviderFlow;

  bool get supportsOAuthResume;

  bool get supportsEmbeddedLink;

  String get experienceLabel;

  Future<AccountLinkLaunchResult?> launch(AccountLinkLaunchRequest request);

  Future<AccountLinkLaunchResult?> resume(AccountLinkResumeRequest request);
}

class AccountLinkLaunchException implements Exception {
  const AccountLinkLaunchException(this.message);

  final String message;

  @override
  String toString() => message;
}

class SimulatedAccountLinkLauncher implements AccountLinkLauncher {
  const SimulatedAccountLinkLauncher();

  @override
  bool get isNativeProviderFlow => false;

  @override
  bool get supportsOAuthResume => false;

  @override
  bool get supportsEmbeddedLink => false;

  @override
  String get experienceLabel => 'Simulated provider handoff';

  @override
  Future<AccountLinkLaunchResult?> launch(
    AccountLinkLaunchRequest request,
  ) async {
    await Future<void>.delayed(const Duration(milliseconds: 650));

    final int now = DateTime.now().microsecondsSinceEpoch;
    return AccountLinkLaunchResult(
      temporaryCode: 'mobile-${request.session.provider.toLowerCase()}-$now',
    );
  }

  @override
  Future<AccountLinkLaunchResult?> resume(
    AccountLinkResumeRequest request,
  ) async {
    throw const AccountLinkLaunchException(
      'OAuth resume is only available when the native provider launcher is enabled.',
    );
  }
}

class PlaidAccountLinkLauncher implements AccountLinkLauncher {
  const PlaidAccountLinkLauncher({required AppEnvironment environment})
      : _environment = environment;

  final AppEnvironment _environment;

  @override
  bool get isNativeProviderFlow => true;

  @override
  bool get supportsOAuthResume => false;

  @override
  bool get supportsEmbeddedLink => false;

  @override
  String get experienceLabel => 'Plaid Link';

  /// Real Plaid link tokens always begin with `link-`.  The simulated backend
  /// gateway returns tokens like `plaid_link_<guid>_<key>` which the native
  /// Plaid SDK rejects with [MalformedLinkTokenException].
  static bool _isRealPlaidLinkToken(String token) =>
      token.startsWith('link-');

  @override
  Future<AccountLinkLaunchResult?> launch(
    AccountLinkLaunchRequest request,
  ) async {
    if (request.session.provider.toLowerCase() != 'plaid') {
      throw const AccountLinkLaunchException(
        'This mobile launcher only supports Plaid-backed account-link sessions right now.',
      );
    }

    if (!_isRealPlaidLinkToken(request.session.launchToken)) {
      debugPrint(
        '[PlaidAccountLinkLauncher] Backend returned a simulated link token '
        '("${request.session.launchToken.length > 20 ? '${request.session.launchToken.substring(0, 20)}...' : request.session.launchToken}"). '
        'Falling back to simulated handoff. Set Finance:PersonalFinance:Plaid:UseRealPlaidApi=true '
        'on the backend to use the real Plaid SDK.',
      );

      // Simulate a short delay and return the token as the temporary code so
      // the backend simulated gateway can still exchange it.
      await Future<void>.delayed(const Duration(milliseconds: 650));
      final int now = DateTime.now().microsecondsSinceEpoch;
      return AccountLinkLaunchResult(
        temporaryCode:
            'mobile-${request.session.provider.toLowerCase()}-$now',
      );
    }

    final _PlaidLinkAwaiter awaiter = _PlaidLinkAwaiter();

    try {
      final LinkTokenConfiguration configuration = LinkTokenConfiguration(
        token: request.session.launchToken,
      );

      await PlaidLink.create(configuration: configuration);
      await PlaidLink.open();

      return await awaiter.waitForResult();
    } catch (error, stackTrace) {
      await awaiter.dispose();

      if (error is AccountLinkLaunchException) {
        rethrow;
      }

      debugPrint(
        '[PlaidAccountLinkLauncher] PlaidLink failed: $error\n$stackTrace',
      );

      throw AccountLinkLaunchException(
        'We could not open the secure Plaid connection right now. '
        'Package: ${_environment.resolvedAccountLinkAndroidPackageName}. '
        'Detail: $error',
      );
    }
  }

  @override
  Future<AccountLinkLaunchResult?> resume(
    AccountLinkResumeRequest request,
  ) async {
    throw const AccountLinkLaunchException(
      'OAuth resume is not enabled for the current Android-only Plaid mobile flow.',
    );
  }
}

class _PlaidLinkAwaiter {
  _PlaidLinkAwaiter() {
    _successSubscription =
        PlaidLink.onSuccess.listen((LinkSuccess event) async {
      if (!_completer.isCompleted) {
        _completer.complete(
          AccountLinkLaunchResult(temporaryCode: event.publicToken),
        );
      }
      await dispose();
    });

    _exitSubscription = PlaidLink.onExit.listen((LinkExit event) async {
      if (!_completer.isCompleted) {
        _completer.complete(null);
      }
      await dispose();
    });
  }

  final Completer<AccountLinkLaunchResult?> _completer =
      Completer<AccountLinkLaunchResult?>();
  bool _disposed = false;
  StreamSubscription<LinkSuccess>? _successSubscription;
  StreamSubscription<LinkExit>? _exitSubscription;

  Future<AccountLinkLaunchResult?> waitForResult() async {
    return _completer.future.timeout(
      const Duration(minutes: 5),
      onTimeout: () async {
        await dispose();
        throw const AccountLinkLaunchException(
          'The secure bank-link session timed out before the provider returned a result.',
        );
      },
    );
  }

  Future<void> dispose() async {
    if (_disposed) return;
    _disposed = true;
    await _successSubscription?.cancel();
    await _exitSubscription?.cancel();
    _successSubscription = null;
    _exitSubscription = null;
  }
}

final Provider<AccountLinkLauncher> accountLinkLauncherProvider =
    Provider<AccountLinkLauncher>((Ref ref) {
  final environment = ref.watch(appEnvironmentProvider);
  final isDemo = ref.watch(isDemoProvider);

  if (!isDemo &&
      environment.usesPlaidAccountLinkProvider &&
      !kIsWeb &&
      defaultTargetPlatform == TargetPlatform.android) {
    return PlaidAccountLinkLauncher(environment: environment);
  }

  return const SimulatedAccountLinkLauncher();
});

class AccountLinkFlowState {
  const AccountLinkFlowState({
    required this.isSubmitting,
    this.activeAction,
    this.activeConnectionId,
    this.errorMessage,
    this.lastResult,
  });

  final bool isSubmitting;
  final String? activeAction;
  final String? activeConnectionId;
  final String? errorMessage;
  final AccountLinkConnectionResult? lastResult;

  factory AccountLinkFlowState.initial() {
    return const AccountLinkFlowState(isSubmitting: false);
  }

  AccountLinkFlowState copyWith({
    bool? isSubmitting,
    Object? activeAction = _flowCopySentinel,
    Object? activeConnectionId = _flowCopySentinel,
    Object? errorMessage = _flowCopySentinel,
    Object? lastResult = _flowCopySentinel,
  }) {
    return AccountLinkFlowState(
      isSubmitting: isSubmitting ?? this.isSubmitting,
      activeAction: activeAction == _flowCopySentinel
          ? this.activeAction
          : activeAction as String?,
      activeConnectionId: activeConnectionId == _flowCopySentinel
          ? this.activeConnectionId
          : activeConnectionId as String?,
      errorMessage: errorMessage == _flowCopySentinel
          ? this.errorMessage
          : errorMessage as String?,
      lastResult: lastResult == _flowCopySentinel
          ? this.lastResult
          : lastResult as AccountLinkConnectionResult?,
    );
  }
}

class AccountLinkFlowController extends StateNotifier<AccountLinkFlowState> {
  AccountLinkFlowController(this._ref) : super(AccountLinkFlowState.initial());

  final Ref _ref;

  /// Monotonically increasing token used to detect stale async flows.
  /// Every call to [connect], [resumeOAuthRedirect], [reset], or [cancel]
  /// bumps this value so that an in-flight future from a previous invocation
  /// can detect it became stale and silently bail out instead of mutating
  /// [state] on a widget tree that may already be disposed.
  int _flowToken = 0;

  AccountLinksRepository get _repository =>
      _ref.read(accountLinksRepositoryProvider);

  AccountLinkLauncher get _launcher => _ref.read(accountLinkLauncherProvider);

  AccountLinkSessionPersistence get _persistence =>
      _ref.read(accountLinkSessionPersistenceProvider);

  Future<AccountLinkExchangeResult?> connect({
    String? provider,
    String mode = 'connect',
    String? connectionId,
    String? countryCode,
  }) async {
    if (state.isSubmitting) {
      return null;
    }

    // Capture a snapshot of the token so we can detect cancellation after
    // each await point.  If [_flowToken] has changed by the time we resume,
    // it means [reset], [cancel], or a newer [connect] was called and we
    // must abandon this flow silently.
    final int token = ++_flowToken;

    state = state.copyWith(
      isSubmitting: true,
      activeAction: mode == 'update' ? 'reconnect' : 'connect',
      activeConnectionId: connectionId,
      errorMessage: null,
      lastResult: null,
    );

    try {
      final AppEnvironment environment = _ref.read(appEnvironmentProvider);
      final AccountLinkLauncher launcher = _launcher;
      final String resolvedProvider =
          environment.resolveAccountLinkProvider(provider);
      final String? androidPackageName = launcher.isNativeProviderFlow &&
              !kIsWeb &&
              defaultTargetPlatform == TargetPlatform.android
          ? environment.resolvedAccountLinkAndroidPackageName
          : null;
      final String? redirectUri = launcher.supportsOAuthResume
          ? environment.configuredAccountLinkRedirectUri
          : null;
      final String? resolvedCountryCode = countryCode?.trim().isNotEmpty == true
          ? countryCode!.trim().toUpperCase()
          : _ref.read(profileCoreProvider).countryCode.isNotEmpty
              ? _ref.read(profileCoreProvider).countryCode
              : null;

      final AccountLinkSession session = await _repository.createSession(
        provider: resolvedProvider,
        mode: mode,
        connectionId: connectionId,
        androidPackageName: androidPackageName,
        redirectUri: redirectUri,
        countryCode: resolvedCountryCode,
      );

      if (_flowToken != token || !mounted) return null;

      if (launcher.supportsOAuthResume) {
        await _persistence.write(session);
      }
      final AccountLinkLaunchResult? launchResult =
          await launcher.launch(AccountLinkLaunchRequest(session: session));

      if (_flowToken != token || !mounted) return null;

      if (launchResult == null) {
        if (launcher.supportsOAuthResume) {
          await _persistence.clear();
        }
        if (_flowToken != token || !mounted) return null;
        state = state.copyWith(
          isSubmitting: false,
          activeAction: null,
          activeConnectionId: null,
        );
        return null;
      }

      final AccountLinkExchangeResult result =
          await _repository.exchangeSession(
        sessionId: session.sessionId,
        temporaryCode: launchResult.temporaryCode,
      );

      if (_flowToken != token || !mounted) return null;

      if (launcher.supportsOAuthResume) {
        await _persistence.clear();
      }
      _ref.invalidate(accountLinksSummaryProvider);

      state = state.copyWith(
        isSubmitting: false,
        activeAction: null,
        activeConnectionId: null,
        lastResult: result,
      );

      return result;
    } catch (error) {
      if (_launcher.supportsOAuthResume) {
        await _persistence.clear();
      }
      if (_flowToken != token || !mounted) return null;
      _setErrorState(error);
      rethrow;
    }
  }

  Future<AccountLinkExchangeResult?> resumeOAuthRedirect(
    String redirectUri,
  ) async {
    if (state.isSubmitting) {
      return null;
    }

    if (!_launcher.supportsOAuthResume) {
      throw const AccountLinkLaunchException(
        'OAuth resume is not enabled for the current Android-only Plaid flow.',
      );
    }

    final PersistedAccountLinkSessionSnapshot? pendingSession =
        await _persistence.read();
    if (pendingSession == null) {
      throw const AccountLinkLaunchException(
        'No pending account-link session was found to resume.',
      );
    }

    if (pendingSession.expiresAt.isBefore(DateTime.now())) {
      await _persistence.clear();
      throw const AccountLinkLaunchException(
        'The pending account-link session expired before it could be resumed.',
      );
    }

    final int token = ++_flowToken;

    state = state.copyWith(
      isSubmitting: true,
      activeAction: 'resume',
      activeConnectionId: pendingSession.connectionId,
      errorMessage: null,
      lastResult: null,
    );

    try {
      final AccountLinkLaunchResult? launchResult = await _launcher.resume(
        AccountLinkResumeRequest(
          session: pendingSession.toSession(),
          redirectUri: redirectUri,
        ),
      );

      if (_flowToken != token || !mounted) return null;

      if (launchResult == null) {
        await _persistence.clear();
        if (_flowToken != token || !mounted) return null;
        state = state.copyWith(
          isSubmitting: false,
          activeAction: null,
          activeConnectionId: null,
        );
        return null;
      }

      final AccountLinkExchangeResult result =
          await _repository.exchangeSession(
        sessionId: pendingSession.sessionId,
        temporaryCode: launchResult.temporaryCode,
      );

      if (_flowToken != token || !mounted) return null;

      await _persistence.clear();
      _ref.invalidate(accountLinksSummaryProvider);

      state = state.copyWith(
        isSubmitting: false,
        activeAction: null,
        activeConnectionId: null,
        lastResult: result,
      );

      return result;
    } catch (error) {
      if (_flowToken != token || !mounted) return null;
      _setErrorState(error);
      rethrow;
    }
  }

  Future<AccountLinkActionResult?> refreshConnection(
      String connectionId) async {
    if (state.isSubmitting) {
      return null;
    }

    state = state.copyWith(
      isSubmitting: true,
      activeAction: 'refresh',
      activeConnectionId: connectionId,
      errorMessage: null,
      lastResult: null,
    );

    try {
      final AccountLinkActionResult result =
          await _repository.refreshConnection(connectionId: connectionId);

      _ref.invalidate(accountLinksSummaryProvider);
      state = state.copyWith(
        isSubmitting: false,
        activeAction: null,
        activeConnectionId: null,
        lastResult: result,
      );

      return result;
    } catch (error) {
      _setErrorState(error);
      rethrow;
    }
  }

  Future<AccountLinkActionResult?> disconnectConnection(
    String connectionId,
  ) async {
    if (state.isSubmitting) {
      return null;
    }

    state = state.copyWith(
      isSubmitting: true,
      activeAction: 'disconnect',
      activeConnectionId: connectionId,
      errorMessage: null,
      lastResult: null,
    );

    try {
      final AccountLinkActionResult result =
          await _repository.disconnectConnection(connectionId: connectionId);

      _ref.invalidate(accountLinksSummaryProvider);
      state = state.copyWith(
        isSubmitting: false,
        activeAction: null,
        activeConnectionId: null,
        lastResult: result,
      );

      return result;
    } catch (error) {
      _setErrorState(error);
      rethrow;
    }
  }

  Future<bool> hasPendingSession() async {
    if (!_launcher.supportsOAuthResume) {
      return false;
    }

    final PersistedAccountLinkSessionSnapshot? pending =
        await _persistence.read();
    return pending != null;
  }

  /// Cancel any in-flight connect/resume flow and return to idle.
  ///
  /// Safe to call from widget `dispose()`.  The bumped [_flowToken] ensures
  /// any pending future from [connect] or [resumeOAuthRedirect] will detect
  /// that it is stale and skip further state mutations.
  ///
  /// State is reset via [Future.microtask] so that calling this during the
  /// widget tree teardown phase (e.g. from `State.dispose()`) does not
  /// trigger Riverpod's "modified a provider while building" assertion.
  void cancel() {
    _flowToken++;
    if (mounted) {
      Future.microtask(() {
        if (mounted) {
          state = AccountLinkFlowState.initial();
        }
      });
    }
  }

  void reset() {
    _flowToken++;
    state = AccountLinkFlowState.initial();
  }

  void _setErrorState(Object error) {
    final String message;
    if (error is ApiException) {
      message = error.message;
    } else if (error is AccountLinkLaunchException) {
      message = error.message;
    } else {
      message =
          'We could not complete the secure account connection right now.';
    }

    state = state.copyWith(
      isSubmitting: false,
      activeAction: null,
      activeConnectionId: null,
      errorMessage: message,
    );
  }
}

final StateNotifierProvider<AccountLinkFlowController, AccountLinkFlowState>
    accountLinkFlowControllerProvider =
    StateNotifierProvider<AccountLinkFlowController, AccountLinkFlowState>(
  AccountLinkFlowController.new,
);

const Object _flowCopySentinel = Object();
