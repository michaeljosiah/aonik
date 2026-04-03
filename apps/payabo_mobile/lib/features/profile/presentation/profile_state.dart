import 'dart:async';
import 'dart:developer' as developer;

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/legacy.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../../../app/errors/api_error_notifier.dart';
import '../../../data/repositories/profile_repository.dart';
import '../../../data/repositories/repository_providers.dart';

const String _touchIdKey = 'profile.touchIdEnabled';

class ProfileCoreState {
  const ProfileCoreState({
    required this.firstName,
    required this.lastName,
    required this.email,
    required this.phone,
    required this.countryCode,
    required this.photoUrl,
    required this.photoLabel,
    required this.loaded,
  });

  final String firstName;
  final String lastName;
  final String email;
  final String phone;
  final String countryCode;
  final String? photoUrl;
  final String photoLabel;
  final bool loaded;

  String get displayName => '$firstName $lastName'.trim();

  ProfileCoreState copyWith({
    String? firstName,
    String? lastName,
    String? email,
    String? phone,
    String? countryCode,
    String? photoUrl,
    bool clearPhotoUrl = false,
    String? photoLabel,
    bool? loaded,
  }) {
    return ProfileCoreState(
      firstName: firstName ?? this.firstName,
      lastName: lastName ?? this.lastName,
      email: email ?? this.email,
      phone: phone ?? this.phone,
      countryCode: countryCode ?? this.countryCode,
      photoUrl: clearPhotoUrl ? null : (photoUrl ?? this.photoUrl),
      photoLabel: photoLabel ?? this.photoLabel,
      loaded: loaded ?? this.loaded,
    );
  }

  factory ProfileCoreState.initial() {
    return const ProfileCoreState(
      firstName: '',
      lastName: '',
      email: '',
      phone: '',
      countryCode: '',
      photoUrl: null,
      photoLabel: 'Add photo',
      loaded: false,
    );
  }
}

class ProfileNotificationsState {
  const ProfileNotificationsState({
    required this.notificationsEmail,
    required this.newBillsPush,
    required this.billUpdatesPush,
    required this.billAssistPush,
    required this.mbaMessagesPush,
    required this.orgMessagesPush,
    required this.friendsMessagesPush,
    required this.newBillsEmail,
    required this.billUpdatesEmail,
    required this.billAssistEmail,
    required this.mbaMessagesEmail,
    required this.orgMessagesEmail,
    required this.loaded,
  });

  final String notificationsEmail;
  final bool newBillsPush;
  final bool billUpdatesPush;
  final bool billAssistPush;
  final bool mbaMessagesPush;
  final bool orgMessagesPush;
  final bool friendsMessagesPush;
  final bool newBillsEmail;
  final bool billUpdatesEmail;
  final bool billAssistEmail;
  final bool mbaMessagesEmail;
  final bool orgMessagesEmail;
  final bool loaded;

  ProfileNotificationsState copyWith({
    String? notificationsEmail,
    bool? newBillsPush,
    bool? billUpdatesPush,
    bool? billAssistPush,
    bool? mbaMessagesPush,
    bool? orgMessagesPush,
    bool? friendsMessagesPush,
    bool? newBillsEmail,
    bool? billUpdatesEmail,
    bool? billAssistEmail,
    bool? mbaMessagesEmail,
    bool? orgMessagesEmail,
    bool? loaded,
  }) {
    return ProfileNotificationsState(
      notificationsEmail: notificationsEmail ?? this.notificationsEmail,
      newBillsPush: newBillsPush ?? this.newBillsPush,
      billUpdatesPush: billUpdatesPush ?? this.billUpdatesPush,
      billAssistPush: billAssistPush ?? this.billAssistPush,
      mbaMessagesPush: mbaMessagesPush ?? this.mbaMessagesPush,
      orgMessagesPush: orgMessagesPush ?? this.orgMessagesPush,
      friendsMessagesPush: friendsMessagesPush ?? this.friendsMessagesPush,
      newBillsEmail: newBillsEmail ?? this.newBillsEmail,
      billUpdatesEmail: billUpdatesEmail ?? this.billUpdatesEmail,
      billAssistEmail: billAssistEmail ?? this.billAssistEmail,
      mbaMessagesEmail: mbaMessagesEmail ?? this.mbaMessagesEmail,
      orgMessagesEmail: orgMessagesEmail ?? this.orgMessagesEmail,
      loaded: loaded ?? this.loaded,
    );
  }

