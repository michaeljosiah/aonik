import '../../app/demo/demo_data_mode.dart';
import '../../data/repositories/community_repository.dart';
import '../../features/community/community_data.dart';
import '../mock_behavior.dart';

class MockCommunityRepository implements CommunityRepository {
  MockCommunityRepository({
    this.demoDataMode = DemoDataMode.populated,
  });

  final DemoDataMode demoDataMode;

  @override
  Future<List<CommunityNewsItem>> getNews() async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('community.getNews');

    if (demoDataMode == DemoDataMode.fresh) {
      return const <CommunityNewsItem>[];
    }

    return const <CommunityNewsItem>[
      CommunityNewsItem(
        id: 'news-1',
        title: 'Payabo 2.0 is Here',
        summary:
            'A redesigned experience with smarter budgeting tools and real-time spending insights.',
        imageUrl: 'https://img.youtube.com/vi/PHe0bXAIuk0/hqdefault.jpg',
        date: 'Mar 2026',
        tag: 'Product Update',
      ),
      CommunityNewsItem(
        id: 'news-2',
        title: 'New: Bill Splitting',
        summary:
            'Split bills with friends and family. Send requests and track who has paid.',
        imageUrl: 'https://img.youtube.com/vi/p7HKvqRI_Bo/hqdefault.jpg',
        date: 'Feb 2026',
        tag: 'Feature',
      ),
      CommunityNewsItem(
        id: 'news-3',
        title: 'Financial Literacy Month',
        summary:
            'Join our March challenge — build a savings habit in 30 days with guided daily tips.',
        imageUrl: 'https://img.youtube.com/vi/Ks-_Mh1QhMc/hqdefault.jpg',
        date: 'Mar 2026',
        tag: 'Community',
      ),
      CommunityNewsItem(
        id: 'news-4',
        title: 'Remittance Fees Lowered',
        summary:
            'Send money home for less. We have reduced cross-border fees by up to 40%.',
        imageUrl: 'https://img.youtube.com/vi/PHe0bXAIuk0/hqdefault.jpg',
        date: 'Jan 2026',
        tag: 'Announcement',
      ),
    ];
  }

  @override
  Future<List<CommunityVideo>> getVideos() async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('community.getVideos');

    if (demoDataMode == DemoDataMode.fresh) {
      return const <CommunityVideo>[];
    }

    return const <CommunityVideo>[
      CommunityVideo(
        id: 'vid-1',
        youtubeVideoId: 'PHe0bXAIuk0',
        title: 'Getting Started with Payabo',
        description:
            'Learn how to set up your account, link a bank, and start tracking your spending in minutes.',
        category: 'getting-started',
        duration: '5:32',
        author: 'Payabo Team',
      ),
      CommunityVideo(
        id: 'vid-2',
        youtubeVideoId: 'p7HKvqRI_Bo',
        title: 'Create Your First Budget',
        description:
            'A step-by-step guide to creating and managing budgets that actually work.',
        category: 'budgeting',
        duration: '8:15',
        author: 'Payabo Team',
      ),
      CommunityVideo(
        id: 'vid-3',
        youtubeVideoId: 'Ks-_Mh1QhMc',
        title: 'How to Pay Bills with Payabo',
        description:
            'Pay electricity, water, internet, and more — all from one place.',
        category: 'bills',
        duration: '4:48',
        author: 'Payabo Team',
      ),
      CommunityVideo(
        id: 'vid-4',
        youtubeVideoId: 'PHe0bXAIuk0',
        title: '5 Money-Saving Tips You Need to Know',
        description:
            'Practical tips that can help you save more each month without changing your lifestyle.',
        category: 'tips',
        duration: '6:20',
        author: 'Payabo Team',
      ),
      CommunityVideo(
        id: 'vid-5',
        youtubeVideoId: 'p7HKvqRI_Bo',
        title: 'Understanding Your Spending Insights',
        description:
            'Dive into your spending breakdown and learn how to read the charts and trends.',
        category: 'getting-started',
        duration: '7:02',
        author: 'Payabo Team',
      ),
      CommunityVideo(
        id: 'vid-6',
        youtubeVideoId: 'Ks-_Mh1QhMc',
        title: "What's New in March 2026",
        description:
            'A quick roundup of all the new features and improvements released this month.',
        category: 'news',
        duration: '3:45',
        author: 'Payabo Team',
      ),
      CommunityVideo(
        id: 'vid-7',
        youtubeVideoId: 'lnavWc2KhAQ',
        title: 'Three Things You Should Never Do',
        description:
            "Common mistakes to avoid when managing your finances — learn from others so you don't have to learn the hard way.",
        category: 'tips',
        duration: '1:01',
        author: 'Kuda App',
      ),
    ];
  }

  @override
  Future<List<CommunityVideoCategory>> getCategories() async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('community.getCategories');

    // Categories are structural, so they are always returned regardless of
    // demo data mode.
    return const <CommunityVideoCategory>[
      CommunityVideoCategory(
        id: 'all',
        label: 'All',
        iconCodePoint: 0xf0674, // Icons.grid_view_rounded
        iconFontFamily: 'MaterialIcons',
      ),
      CommunityVideoCategory(
        id: 'getting-started',
        label: 'Getting Started',
        iconCodePoint: 0xf3db, // Icons.rocket_launch_outlined
        iconFontFamily: 'MaterialIcons',
      ),
      CommunityVideoCategory(
        id: 'budgeting',
        label: 'Budgeting',
        iconCodePoint: 0xf336, // Icons.savings_outlined
        iconFontFamily: 'MaterialIcons',
      ),
      CommunityVideoCategory(
        id: 'bills',
        label: 'Bills & Payments',
        iconCodePoint: 0xf2ef, // Icons.receipt_long_outlined
        iconFontFamily: 'MaterialIcons',
      ),
      CommunityVideoCategory(
        id: 'tips',
        label: 'Tips & Tricks',
        iconCodePoint: 0xe3a1, // Icons.lightbulb_outline
        iconFontFamily: 'MaterialIcons',
      ),
      CommunityVideoCategory(
        id: 'news',
        label: 'News',
        iconCodePoint: 0xef45, // Icons.campaign_outlined
        iconFontFamily: 'MaterialIcons',
      ),
    ];
  }
}
