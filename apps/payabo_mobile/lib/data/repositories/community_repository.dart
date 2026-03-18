import '../../features/community/community_data.dart';

/// Repository that provides community content: news articles, videos and video
/// categories.
///
/// A live implementation would fetch from the API; the mock seeds static demo
/// content.
abstract class CommunityRepository {
  Future<List<CommunityNewsItem>> getNews();

  Future<List<CommunityVideo>> getVideos();

  Future<List<CommunityVideoCategory>> getCategories();
}
