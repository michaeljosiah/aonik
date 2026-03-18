import '../../features/notifications/notification_data.dart';

/// Repository that provides notification content grouped into date sections.
///
/// A live implementation would fetch from the API; the mock seeds static demo
/// content.
abstract class NotificationRepository {
  Future<List<NotificationSection>> getSections();
}
