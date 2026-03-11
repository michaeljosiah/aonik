import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../features/app/presentation/design_system_screen.dart';
import '../../features/app/presentation/splash_screen.dart';
import '../../features/auth/presentation/contact_details_screen.dart';
import '../../features/auth/presentation/country_selection_screen.dart';
import '../../features/auth/presentation/forgot_password_screen.dart';
import '../../features/auth/presentation/intro_screen.dart';
import '../../features/auth/presentation/login_details_screen.dart';
import '../../features/auth/presentation/login_screen.dart';
import '../../features/auth/presentation/personal_details_screen.dart';
import '../../features/auth/presentation/phone_code_screen.dart';
import '../../features/auth/presentation/register_screen.dart';
import '../../features/chat/presentation/chat_history_screen.dart';
import '../../features/chat/presentation/chat_screen.dart';
import '../../features/dashboard/presentation/dashboard_screen.dart';
import '../../features/notifications/presentation/notification_center_screen.dart';
import '../../features/payments/presentation/add_friend_screen.dart';
import '../../features/payments/presentation/card_details_screen.dart';
import '../../features/payments/presentation/card_selection_screen.dart';
import '../../features/payments/presentation/checkout_card_screen.dart';
import '../../features/payments/presentation/checkout_help_screen.dart';
import '../../features/payments/presentation/friend_message_screen.dart';
import '../../features/payments/presentation/friend_selection_screen.dart';
import '../../features/payments/presentation/payment_country_screen.dart';
import '../../features/payments/presentation/payment_return_placeholder_screen.dart';
import '../../features/payments/presentation/payment_selection_screen.dart';
import '../../features/payments/presentation/provider_list_screen.dart';
import '../../features/payments/presentation/service_details_screen.dart';
import '../../features/payments/presentation/thank_you_screen.dart';
import '../../features/profile/presentation/demo_data_preferences_screen.dart';
import '../../features/profile/presentation/edit_contact_screen.dart';
import '../../features/profile/presentation/edit_name_screen.dart';
import '../../features/profile/presentation/login_details_screen.dart';
import '../../features/profile/presentation/login_email_screen.dart';
import '../../features/profile/presentation/login_password_screen.dart';
import '../../features/profile/presentation/marketing_email_screen.dart';
import '../../features/profile/presentation/marketing_screen.dart';
import '../../features/profile/presentation/notifications_email_screen.dart';
import '../../features/profile/presentation/notifications_screen.dart';
import '../../features/profile/presentation/personal_details_screen.dart';
import '../../features/profile/presentation/photo_selection_screen.dart';
import '../../features/profile/presentation/profile_screen.dart';
import '../../features/spending/presentation/spending_category_detail_screen.dart';
import '../../features/spending/presentation/spending_budget_screen.dart';
import '../../features/spending/presentation/spending_merchant_detail_screen.dart';
import '../../features/spending/presentation/spending_screen.dart';
import '../auth/auth_controller.dart';

class RouterRefreshNotifier extends ChangeNotifier {
  RouterRefreshNotifier(this._ref) {
    _ref.listen<AuthState>(
      authControllerProvider,
      (_, __) => notifyListeners(),
    );
  }

  final Ref _ref;
}

final Provider<RouterRefreshNotifier> routerRefreshNotifierProvider =
    Provider<RouterRefreshNotifier>(
  (Ref ref) {
    final notifier = RouterRefreshNotifier(ref);
    ref.onDispose(notifier.dispose);
    return notifier;
  },
);

