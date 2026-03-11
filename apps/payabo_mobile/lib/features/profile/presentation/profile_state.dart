import 'dart:developer' as developer;

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/legacy.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../../../data/repositories/profile_repository.dart';
import '../../../data/repositories/repository_providers.dart';

const String _touchIdKey = 'profile.touchIdEnabled';

class ProfileState {
  const ProfileState({
    required this.firstName,
    required this.lastName,
    required this.email,
    required this.phone,
    required this.countryCode,
    required this.photoUrl,
    required this.photoLabel,
    required this.touchIdEnabled,
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
    required this.marketingEmail,
    required this.marketingNews,
    required this.marketingOffers,
    required this.marketingSurveys,
    required this.loaded,
  });

  final String firstName;
  final String lastName;
  final String email;
  final String phone;
  final String countryCode;
  final String? photoUrl;
  final String photoLabel;
  final bool touchIdEnabled;
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
  final String marketingEmail;
  final bool marketingNews;
  final bool marketingOffers;
  final bool marketingSurveys;
  final bool loaded;

  String get displayName => '$firstName $lastName'.trim();

  ProfileState copyWith({
    String? firstName,
    String? lastName,
    String? email,
    String? phone,
    String? countryCode,
    String? photoUrl,
    bool clearPhotoUrl = false,
    String? photoLabel,
    bool? touchIdEnabled,
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
    String? marketingEmail,
    bool? marketingNews,
    bool? marketingOffers,
    bool? marketingSurveys,
    bool? loaded,
  }) {
    return ProfileState(
      firstName: firstName ?? this.firstName,
      lastName: lastName ?? this.lastName,
      email: email ?? this.email,
      phone: phone ?? this.phone,
      countryCode: countryCode ?? this.countryCode,
      photoUrl: clearPhotoUrl ? null : (photoUrl ?? this.photoUrl),
      photoLabel: photoLabel ?? this.photoLabel,
      touchIdEnabled: touchIdEnabled ?? this.touchIdEnabled,
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
      marketingEmail: marketingEmail ?? this.marketingEmail,
      marketingNews: marketingNews ?? this.marketingNews,
      marketingOffers: marketingOffers ?? this.marketingOffers,
      marketingSurveys: marketingSurveys ?? this.marketingSurveys,
      loaded: loaded ?? this.loaded,
    );
  }

  factory ProfileState.initial() {
    return const ProfileState(
      firstName: '',
      lastName: '',
      email: '',
      phone: '',
      countryCode: '',
      photoUrl: null,
      photoLabel: 'Add photo',
      touchIdEnabled: false,
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
      marketingEmail: '',
      marketingNews: true,
      marketingOffers: true,
      marketingSurveys: false,
      loaded: false,
    );
  }
}

class ProfileController extends StateNotifier<ProfileState> {
  ProfileController(this._ref) : super(ProfileState.initial());

  final Ref _ref;

  ProfileRepository get _repository => _ref.read(profileRepositoryProvider);

  Future<void> ensureLoaded() async {
    if (state.loaded) {
      return;
    }

    final profile = await _loadProfile();

    final results = await Future.wait<Object>(<Future<Object>>[
      _loadNotificationPreferencesOrDefault(profile.email),
      _loadMarketingPreferencesOrDefault(profile.email),
    ]);

    final notifPrefs = results[0] as NotificationPreferences;
    final marketingPrefs = results[1] as MarketingPreferences;

    final prefs = await SharedPreferences.getInstance();
    final touchIdEnabled = prefs.getBool(_touchIdKey) ?? false;

    state = state.copyWith(
      firstName: profile.firstName,
      lastName: profile.lastName,
      email: profile.email,
      phone: profile.phone,
      countryCode: profile.countryCode,
      photoUrl: profile.photoUrl,
      photoLabel: profile.photoUrl != null ? 'Change photo' : 'Add photo',
      touchIdEnabled: touchIdEnabled,
      notificationsEmail: notifPrefs.email,
      newBillsPush: notifPrefs.newBillsPush,
      billUpdatesPush: notifPrefs.billUpdatesPush,
      billAssistPush: notifPrefs.billAssistPush,
      mbaMessagesPush: notifPrefs.mbaMessagesPush,
      orgMessagesPush: notifPrefs.orgMessagesPush,
      friendsMessagesPush: notifPrefs.friendsMessagesPush,
      newBillsEmail: notifPrefs.newBillsEmail,
      billUpdatesEmail: notifPrefs.billUpdatesEmail,
      billAssistEmail: notifPrefs.billAssistEmail,
      mbaMessagesEmail: notifPrefs.mbaMessagesEmail,
      orgMessagesEmail: notifPrefs.orgMessagesEmail,
      marketingEmail: marketingPrefs.email,
      marketingNews: marketingPrefs.news,
      marketingOffers: marketingPrefs.offers,
      marketingSurveys: marketingPrefs.surveys,
      loaded: true,
    );
  }