  factory ProfileNotificationsState.initial() {
    return const ProfileNotificationsState(
      notificationsEmail: '',
      newBillsPush: true,
      billUpdatesPush: true,
      billAssistPush: false,
      mbaMessagesPush: true,
      orgMessagesPush: true,
      friendsMessagesPush: false,
      newBillsEmail: true,
      billUpdatesEmail: true,
      billAssistEmail: false,
      mbaMessagesEmail: true,
      orgMessagesEmail: true,
      loaded: false,
    );
  }
}

class ProfileMarketingState {
  const ProfileMarketingState({
    required this.marketingEmail,
    required this.marketingNews,
    required this.marketingOffers,
    required this.marketingSurveys,
    required this.loaded,
  });

  final String marketingEmail;
  final bool marketingNews;
  final bool marketingOffers;
  final bool marketingSurveys;
  final bool loaded;

  ProfileMarketingState copyWith({
    String? marketingEmail,
    bool? marketingNews,
    bool? marketingOffers,
    bool? marketingSurveys,
    bool? loaded,
  }) {
    return ProfileMarketingState(
      marketingEmail: marketingEmail ?? this.marketingEmail,
      marketingNews: marketingNews ?? this.marketingNews,
      marketingOffers: marketingOffers ?? this.marketingOffers,
      marketingSurveys: marketingSurveys ?? this.marketingSurveys,
      loaded: loaded ?? this.loaded,
    );
  }

  factory ProfileMarketingState.initial() {
    return const ProfileMarketingState(
      marketingEmail: '',
      marketingNews: true,
      marketingOffers: true,
      marketingSurveys: false,
      loaded: false,
    );
  }
}

class ProfileHeaderState {
  const ProfileHeaderState({
    required this.displayName,
    required this.photoUrl,
  });

  final String displayName;
  final String? photoUrl;
}

class ProfilePersonalDetailsState {
  const ProfilePersonalDetailsState({
    required this.displayName,
    required this.phone,
  });

  final String displayName;
  final String phone;
}

class ProfileLoginDetailsState {
  const ProfileLoginDetailsState({
    required this.email,
    required this.touchIdEnabled,
  });

  final String email;
  final bool touchIdEnabled;
}

class ProfileCoreController extends StateNotifier<ProfileCoreState> {
  ProfileCoreController(this._ref) : super(ProfileCoreState.initial());

  final Ref _ref;

  ProfileRepository get _repository => _ref.read(profileRepositoryProvider);

  Future<void> ensureLoaded({bool force = false}) async {
    if (state.loaded && !force) {
      return;
    }

    final UserProfile profile = await _loadProfile();
    _setProfile(profile);
  }

  Future<void> reload() {
    return ensureLoaded(force: true);
  }

  Future<UserProfile> _loadProfile() async {
    try {
      return await _repository.getProfile();
    } catch (error, stackTrace) {
      developer.log(
        'Failed to load customer profile.',
        name: 'Payabo.ProfileCoreController',
        error: error,
        stackTrace: stackTrace,
      );
      rethrow;
    }
  }

  Future<void> updateName({
    required String firstName,
    required String lastName,
  }) async {
    final ProfileCoreState previousState = state;
    state = state.copyWith(
      firstName: firstName.trim(),
      lastName: lastName.trim(),
    );

    try {
      await _persistCoreProfile();
    } catch (_) {
      state = previousState;
      rethrow;
    }
  }

  Future<void> updatePhone({
    required String phone,
    String? countryCode,
  }) async {
    final ProfileCoreState previousState = state;
    state = state.copyWith(
      phone: phone.trim(),
      countryCode: countryCode ?? state.countryCode,
    );

    try {
      await _persistCoreProfile();
    } catch (_) {
      state = previousState;
      rethrow;
    }
  }

  Future<void> updateLoginEmail({
    required String currentEmail,
    required String newEmail,
    required String password,
  }) async {
    final UserProfile profile = await _repository.updateEmail(
      currentEmail: currentEmail,
      newEmail: newEmail,
      password: password,
    );

    _setProfile(profile);
  }

  Future<void> updatePassword({
    required String currentPassword,
    required String newPassword,
  }) async {
    await _repository.updatePassword(
      currentPassword: currentPassword,
      newPassword: newPassword,
    );
  }

