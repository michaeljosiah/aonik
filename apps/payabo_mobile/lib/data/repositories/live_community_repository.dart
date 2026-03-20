import 'dart:convert';
import 'dart:developer' as developer;

import 'package:dio/dio.dart';

import '../../features/community/community_data.dart';
import '../api/api_exception.dart';
import 'community_repository.dart';

/// Live implementation of [CommunityRepository] that fetches community content
/// from the CMS content-block API endpoints.
///
/// ## Backend mapping
///
/// | Mobile model               | ContentBlock area          | Body / TargetingJson usage                     |
/// |----------------------------|----------------------------|------------------------------------------------|
/// | [CommunityNewsItem]        | `CommunityNews`            | Body = summary (Markdown), Media[0] = image    |
/// | [CommunityVideo]           | `CommunityVideo`           | Body = description, TargetingJson = video meta  |
/// | [CommunityVideoCategory]   | `CommunityVideoCategory`   | TargetingJson = icon metadata                   |
///
/// ### CommunityVideo TargetingJson schema
/// ```json
/// {
///   "youtubeVideoId": "PHe0bXAIuk0",
///   "duration": "5:32",
///   "author": "Payabo Team",
///   "category": "getting-started"
/// }
/// ```
///
/// ### CommunityVideoCategory TargetingJson schema
/// ```json
/// {
///   "iconCodePoint": 63604,
///   "iconFontFamily": "MaterialIcons"
/// }
/// ```
class LiveCommunityRepository implements CommunityRepository {
  LiveCommunityRepository({required Dio apiClient}) : _apiClient = apiClient;

  final Dio _apiClient;

  @override
  Future<List<CommunityNewsItem>> getNews() async {
    try {
      final response = await _apiClient.get<List<dynamic>>(
        '/cms/content/active',
        queryParameters: <String, dynamic>{
          'area': 'CommunityNews',
          'locale': 'en',
        },
      );

      final List<dynamic> raw = response.data ?? const <dynamic>[];
      return raw
          .whereType<Map<Object?, Object?>>()
          .map((item) => _mapNewsItem(Map<String, dynamic>.from(item)))
          .toList(growable: false);
    } on DioException catch (exception) {
      _logDioFailure('getNews', exception);
      throw mapDioException(exception);
    }
  }

  @override
  Future<List<CommunityVideo>> getVideos() async {
    try {
      final response = await _apiClient.get<List<dynamic>>(
        '/cms/content/active',
        queryParameters: <String, dynamic>{
          'area': 'CommunityVideo',
          'locale': 'en',
        },
      );

      final List<dynamic> raw = response.data ?? const <dynamic>[];
      return raw
          .whereType<Map<Object?, Object?>>()
          .map((item) => _mapVideo(Map<String, dynamic>.from(item)))
          .where((video) => video != null)
          .cast<CommunityVideo>()
          .toList(growable: false);
    } on DioException catch (exception) {
      _logDioFailure('getVideos', exception);
      throw mapDioException(exception);
    }
  }

  @override
  Future<List<CommunityVideoCategory>> getCategories() async {
    try {
      final response = await _apiClient.get<List<dynamic>>(
        '/cms/content/active',
        queryParameters: <String, dynamic>{
          'area': 'CommunityVideoCategory',
          'locale': 'en',
        },
      );

      final List<dynamic> raw = response.data ?? const <dynamic>[];
      final categories = raw
          .whereType<Map<Object?, Object?>>()
          .map((item) => _mapCategory(Map<String, dynamic>.from(item)))
          .where((cat) => cat != null)
          .cast<CommunityVideoCategory>()
          .toList(growable: false);

      // Always prepend the "All" meta-category (client-side only).
      return <CommunityVideoCategory>[
        const CommunityVideoCategory(
          id: 'all',
          label: 'All',
          iconCodePoint: 0xf0674, // Icons.grid_view_rounded
          iconFontFamily: 'MaterialIcons',
        ),
        ...categories,
      ];
    } on DioException catch (exception) {
      _logDioFailure('getCategories', exception);
      throw mapDioException(exception);
    }
  }