  Future<UserProfile> _loadProfile() async {
    try {
      return await _repository.getProfile();
    } catch (error, stackTrace) {
      developer.log(
        'Failed to load customer profile.',
        name: 'Payabo.ProfileController',
        error: error,
        stackTrace: stackTrace,
      );
      rethrow;
    }
  }

  Future<NotificationPreferences> _loadNotificationPreferencesOrDefault(
    String fallbackEmail,
  ) async {
    try {
      return await _repository.getNotificationPreferences();
    } catch (error, stackTrace) {
      developer.log(
        'Failed to load notification preferences. Falling back to defaults.',
        name: 'Payabo.ProfileController',
        error: error,
        stackTrace: stackTrace,
      );

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

  Future<MarketingPreferences> _loadMarketingPreferencesOrDefault(
    String fallbackEmail,
  ) async {
    try {
      return await _repository.getMarketingPreferences();
    } catch (error, stackTrace) {
      developer.log(
        'Failed to load marketing preferences. Falling back to defaults.',
        name: 'Payabo.ProfileController',
        error: error,
        stackTrace: stackTrace,
      );

      return MarketingPreferences(
        email: fallbackEmail,
        news: state.marketingNews,
        offers: state.marketingOffers,
        surveys: state.marketingSurveys,
      );
    }
  }

  // -- Core profile --

  Future<void> updateName(
      {required String firstName, required String lastName}) async {
    final previousState = state;
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

  Future<void> updatePhone(String phone) async {
    final previousState = state;
    state = state.copyWith(phone: phone.trim());

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
    final profile = await _repository.updateEmail(
      currentEmail: currentEmail,
      newEmail: newEmail,
      password: password,
    );

    state = state.copyWith(
      firstName: profile.firstName,
      lastName: profile.lastName,
      email: profile.email,
      phone: profile.phone,
      countryCode: profile.countryCode,
      photoUrl: profile.photoUrl,
      loaded: true,
    );
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

  // -- Photo --

  Future<void> uploadPhoto(String filePath) async {
    final uploadedUrl = await _repository.uploadPhoto(filePath);
    var resolvedUrl = uploadedUrl.trim();

    developer.log(
      'uploadPhoto returned URL: ${resolvedUrl.isEmpty ? '<empty>' : resolvedUrl}',
      name: 'Payabo.ProfileController',
    );

    if (resolvedUrl.isEmpty) {
      final profile = await _repository.getProfile();
      resolvedUrl = profile.photoUrl?.trim() ?? '';

      developer.log(
        'Reloaded profile after upload. photoUrl: ${resolvedUrl.isEmpty ? '<empty>' : resolvedUrl}',
        name: 'Payabo.ProfileController',
      );
    }

    state = state.copyWith(
      photoUrl: resolvedUrl.isEmpty ? null : resolvedUrl,
      photoLabel: 'Change photo',
    );

    developer.log(
      'Profile state photoUrl set to: ${state.photoUrl ?? '<empty>'}',
      name: 'Payabo.ProfileController',
    );
  }

  Future<void> deletePhoto() async {
    await _repository.deletePhoto();
    state = state.copyWith(clearPhotoUrl: true, photoLabel: 'Add photo');
  }

  void setPhotoLabel(String label) {
    state = state.copyWith(photoLabel: label);
  }

  // -- Touch ID --

  Future<void> setTouchId(bool enabled) async {
    state = state.copyWith(touchIdEnabled: enabled);
    final prefs = await SharedPreferences.getInstance();
    await prefs.setBool(_touchIdKey, enabled);
  }

  // -- Notification preferences --

  Future<void> setNotificationsEmail(String email) async {
    final previous = state;
    state = state.copyWith(notificationsEmail: email.trim());

    try {
      await _persistNotificationPreferences();
    } catch (_) {
      state = previous;
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
    final previous = state;
    state = state.copyWith(
      newBillsPush: newBills,
      billUpdatesPush: billUpdates,
      billAssistPush: billAssist,
      mbaMessagesPush: mbaMessages,
      orgMessagesPush: orgMessages,
      friendsMessagesPush: friendsMessages,
    );

    try {
      await _persistNotificationPreferences();
    } catch (_) {
      state = previous;
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
    final previous = state;
    state = state.copyWith(
      newBillsEmail: newBills,
      billUpdatesEmail: billUpdates,
      billAssistEmail: billAssist,
      mbaMessagesEmail: mbaMessages,
      orgMessagesEmail: orgMessages,
    );

    try {
      await _persistNotificationPreferences();
    } catch (_) {
      state = previous;
      rethrow;
    }
  }

  // -- Marketing preferences --

  Future<void> setMarketingEmail(String email) async {
    final previous = state;
    state = state.copyWith(marketingEmail: email.trim());

    try {
      await _persistMarketingPreferences();
    } catch (_) {
      state = previous;
      rethrow;
    }
  }

  Future<void> setMarketingToggle(
      {bool? news, bool? offers, bool? surveys}) async {
    final previous = state;
    state = state.copyWith(
      marketingNews: news,
      marketingOffers: offers,
      marketingSurveys: surveys,
    );

    try {
      await _persistMarketingPreferences();
    } catch (_) {
      state = previous;
      rethrow;
    }
  }

  // -- Private helpers --

  Future<void> _persistCoreProfile() async {
    final profile = await _repository.updateProfile(
      UserProfile(
        firstName: state.firstName,
        lastName: state.lastName,
        email: state.email,
        phone: state.phone,
        countryCode: state.countryCode,
        photoUrl: state.photoUrl,
      ),
    );

    state = state.copyWith(
      firstName: profile.firstName,
      lastName: profile.lastName,
      email: profile.email,
      phone: profile.phone,
      countryCode: profile.countryCode,
      photoUrl: profile.photoUrl,
      loaded: true,
    );
  }

  Future<void> _persistNotificationPreferences() async {
    final prefs = await _repository.updateNotificationPreferences(
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

    state = state.copyWith(
      notificationsEmail: prefs.email,
      newBillsPush: prefs.newBillsPush,
      billUpdatesPush: prefs.billUpdatesPush,
      billAssistPush: prefs.billAssistPush,
      mbaMessagesPush: prefs.mbaMessagesPush,
      orgMessagesPush: prefs.orgMessagesPush,
      friendsMessagesPush: prefs.friendsMessagesPush,
      newBillsEmail: prefs.newBillsEmail,
      billUpdatesEmail: prefs.billUpdatesEmail,
      billAssistEmail: prefs.billAssistEmail,
      mbaMessagesEmail: prefs.mbaMessagesEmail,
      orgMessagesEmail: prefs.orgMessagesEmail,
    );
  }

  Future<void> _persistMarketingPreferences() async {
    final prefs = await _repository.updateMarketingPreferences(
      MarketingPreferences(
        email: state.marketingEmail,
        news: state.marketingNews,
        offers: state.marketingOffers,
        surveys: state.marketingSurveys,
      ),
    );

    state = state.copyWith(
      marketingEmail: prefs.email,
      marketingNews: prefs.news,
      marketingOffers: prefs.offers,
      marketingSurveys: prefs.surveys,
    );
  }
}

final StateNotifierProvider<ProfileController, ProfileState>
    profileControllerProvider =
    StateNotifierProvider<ProfileController, ProfileState>(
  ProfileController.new,
);