  Future<void> uploadPhoto(String filePath) async {
    final String uploadedUrl = await _repository.uploadPhoto(filePath);
    var resolvedUrl = uploadedUrl.trim();

    developer.log(
      'uploadPhoto returned URL: ${resolvedUrl.isEmpty ? '<empty>' : resolvedUrl}',
      name: 'Payabo.ProfileCoreController',
    );

    if (resolvedUrl.isEmpty) {
      final UserProfile profile = await _repository.getProfile();
      resolvedUrl = profile.photoUrl?.trim() ?? '';

      developer.log(
        'Reloaded profile after upload. photoUrl: ${resolvedUrl.isEmpty ? '<empty>' : resolvedUrl}',
        name: 'Payabo.ProfileCoreController',
      );
    }

    if (resolvedUrl.isEmpty) {
      resolvedUrl = filePath.trim();
    }

    state = state.copyWith(
      photoUrl: resolvedUrl.isEmpty ? null : resolvedUrl,
      photoLabel: 'Change photo',
      loaded: true,
    );
  }

  Future<void> deletePhoto() async {
    await _repository.deletePhoto();
    state = state.copyWith(clearPhotoUrl: true, photoLabel: 'Add photo');
  }

  void setPhotoLabel(String label) {
    state = state.copyWith(photoLabel: label);
  }

  Future<void> _persistCoreProfile() async {
    final UserProfile profile = await _repository.updateProfile(
      UserProfile(
        firstName: state.firstName,
        lastName: state.lastName,
        email: state.email,
        phone: state.phone,
        countryCode: state.countryCode,
        photoUrl: state.photoUrl,
      ),
    );

    _setProfile(profile);
  }

  void _setProfile(UserProfile profile) {
    state = state.copyWith(
      firstName: profile.firstName,
      lastName: profile.lastName,
      email: profile.email,
      phone: profile.phone,
      countryCode: profile.countryCode,
      photoUrl: profile.photoUrl,
      photoLabel: _photoLabelFor(profile.photoUrl),
      loaded: true,
    );
  }

  String _photoLabelFor(String? photoUrl) {
    return photoUrl != null && photoUrl.trim().isNotEmpty
        ? 'Change photo'
        : 'Add photo';
  }
}

class ProfileNotificationsController
    extends StateNotifier<ProfileNotificationsState> {
  ProfileNotificationsController(this._ref)
      : super(ProfileNotificationsState.initial());

  final Ref _ref;

  ProfileRepository get _repository => _ref.read(profileRepositoryProvider);

  Future<void> ensureLoaded({
    required String fallbackEmail,
    bool force = false,
  }) async {
    if (state.loaded && !force) {
      return;
    }

    final NotificationPreferences preferences =
        await _loadPreferencesOrDefault(fallbackEmail);
    _setPreferences(preferences);
  }

  Future<void> reload({required String fallbackEmail}) {
    return ensureLoaded(fallbackEmail: fallbackEmail, force: true);
  }

  Future<NotificationPreferences> _loadPreferencesOrDefault(
    String fallbackEmail,
  ) async {
    try {
      return await _repository.getNotificationPreferences();
    } catch (error, stackTrace) {
      developer.log(
        'Failed to load notification preferences. Falling back to defaults.',
        name: 'Payabo.ProfileNotificationsController',
        error: error,
        stackTrace: stackTrace,
      );

      if (mounted) {
        _ref.read(apiErrorNotifierProvider.notifier).report(error);
      }

      return NotificationPreferences(
        email: fallbackEmail,
        newBillsPush: state.newBillsPush,
        billUpdatesPush: state.billUpdatesPush,
        billAssistPush: state.billAssistPush,
        mbaMessagesPush: state.mbaMessagesPush,
        orgMessagesPush: state.orgMessagesPush,
        friendsMessagesPush: state.friendsMessagesPush,
        newBillsEmail: state.newBillsEmail,
        billUpdatesEmail: state.billUpdatesEmail,
        billAssistEmail: state.billAssistEmail,
        mbaMessagesEmail: state.mbaMessagesEmail,
        orgMessagesEmail: state.orgMessagesEmail,
      );
    }
  }

  Future<void> setNotificationsEmail(String email) async {
    final ProfileNotificationsState previousState = state;
    state = state.copyWith(notificationsEmail: email.trim());

    try {
      await _persistPreferences();
    } catch (_) {
      state = previousState;
      rethrow;
    }
  }

  Future<void> setPushToggle({
    bool? newBills,
    bool? billUpdates,
    bool? billAssist,
    bool? mbaMessages,
    bool? orgMessages,
    bool? friendsMessages,
  }) async {
    final ProfileNotificationsState previousState = state;
    state = state.copyWith(
      newBillsPush: newBills,
      billUpdatesPush: billUpdates,
      billAssistPush: billAssist,
      mbaMessagesPush: mbaMessages,
      orgMessagesPush: orgMessages,
      friendsMessagesPush: friendsMessages,
    );

    try {
      await _persistPreferences();
    } catch (_) {
      state = previousState;
      rethrow;
    }
  }

  Future<void> setEmailNotificationToggle({
    bool? newBills,
    bool? billUpdates,
    bool? billAssist,
    bool? mbaMessages,
    bool? orgMessages,
  }) async {
    final ProfileNotificationsState previousState = state;
    state = state.copyWith(
      newBillsEmail: newBills,
      billUpdatesEmail: billUpdates,
      billAssistEmail: billAssist,
      mbaMessagesEmail: mbaMessages,
      orgMessagesEmail: orgMessages,
    );

    try {
      await _persistPreferences();
    } catch (_) {
      state = previousState;
      rethrow;
    }
  }

  Future<void> _persistPreferences() async {
    final NotificationPreferences preferences =
        await _repository.updateNotificationPreferences(
      NotificationPreferences(
        email: state.notificationsEmail,
        newBillsPush: state.newBillsPush,
        billUpdatesPush: state.billUpdatesPush,
        billAssistPush: state.billAssistPush,
        mbaMessagesPush: state.mbaMessagesPush,
        orgMessagesPush: state.orgMessagesPush,
        friendsMessagesPush: state.friendsMessagesPush,
        newBillsEmail: state.newBillsEmail,
        billUpdatesEmail: state.billUpdatesEmail,
        billAssistEmail: state.billAssistEmail,
        mbaMessagesEmail: state.mbaMessagesEmail,
        orgMessagesEmail: state.orgMessagesEmail,
      ),
    );

    _setPreferences(preferences);
  }

  void _setPreferences(NotificationPreferences preferences) {
    state = state.copyWith(
      notificationsEmail: preferences.email,
      newBillsPush: preferences.newBillsPush,
      billUpdatesPush: preferences.billUpdatesPush,
      billAssistPush: preferences.billAssistPush,
      mbaMessagesPush: preferences.mbaMessagesPush,
      orgMessagesPush: preferences.orgMessagesPush,
      friendsMessagesPush: preferences.friendsMessagesPush,
      newBillsEmail: preferences.newBillsEmail,
      billUpdatesEmail: preferences.billUpdatesEmail,
      billAssistEmail: preferences.billAssistEmail,
      mbaMessagesEmail: preferences.mbaMessagesEmail,
      orgMessagesEmail: preferences.orgMessagesEmail,
      loaded: true,
    );
  }
}

