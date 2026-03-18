/// A group of notification items under a shared date/time heading.
class NotificationSection {
  const NotificationSection({
    required this.title,
    required this.items,
  });

  final String title;
  final List<NotificationItem> items;
}

/// A single notification entry within a section.
class NotificationItem {
  const NotificationItem({
    required this.title,
    required this.message,
    required this.timeLabel,
    required this.iconCodePoint,
    required this.iconFontFamily,
    required this.iconColorValue,
    required this.unread,
  });

  final String title;
  final String message;
  final String timeLabel;
  final int iconCodePoint;
  final String iconFontFamily;
  final int iconColorValue;
  final bool unread;
}