  // ═══════════════════════════════════════════════════════════════
  // Mapping helpers
  // ═══════════════════════════════════════════════════════════════

  CommunityNewsItem _mapNewsItem(Map<String, dynamic> json) {
    final List<dynamic> media =
        json['media'] as List<dynamic>? ?? const <dynamic>[];

    // First media item's URL is the hero image.
    String imageUrl = '';
    if (media.isNotEmpty && media.first is Map) {
      final firstMedia = Map<String, dynamic>.from(media.first as Map);
      imageUrl = _readString(firstMedia['url']) ?? '';
    }

    // Use the tag from targetingJson if present, otherwise fall back to slug.
    final targeting = _parseTargetingJson(json['targetingJson']);
    final tag = _readString(targeting?['tag']) ?? _readString(json['slug']);

    // Format the date from createdAt (ISO-8601) into a display label.
    final createdAt = _readString(json['createdAt']);
    final dateLabel = _formatDateLabel(createdAt);

    return CommunityNewsItem(
      id: _readString(json['contentKey']) ?? _readString(json['id']) ?? '',
      title: _readString(json['title']) ?? '',
      summary: _readString(json['body']) ?? '',
      imageUrl: imageUrl,
      date: dateLabel,
      tag: tag,
    );
  }

  CommunityVideo? _mapVideo(Map<String, dynamic> json) {
    final targeting = _parseTargetingJson(json['targetingJson']);
    if (targeting == null) return null;

    final youtubeVideoId = _readString(targeting['youtubeVideoId']);
    if (youtubeVideoId == null || youtubeVideoId.isEmpty) return null;

    return CommunityVideo(
      id: _readString(json['contentKey']) ?? _readString(json['id']) ?? '',
      youtubeVideoId: youtubeVideoId,
      title: _readString(json['title']) ?? '',
      description: _readString(json['body']) ?? '',
      category: _readString(targeting['category']) ?? 'general',
      duration: _readString(targeting['duration']) ?? '',
      author: _readString(targeting['author']),
    );
  }

  CommunityVideoCategory? _mapCategory(Map<String, dynamic> json) {
    final targeting = _parseTargetingJson(json['targetingJson']);
    final iconCodePoint = targeting?['iconCodePoint'];
    if (iconCodePoint is! int) return null;

    return CommunityVideoCategory(
      id: _readString(json['contentKey']) ?? _readString(json['id']) ?? '',
      label: _readString(json['title']) ?? '',
      iconCodePoint: iconCodePoint,
      iconFontFamily:
          _readString(targeting?['iconFontFamily']) ?? 'MaterialIcons',
    );
  }

  // ═══════════════════════════════════════════════════════════════
  // Utilities
  // ═══════════════════════════════════════════════════════════════

  Map<String, dynamic>? _parseTargetingJson(Object? value) {
    final raw = _readString(value);
    if (raw == null) return null;

    try {
      final decoded = jsonDecode(raw);
      if (decoded is Map<String, dynamic>) return decoded;
    } catch (_) {
      // Malformed JSON — treat as absent.
    }

    return null;
  }

  String _formatDateLabel(String? isoDateString) {
    if (isoDateString == null || isoDateString.isEmpty) return '';

    try {
      final date = DateTime.parse(isoDateString);
      const months = <String>[
        'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
        'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec',
      ];
      return '${months[date.month - 1]} ${date.year}';
    } catch (_) {
      return '';
    }
  }

  String? _readString(Object? value) {
    if (value is! String) return null;
    final trimmed = value.trim();
    return trimmed.isEmpty ? null : trimmed;
  }

  void _logDioFailure(String operation, DioException exception) {
    final request = exception.requestOptions;
    final statusCode = exception.response?.statusCode;

    developer.log(
      '$operation failed for ${request.method} ${request.path}'
      '${statusCode != null ? ' (HTTP $statusCode)' : ''} '
      '[${exception.type.name}].',
      name: 'Payabo.LiveCommunityRepository',
      stackTrace: exception.stackTrace,
    );
  }
}