class ProfileMarketingController extends StateNotifier<ProfileMarketingState> {
  ProfileMarketingController(this._ref)
      : super(ProfileMarketingState.initial());

  final Ref _ref;

  ProfileRepository get _repository => _ref.read(profileRepositoryProvider);

  Future<void> ensureLoaded({
    required String fallbackEmail,
    bool force = false,
  }) async {
    if (state.loaded && !force) {
      return;
    }

    final MarketingPreferences preferences =
        await _loadPreferencesOrDefault(fallbackEmail);
    _setPreferences(preferences);
  }

  Future<void> reload({required String fallbackEmail}) {
    return ensureLoaded(fallbackEmail: fallbackEmail, force: true);
  }

  Future<MarketingPreferences> _loadPreferencesOrDefault(
    String fallbackEmail,
  ) async {
    try {
      return await _repository.getMarketingPreferences();
    } catch (error, stackTrace) {
      developer.log(
        'Failed to load marketing preferences. Falling back to defaults.',
        name: 'Payabo.ProfileMarketingController',
        error: error,
        stackTrace: stackTrace,
      );

      if (mounted) {
        _ref.read(apiErrorNotifierProvider.notifier).report(error);
      }

      return MarketingPreferences(
        email: fallbackEmail,
        news: state.marketingNews,
        offers: state.marketingOffers,
        surveys: state.marketingSurveys,
      );
    }
  }

  Future<void> setMarketingEmail(String email) async {
    final ProfileMarketingState previousState = state;
    state = state.copyWith(marketingEmail: email.trim());

    try {
      await _persistPreferences();
    } catch (_) {
      state = previousState;
      rethrow;
    }
  }

  Future<void> setMarketingToggle({
    bool? news,
    bool? offers,
    bool? surveys,
  }) async {
    final ProfileMarketingState previousState = state;
    state = state.copyWith(
      marketingNews: news,
      marketingOffers: offers,
      marketingSurveys: surveys,
    );

    try {
      await _persistPreferences();
    } catch (_) {
      state = previousState;
      rethrow;
    }
  }

  Future<void> _persistPreferences() async {
    final MarketingPreferences preferences =
        await _repository.updateMarketingPreferences(
      MarketingPreferences(
        email: state.marketingEmail,
        news: state.marketingNews,
        offers: state.marketingOffers,
        surveys: state.marketingSurveys,
      ),
    );

    _setPreferences(preferences);
  }