final Provider<GoRouter> appRouterProvider = Provider<GoRouter>(
  (Ref ref) {
    final refreshNotifier = ref.watch(routerRefreshNotifierProvider);

    return GoRouter(
      initialLocation: '/',
      refreshListenable: refreshNotifier,
      redirect: (context, state) {
        final authState = ref.read(authControllerProvider);

        if (!authState.isInitialized) {
          return null;
        }

        final String location = state.uri.path;
        final bool isAuthArea = location == '/' ||
            location == '/intro' ||
            location.startsWith('/auth');

        if (!authState.isAuthenticated &&
            !isAuthArea &&
            location != '/design-system') {
          return '/auth/login';
        }

        if (authState.isAuthenticated && isAuthArea) {
          return '/dashboard';
        }

        return null;
      },
      routes: <GoRoute>[
        GoRoute(
          path: '/',
          name: 'splash',
          builder: (context, state) => const SplashScreen(),
        ),
        GoRoute(
          path: '/design-system',
          name: 'design-system',
          builder: (context, state) => const DesignSystemScreen(),
        ),
        GoRoute(
          path: '/intro',
          name: 'intro',
          builder: (context, state) => const IntroScreen(),
        ),
        GoRoute(
          path: '/auth/login',
          name: 'login',
          builder: (context, state) => const LoginScreen(),
        ),
        GoRoute(
          path: '/auth/forgot-password',
          name: 'forgot-password',
          builder: (context, state) => const ForgotPasswordScreen(),
        ),
        GoRoute(
          path: '/auth/register',
          name: 'register',
          builder: (context, state) => const RegisterScreen(),
        ),
        GoRoute(
          path: '/auth/register/personal-details',
          name: 'personal-details',
          builder: (context, state) => const PersonalDetailsScreen(),
        ),
        GoRoute(
          path: '/auth/register/contact-details',
          name: 'contact-details',
          builder: (context, state) => const ContactDetailsScreen(),
        ),
        GoRoute(
          path: '/auth/register/country-selection',
          name: 'country-selection-registration',
          builder: (context, state) => const CountrySelectionScreen(
              target: CountrySelectionTarget.registration),
        ),
        GoRoute(
          path: '/auth/register/phone-country-selection',
          name: 'country-selection-phone',
          builder: (context, state) => const CountrySelectionScreen(
              target: CountrySelectionTarget.phone),
        ),
        GoRoute(
          path: '/auth/register/phone-code',
          name: 'phone-code',
          builder: (context, state) {
            final disabled = state.uri.queryParameters['disabled'] == 'true';
            return PhoneCodeScreen(initialDisabled: disabled);
          },
        ),
        GoRoute(
          path: '/auth/register/login-details',
          name: 'login-details',
          builder: (context, state) {
            final disabled = state.uri.queryParameters['disabled'] == 'true';
            return LoginDetailsScreen(isDisabledState: disabled);
          },
        ),
        GoRoute(
          path: '/dashboard',
          name: 'dashboard',
          builder: (context, state) => const DashboardScreen(),
        ),
        GoRoute(
          path: '/dashboard/empty',
          name: 'dashboard-empty',
          builder: (context, state) =>
              const DashboardScreen(showEmptyState: true),
        ),
        GoRoute(
          path: '/spending',
          name: 'spending',
          builder: (context, state) => const SpendingScreen(),
        ),
        GoRoute(
          path: '/spending/budgets',
          name: 'spending-budgets',
          builder: (context, state) => const SpendingBudgetScreen(),
        ),
        GoRoute(
          path: '/spending/transactions',
          name: 'spending-transactions',
          redirect: (context, state) => '/spending',
        ),
        GoRoute(
          path: '/spending/category/:categoryId',
          name: 'spending-category-detail',
          builder: (context, state) => SpendingCategoryDetailScreen(
            categoryId: state.pathParameters['categoryId'] ?? 'finances',
          ),
        ),
        GoRoute(
          path: '/spending/merchant/:merchantId',
          name: 'spending-merchant-detail',
          builder: (context, state) => SpendingMerchantDetailScreen(
            merchantId: state.pathParameters['merchantId'] ?? 'amazon',
          ),
        ),
        GoRoute(
          path: '/chat',
          name: 'chat',
          builder: (context, state) => const ChatScreen(),
        ),
        GoRoute(
          path: '/chat/history',
          name: 'chat-history',
          builder: (context, state) => ChatHistoryScreen(
            selectedConversationId: state.uri.queryParameters['selected'],
          ),
        ),
        GoRoute(
          path: '/notifications',
          name: 'notifications-center',
          builder: (context, state) => const NotificationCenterScreen(),
        ),
        GoRoute(
          path: '/payments/country',
          name: 'payment-country',
          builder: (context, state) => const PaymentCountryScreen(),
        ),
        GoRoute(
          path: '/payments/providers',
          name: 'providers',
          builder: (context, state) => const ProviderListScreen(),
        ),
        GoRoute(
          path: '/payments/service-details',
          name: 'payment-service-details',
          builder: (context, state) => const ServiceDetailsScreen(),
        ),
        GoRoute(
          path: '/payments/payment-selection',
          name: 'payment-selection',
          builder: (context, state) => const PaymentSelectionScreen(),
        ),
        GoRoute(
          path: '/payments/card-selection',
          name: 'payment-card-selection',
          builder: (context, state) => const CardSelectionScreen(),
        ),
        GoRoute(
          path: '/payments/card-details',
          name: 'payment-card-details',
          builder: (context, state) => const CardDetailsScreen(),
        ),
        GoRoute(
          path: '/payments/checkout/card',
          name: 'payment-checkout-card',
          builder: (context, state) => const CheckoutCardScreen(),
        ),
        GoRoute(
          path: '/payments/friends',
          name: 'payment-friends',
          builder: (context, state) => const FriendSelectionScreen(),
        ),
        GoRoute(
          path: '/payments/friends/add',
          name: 'payment-add-friend',
          builder: (context, state) => const AddFriendScreen(),
        ),
        GoRoute(
          path: '/payments/friends/message',
          name: 'payment-friend-message',
          builder: (context, state) => const FriendMessageScreen(),
        ),
        GoRoute(
          path: '/payments/checkout/help',
          name: 'payment-checkout-help',
          builder: (context, state) => const CheckoutHelpScreen(),
        ),
        GoRoute(
          path: '/payments/thank-you',
          name: 'payment-thank-you',
          builder: (context, state) => const ThankYouScreen(),
        ),
        GoRoute(
          path: '/payments/return',
          name: 'payment-return-placeholder',
          builder: (context, state) => const PaymentReturnPlaceholderScreen(),
        ),
        GoRoute(
          path: '/profile',
          name: 'profile',
          builder: (context, state) => const ProfileScreen(),
        ),
        GoRoute(
          path: '/profile/photo',
          name: 'profile-photo',
          builder: (context, state) => const PhotoSelectionScreen(),
        ),
        GoRoute(
          path: '/profile/personal-details',
          name: 'profile-personal-details',
          builder: (context, state) => const ProfilePersonalDetailsScreen(),
        ),
        GoRoute(
          path: '/profile/personal-details/name',
          name: 'profile-edit-name',
          builder: (context, state) => const EditNameScreen(),
        ),
        GoRoute(
          path: '/profile/personal-details/contact',
          name: 'profile-edit-contact',
          builder: (context, state) => const EditContactScreen(),
        ),
        GoRoute(
          path: '/profile/login-details',
          name: 'profile-login-details',
          builder: (context, state) => const ProfileLoginDetailsScreen(),
        ),
        GoRoute(
          path: '/profile/login-details/email',
          name: 'profile-login-email',
          builder: (context, state) => const LoginEmailScreen(),
        ),
        GoRoute(
          path: '/profile/login-details/password',
          name: 'profile-login-password',
          builder: (context, state) => const LoginPasswordScreen(),
        ),
        GoRoute(
          path: '/profile/notifications',
          name: 'profile-notifications',
          builder: (context, state) => const NotificationsScreen(),
        ),
        GoRoute(
          path: '/profile/notifications/email',
          name: 'profile-notifications-email',
          builder: (context, state) => const NotificationsEmailScreen(),
        ),
        GoRoute(
          path: '/profile/marketing',
          name: 'profile-marketing',
          builder: (context, state) => const MarketingScreen(),
        ),
        GoRoute(
          path: '/profile/marketing/email',
          name: 'profile-marketing-email',
          builder: (context, state) => const MarketingEmailScreen(),
        ),
        GoRoute(
          path: '/profile/demo-data',
          name: 'profile-demo-data',
          builder: (context, state) => const DemoDataPreferencesScreen(),
        ),
      ],
    );
  },
);
