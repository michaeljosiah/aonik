/// Represents a news/announcement card shown in the top carousel.
class CommunityNewsItem {
  const CommunityNewsItem({
    required this.id,
    required this.title,
    required this.summary,
    required this.imageUrl,
    required this.date,
    this.tag,
  });

  final String id;
  final String title;
  final String summary;
  final String imageUrl;
  final String date;
  final String? tag;
}

/// Represents a video guide entry.
class CommunityVideo {
  const CommunityVideo({
    required this.id,
    required this.youtubeVideoId,
    required this.title,
    required this.description,
    required this.category,
    required this.duration,
    this.author,
  });

  final String id;
  final String youtubeVideoId;
  final String title;
  final String description;
  final String category;
  final String duration;
  final String? author;

  /// YouTube thumbnail URL derived from the video ID.
  String get thumbnailUrl =>
      'https://img.youtube.com/vi/$youtubeVideoId/hqdefault.jpg';
}

/// A video category with its icon and colour accent.
class CommunityVideoCategory {
  const CommunityVideoCategory({
    required this.id,
    required this.label,
    required this.iconCodePoint,
    required this.iconFontFamily,
  });

  final String id;
  final String label;
  final int iconCodePoint;
  final String iconFontFamily;
}