  void _setPreferences(MarketingPreferences preferences) {
    state = state.copyWith(
      marketingEmail: preferences.email,
      marketingNews: preferences.news,
      marketingOffers: preferences.offers,
      marketingSurveys: preferences.surveys,
      loaded: true,
    );
  }
}

class BiometricPreferenceController extends StateNotifier<bool> {
  BiometricPreferenceController() : super(false) {
    unawaited(ensureLoaded());
  }

  bool _loaded = false;

  Future<void> ensureLoaded({bool force = false}) async {
    if (_loaded && !force) {
      return;
    }

    final SharedPreferences prefs = await SharedPreferences.getInstance();
    state = prefs.getBool(_touchIdKey) ?? false;
    _loaded = true;
  }

  Future<void> reload() {
    return ensureLoaded(force: true);
  }

  Future<void> setTouchId(bool enabled) async {
    state = enabled;
    final SharedPreferences prefs = await SharedPreferences.getInstance();
    await prefs.setBool(_touchIdKey, enabled);
    _loaded = true;
  }
}

class ProfileDataCoordinator {
  ProfileDataCoordinator(this._ref);

  final Ref _ref;

  Future<void> ensureLoaded() async {
    await _ref.read(profileCoreProvider.notifier).ensureLoaded();
    final String fallbackEmail = _ref.read(profileCoreProvider).email;

    await Future.wait<void>(<Future<void>>[
      _ref
          .read(profileNotificationsProvider.notifier)
          .ensureLoaded(fallbackEmail: fallbackEmail),
      _ref
          .read(profileMarketingProvider.notifier)
          .ensureLoaded(fallbackEmail: fallbackEmail),
      _ref.read(biometricPreferenceProvider.notifier).ensureLoaded(),
    ]);
  }

  Future<void> reload() async {
    await _ref.read(profileCoreProvider.notifier).reload();
    final String fallbackEmail = _ref.read(profileCoreProvider).email;

    await Future.wait<void>(<Future<void>>[
      _ref
          .read(profileNotificationsProvider.notifier)
          .reload(fallbackEmail: fallbackEmail),
      _ref
          .read(profileMarketingProvider.notifier)
          .reload(fallbackEmail: fallbackEmail),
      _ref.read(biometricPreferenceProvider.notifier).reload(),
    ]);
  }
}

final StateNotifierProvider<ProfileCoreController, ProfileCoreState>
    profileCoreProvider =
    StateNotifierProvider<ProfileCoreController, ProfileCoreState>(
  ProfileCoreController.new,
);

final StateNotifierProvider<ProfileNotificationsController,
        ProfileNotificationsState> profileNotificationsProvider =
    StateNotifierProvider<ProfileNotificationsController,
        ProfileNotificationsState>(
  ProfileNotificationsController.new,
);

final StateNotifierProvider<ProfileMarketingController, ProfileMarketingState>
    profileMarketingProvider =
    StateNotifierProvider<ProfileMarketingController, ProfileMarketingState>(
  ProfileMarketingController.new,
);

final StateNotifierProvider<BiometricPreferenceController, bool>
    biometricPreferenceProvider =
    StateNotifierProvider<BiometricPreferenceController, bool>(
  (Ref ref) => BiometricPreferenceController(),
);

final Provider<ProfileDataCoordinator> profileDataCoordinatorProvider =
    Provider<ProfileDataCoordinator>(
  ProfileDataCoordinator.new,
);

final Provider<ProfileHeaderState> profileHeaderProvider =
    Provider<ProfileHeaderState>(
  (Ref ref) {
    final ProfileCoreState state = ref.watch(profileCoreProvider);
    return ProfileHeaderState(
      displayName: state.displayName,
      photoUrl: state.photoUrl,
    );
  },
);

final Provider<ProfilePersonalDetailsState> profilePersonalDetailsProvider =
    Provider<ProfilePersonalDetailsState>(
  (Ref ref) {
    final ProfileCoreState state = ref.watch(profileCoreProvider);
    return ProfilePersonalDetailsState(
      displayName: state.displayName,
      phone: state.phone,
    );
  },
);

final Provider<ProfileLoginDetailsState> profileLoginDetailsProvider =
    Provider<ProfileLoginDetailsState>(
  (Ref ref) {
    final ProfileCoreState coreState = ref.watch(profileCoreProvider);
    final bool touchIdEnabled = ref.watch(biometricPreferenceProvider);
    return ProfileLoginDetailsState(
      email: coreState.email,
      touchIdEnabled: touchIdEnabled,
    );
  },
);
