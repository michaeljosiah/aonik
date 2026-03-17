import 'package:flutter/material.dart';

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
    required this.icon,
  });

  final String id;
  final String label;
  final IconData icon;
}

// ── Mock data ─────────────────────────────────────────────

const List<CommunityVideoCategory> kCommunityCategories =
    <CommunityVideoCategory>[
  CommunityVideoCategory(
    id: 'all',
    label: 'All',
    icon: Icons.grid_view_rounded,
  ),
  CommunityVideoCategory(
    id: 'getting-started',
    label: 'Getting Started',
    icon: Icons.rocket_launch_outlined,
  ),
  CommunityVideoCategory(
    id: 'budgeting',
    label: 'Budgeting',
    icon: Icons.savings_outlined,
  ),
  CommunityVideoCategory(
    id: 'bills',
    label: 'Bills & Payments',
    icon: Icons.receipt_long_outlined,
  ),
  CommunityVideoCategory(
    id: 'tips',
    label: 'Tips & Tricks',
    icon: Icons.lightbulb_outline,
  ),
  CommunityVideoCategory(
    id: 'news',
    label: 'News',
    icon: Icons.campaign_outlined,
  ),
];

const List<CommunityNewsItem> kCommunityNews = <CommunityNewsItem>[
  CommunityNewsItem(
    id: 'news-1',
    title: 'Payabo 2.0 is Here',
    summary:
        'A redesigned experience with smarter budgeting tools and real-time spending insights.',
    imageUrl: 'https://img.youtube.com/vi/lnavWc2KhAQ/hqdefault.jpg',
    date: 'Mar 2026',
    tag: 'Product Update',
  ),
  CommunityNewsItem(
    id: 'news-2',
    title: 'New: Bill Splitting',
    summary:
        'Split bills with friends and family. Send requests and track who has paid.',
    imageUrl: 'https://img.youtube.com/vi/lnavWc2KhAQ/hqdefault.jpg',
    date: 'Feb 2026',
    tag: 'Feature',
  ),
  CommunityNewsItem(
    id: 'news-3',
    title: 'Financial Literacy Month',
    summary:
        'Join our March challenge — build a savings habit in 30 days with guided daily tips.',
    imageUrl: 'https://img.youtube.com/vi/lnavWc2KhAQ/hqdefault.jpg',
    date: 'Mar 2026',
    tag: 'Community',
  ),
  CommunityNewsItem(
    id: 'news-4',
    title: 'Remittance Fees Lowered',
    summary:
        'Send money home for less. We have reduced cross-border fees by up to 40%.',
    imageUrl: 'https://img.youtube.com/vi/lnavWc2KhAQ/hqdefault.jpg',
    date: 'Jan 2026',
    tag: 'Announcement',
  ),
];

const List<CommunityVideo> kCommunityVideos = <CommunityVideo>[
  CommunityVideo(
    id: 'vid-1',
    youtubeVideoId: 'lnavWc2KhAQ',
    title: 'Getting Started with Payabo',
    description:
        'Learn how to set up your account, link a bank, and start tracking your spending in minutes.',
    category: 'getting-started',
    duration: '5:32',
    author: 'Payabo Team',
  ),
  CommunityVideo(
    id: 'vid-2',
    youtubeVideoId: 'lnavWc2KhAQ',
    title: 'Create Your First Budget',
    description:
        'A step-by-step guide to creating and managing budgets that actually work.',
    category: 'budgeting',
    duration: '8:15',
    author: 'Payabo Team',
  ),
  CommunityVideo(
    id: 'vid-3',
    youtubeVideoId: 'lnavWc2KhAQ',
    title: 'How to Pay Bills with Payabo',
    description:
        'Pay electricity, water, internet, and more — all from one place.',
    category: 'bills',
    duration: '4:48',
    author: 'Payabo Team',
  ),
  CommunityVideo(
    id: 'vid-4',
    youtubeVideoId: 'lnavWc2KhAQ',
    title: '5 Money-Saving Tips You Need to Know',
    description:
        'Practical tips that can help you save more each month without changing your lifestyle.',
    category: 'tips',
    duration: '6:20',
    author: 'Payabo Team',
  ),
  CommunityVideo(
    id: 'vid-5',
    youtubeVideoId: 'lnavWc2KhAQ',
    title: 'Understanding Your Spending Insights',
    description:
        'Dive into your spending breakdown and learn how to read the charts and trends.',
    category: 'getting-started',
    duration: '7:02',
    author: 'Payabo Team',
  ),
  CommunityVideo(
    id: 'vid-6',
    youtubeVideoId: 'lnavWc2KhAQ',
    title: 'What\'s New in March 2026',
    description:
        'A quick roundup of all the new features and improvements released this month.',
    category: 'news',
    duration: '3:45',
    author: 'Payabo Team',
  ),
];
