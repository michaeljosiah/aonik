import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/legacy.dart';
import 'package:plaid_flutter/plaid_flutter.dart';

import '../../../app/environment/app_environment.dart';
import '../../../app/environment/environment_provider.dart';
import '../../../data/api/api_exception.dart';
import '../../../data/repositories/account_links_repository.dart';
import '../../../data/repositories/repository_providers.dart';
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
  const PlaidAccountLinkLauncher();

  @override
  bool get isNativeProviderFlow => true;

  @override
  bool get supportsOAuthResume => false;

  @override
  String get experienceLabel => 'Plaid Link';

  @override
  Future<AccountLinkLaunchResult?> launch(
    AccountLinkLaunchRequest request,
  ) async {
    if (request.session.provider.toLowerCase() != 'plaid') {
      throw const AccountLinkLaunchException(
        'This mobile launcher only supports Plaid-backed account-link sessions right now.',
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
    } catch (error) {
      await awaiter.dispose();

      if (error is AccountLinkLaunchException) {
        rethrow;
      }

      throw const AccountLinkLaunchException(
        'We could not open the secure Plaid connection right now. Check your Plaid mobile configuration and try again.',
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

    _eventSubscription = PlaidLink.onEvent.listen((LinkEvent event) {});
    _loadSubscription = PlaidLink.onLoad.listen((LinkOnLoad event) {});
  }

  final Completer<AccountLinkLaunchResult?> _completer =
      Completer<AccountLinkLaunchResult?>();
  StreamSubscription<LinkSuccess>? _successSubscription;
  StreamSubscription<LinkExit>? _exitSubscription;
  StreamSubscription<LinkEvent>? _eventSubscription;
  StreamSubscription<LinkOnLoad>? _loadSubscription;

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
    await _successSubscription?.cancel();
    await _exitSubscription?.cancel();
    await _eventSubscription?.cancel();
    await _loadSubscription?.cancel();
    _successSubscription = null;
    _exitSubscription = null;
    _eventSubscription = null;
    _loadSubscription = null;
  }
}

final Provider<AccountLinkLauncher> accountLinkLauncherProvider =
    Provider<AccountLinkLauncher>((Ref ref) {
  final environment = ref.watch(appEnvironmentProvider);

  if (!environment.useMocks &&
      environment.accountLinkUseNativeLauncher &&
      environment.accountLinkProvider.toLowerCase() == 'plaid' &&
      !kIsWeb &&
      defaultTargetPlatform == TargetPlatform.android) {
    return const PlaidAccountLinkLauncher();
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

  AccountLinksRepository get _repository =>
      _ref.read(accountLinksRepositoryProvider);

  AccountLinkLauncher get _launcher => _ref.read(accountLinkLauncherProvider);

  AccountLinkSessionPersistence get _persistence =>
      _ref.read(accountLinkSessionPersistenceProvider);

  Future<AccountLinkExchangeResult?> connect({
    String provider = 'Plaid',
    String mode = 'connect',
    String? connectionId,
  }) async {
    if (state.isSubmitting) {
      return null;
    }

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
      final String? androidPackageName = launcher.isNativeProviderFlow &&
              !kIsWeb &&
              defaultTargetPlatform == TargetPlatform.android
          ? environment.accountLinkAndroidPackageName
          : null;
      final String? redirectUri = launcher.supportsOAuthResume
          ? environment.accountLinkRedirectUri
          : null;

      final AccountLinkSession session = await _repository.createSession(
        provider: provider,
        mode: mode,
        connectionId: connectionId,
        androidPackageName: androidPackageName,
        redirectUri: redirectUri,
      );

      if (launcher.supportsOAuthResume) {
        await _persistence.write(session);
      }
      final AccountLinkLaunchResult? launchResult =
          await launcher.launch(AccountLinkLaunchRequest(session: session));

      if (launchResult == null) {
        if (launcher.supportsOAuthResume) {
          await _persistence.clear();
        }
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

      if (launchResult == null) {
        await _persistence.clear();
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

  void reset() {
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
