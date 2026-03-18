import '../../app/demo/demo_data_mode.dart';
import '../../data/repositories/notification_repository.dart';
import '../../features/notifications/notification_data.dart';
import '../mock_behavior.dart';

class MockNotificationRepository implements NotificationRepository {
  MockNotificationRepository({
    this.demoDataMode = DemoDataMode.populated,
  });

  final DemoDataMode demoDataMode;

  @override
  Future<List<NotificationSection>> getSections() async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('notifications.getSections');

    if (demoDataMode == DemoDataMode.fresh) {
      return const <NotificationSection>[];
    }

    return const <NotificationSection>[
      // ── Today ──
      NotificationSection(
        title: 'Today',
        items: <NotificationItem>[
          NotificationItem(
            title: 'Electricity bill reminder',
            message: 'ECG Power is due tomorrow. Pay now to avoid late fees.',
            timeLabel: '09:42 AM',
            iconCodePoint: 0xf5ca, // Icons.bolt_rounded
            iconFontFamily: 'MaterialIcons',
            iconColorValue: 0xFFB35E17,
            unread: true,
          ),
          NotificationItem(
            title: 'Spend alert',
            message: 'Dining spend is 18% above your monthly pace.',
            timeLabel: '07:15 AM',
            iconCodePoint: 0xf0174, // Icons.show_chart_rounded
            iconFontFamily: 'MaterialIcons',
            iconColorValue: 0xFF355F3E,
            unread: true,
          ),
          NotificationItem(
            title: 'Naira account synced',
            message:
                'Your GTBank current account has been synced. 12 new transactions imported.',
            timeLabel: '06:30 AM',
            iconCodePoint: 0xf0295, // Icons.sync_rounded
            iconFontFamily: 'MaterialIcons',
            iconColorValue: 0xFF31518A,
            unread: true,
          ),
        ],
      ),
      // ── Yesterday ──
      NotificationSection(
        title: 'Yesterday',
        items: <NotificationItem>[
          NotificationItem(
            title: 'Transfer completed',
            message: 'Your GHS 300 transfer to Ama Boafo was successful.',
            timeLabel: '08:03 PM',
            iconCodePoint: 0xf65d, // Icons.compare_arrows_rounded
            iconFontFamily: 'MaterialIcons',
            iconColorValue: 0xFF31518A,
            unread: false,
          ),
          NotificationItem(
            title: 'Budget milestone',
            message:
                'You stayed under groceries budget for 3 weeks straight.',
            timeLabel: '11:20 AM',
            iconCodePoint: 0xf707, // Icons.emoji_events_rounded
            iconFontFamily: 'MaterialIcons',
            iconColorValue: 0xFF8A6325,
            unread: false,
          ),
          NotificationItem(
            title: 'Payment confirmed',
            message: 'Netflix subscription (GHS 58) charged to your account.',
            timeLabel: '00:05 AM',
            iconCodePoint: 0xf1d0, // Icons.check_circle_rounded
            iconFontFamily: 'MaterialIcons',
            iconColorValue: 0xFF2E7D32,
            unread: false,
          ),
        ],
      ),
      // ── 15 Mar 2026 ──
      NotificationSection(
        title: '15 Mar 2026',
        items: <NotificationItem>[
          NotificationItem(
            title: 'Salary received',
            message:
                'GHS 4,232.24 from Employer Payroll credited to your current account.',
            timeLabel: '08:00 AM',
            iconCodePoint: 0xee33, // Icons.account_balance_wallet_outlined
            iconFontFamily: 'MaterialIcons',
            iconColorValue: 0xFF2E7D32,
            unread: false,
          ),
          NotificationItem(
            title: 'Auto-save triggered',
            message: '£200 moved to UK Savings as part of your auto-save rule.',
            timeLabel: '08:05 AM',
            iconCodePoint: 0xf336, // Icons.savings_outlined
            iconFontFamily: 'MaterialIcons',
            iconColorValue: 0xFF1565C0,
            unread: false,
          ),
        ],
      ),
      // ── 12 Mar 2026 ──
      NotificationSection(
        title: '12 Mar 2026',
        items: <NotificationItem>[
          NotificationItem(
            title: 'New insight available',
            message:
                'Payabo found two subscriptions you may want to review.',
            timeLabel: '04:45 PM',
            iconCodePoint: 0xf855, // Icons.lightbulb_rounded
            iconFontFamily: 'MaterialIcons',
            iconColorValue: 0xFF784A34,
            unread: false,
          ),
          NotificationItem(
            title: 'Support plan reminder',
            message:
                'Monthly support for Mama Grace (GHS 500) is due in 3 days.',
            timeLabel: '09:00 AM',
            iconCodePoint: 0xf862, // Icons.favorite_rounded
            iconFontFamily: 'MaterialIcons',
            iconColorValue: 0xFFC62828,
            unread: false,
          ),
          NotificationItem(
            title: 'Credit card alert',
            message:
                'Your Amex card needs reconnection. Tap to fix.',
            timeLabel: '07:30 AM',
            iconCodePoint: 0xef8f, // Icons.credit_card_outlined
            iconFontFamily: 'MaterialIcons',
            iconColorValue: 0xFFD84315,
            unread: false,
          ),
        ],
      ),
      // ── 10 Mar 2026 ──
      NotificationSection(
        title: '10 Mar 2026',
        items: <NotificationItem>[
          NotificationItem(
            title: 'Bill paid successfully',
            message: 'Ghana Water (GHS 90) paid on time. No late fees.',
            timeLabel: '10:30 AM',
            iconCodePoint: 0xf1d0, // Icons.check_circle_rounded
            iconFontFamily: 'MaterialIcons',
            iconColorValue: 0xFF2E7D32,
            unread: false,
          ),
          NotificationItem(
            title: 'Community update',
            message:
                'New video: "5 Money-Saving Tips You Need to Know" is now live.',
            timeLabel: '02:00 PM',
            iconCodePoint: 0xf891, // Icons.play_circle_rounded
            iconFontFamily: 'MaterialIcons',
            iconColorValue: 0xFF6A1B9A,
            unread: false,
          ),
        ],
      ),
      // ── 7 Mar 2026 ──
      NotificationSection(
        title: '7 Mar 2026',
        items: <NotificationItem>[
          NotificationItem(
            title: 'Weekly spending summary',
            message:
                'You spent £412 last week — 8% less than the week before.',
            timeLabel: '09:00 AM',
            iconCodePoint: 0xe5f7, // Icons.stacked_line_chart
            iconFontFamily: 'MaterialIcons',
            iconColorValue: 0xFF355F3E,
            unread: false,
          ),
          NotificationItem(
            title: 'Fuel price alert',
            message:
                'Shell Fuel Station charges increased 5% this month. Consider alternatives.',
            timeLabel: '06:00 PM',
            iconCodePoint: 0xe3a6, // Icons.local_gas_station_outlined
            iconFontFamily: 'MaterialIcons',
            iconColorValue: 0xFFE65100,
            unread: false,
          ),
          NotificationItem(
            title: 'Remittance fee lowered',
            message:
                'Cross-border fees reduced by 40%. Send money home for less.',
            timeLabel: '10:00 AM',
            iconCodePoint: 0xf05b4, // Icons.currency_exchange
            iconFontFamily: 'MaterialIcons',
            iconColorValue: 0xFF00695C,
            unread: false,
          ),
        ],
      ),
    ];
  }
}
