import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../data/repositories/profile_repository.dart';
import '../../../data/repositories/repository_providers.dart';

class ProfileState {
  const ProfileState({
    required this.firstName,
    required this.lastName,
    required this.email,
    required this.phone,
    required this.countryCode,
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

  String get displayName => '$firstName $lastName';

  ProfileState copyWith({
    String? firstName,
    String? lastName,
    String? email,
    String? phone,
    String? countryCode,
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
      firstName: 'John',
      lastName: 'Doe',
      email: 'johndoe@mail.com',
      phone: '+44 999 999 999',
      countryCode: 'GB',
      photoLabel: 'Add photo',
      touchIdEnabled: true,
      notificationsEmail: 'johndoe@mail.com',
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
      marketingEmail: 'johndoe@mail.com',
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

  Future<void> ensureLoaded() async {
    if (state.loaded) {
      return;
    }

    final profile = await _ref.read(profileRepositoryProvider).getProfile();
    state = state.copyWith(
      firstName: profile.firstName,
      lastName: profile.lastName,
      email: profile.email,
      phone: profile.phone,
      countryCode: profile.countryCode,
      notificationsEmail: profile.email,
      marketingEmail: profile.email,
      loaded: true,
    );
  }

  Future<void> updateName(
      {required String firstName, required String lastName}) async {
    state =
        state.copyWith(firstName: firstName.trim(), lastName: lastName.trim());
    await _persistCoreProfile();
  }

  Future<void> updatePhone(String phone) async {
    state = state.copyWith(phone: phone.trim());
    await _persistCoreProfile();
  }

  Future<void> updateLoginEmail(String email) async {
    state = state.copyWith(email: email.trim());
    await _persistCoreProfile();
  }

  void setPhotoLabel(String label) {
    state = state.copyWith(photoLabel: label);
  }

  void setTouchId(bool enabled) {
    state = state.copyWith(touchIdEnabled: enabled);
  }

  void setNotificationsEmail(String email) {
    state = state.copyWith(notificationsEmail: email.trim());
  }

  void setMarketingEmail(String email) {
    state = state.copyWith(marketingEmail: email.trim());
  }

  void setPushToggle({
    bool? newBills,
    bool? billUpdates,
    bool? billAssist,
    bool? mbaMessages,
    bool? orgMessages,
    bool? friendsMessages,
  }) {
    state = state.copyWith(
      newBillsPush: newBills,
      billUpdatesPush: billUpdates,
      billAssistPush: billAssist,
      mbaMessagesPush: mbaMessages,
      orgMessagesPush: orgMessages,
      friendsMessagesPush: friendsMessages,
    );
  }

  void setEmailNotificationToggle({
    bool? newBills,
    bool? billUpdates,
    bool? billAssist,
    bool? mbaMessages,
    bool? orgMessages,
  }) {
    state = state.copyWith(
      newBillsEmail: newBills,
      billUpdatesEmail: billUpdates,
      billAssistEmail: billAssist,
      mbaMessagesEmail: mbaMessages,
      orgMessagesEmail: orgMessages,
    );
  }

  void setMarketingToggle({bool? news, bool? offers, bool? surveys}) {
    state = state.copyWith(
      marketingNews: news,
      marketingOffers: offers,
      marketingSurveys: surveys,
    );
  }

  Future<void> _persistCoreProfile() async {
    await _ref.read(profileRepositoryProvider).updateProfile(
          UserProfile(
            firstName: state.firstName,
            lastName: state.lastName,
            email: state.email,
            phone: state.phone,
            countryCode: state.countryCode,
          ),
        );
  }
}

final StateNotifierProvider<ProfileController, ProfileState>
    profileControllerProvider =
    StateNotifierProvider<ProfileController, ProfileState>(
  ProfileController.new,
);
