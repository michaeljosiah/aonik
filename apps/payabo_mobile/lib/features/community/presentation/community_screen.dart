import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../app/demo/demo_data_mode.dart';
import '../../../data/repositories/repository_providers.dart';
import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_radii.dart';
import '../../../shared/theme/payabo_shadows.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_primary_app_shell.dart';
import '../../../shared/widgets/payabo_warm_scaffold.dart';
import '../community_data.dart';
import 'community_video_sheet.dart';

// ─────────────────────────────────────────────────────────
//  Community data providers (backed by CommunityRepository)
// ─────────────────────────────────────────────────────────

final _communityNewsFutureProvider =
    FutureProvider<List<CommunityNewsItem>>((Ref ref) async {
  ref.watch(demoDataModeProvider);
  final repository = ref.watch(communityRepositoryProvider);
  return repository.getNews();
});

final _communityVideosFutureProvider =
    FutureProvider<List<CommunityVideo>>((Ref ref) async {
  ref.watch(demoDataModeProvider);
  final repository = ref.watch(communityRepositoryProvider);
  return repository.getVideos();
});

final _communityCategoriesFutureProvider =
    FutureProvider<List<CommunityVideoCategory>>((Ref ref) async {
  ref.watch(demoDataModeProvider);
  final repository = ref.watch(communityRepositoryProvider);
  return repository.getCategories();
});

class CommunityScreen extends ConsumerStatefulWidget {
  const CommunityScreen({super.key});

  @override
  ConsumerState<CommunityScreen> createState() => _CommunityScreenState();
}

class _CommunityScreenState extends ConsumerState<CommunityScreen>
    with SingleTickerProviderStateMixin {
  String _selectedCategoryId = 'all';

  // ── Entrance animation ───────────────────────────────────
  late final AnimationController _entranceController;
  late final Animation<double> _fadeIn;
  late final Animation<Offset> _slideUp;

  @override
  void initState() {
    super.initState();
    _entranceController = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 500),
    );
    _fadeIn = CurvedAnimation(
      parent: _entranceController,
      curve: Curves.easeOutCubic,
    );
    _slideUp = Tween<Offset>(
      begin: const Offset(0, 0.08),
      end: Offset.zero,
    ).animate(_fadeIn);
    _entranceController.forward();
  }

  @override
  void dispose() {
    _entranceController.dispose();
    super.dispose();
  }

  List<CommunityVideo> _filteredVideos(List<CommunityVideo> allVideos) {
    if (_selectedCategoryId == 'all') {
      return allVideos;
    }
    return allVideos
        .where((v) => v.category == _selectedCategoryId)
        .toList();
  }

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final textTheme = Theme.of(context).textTheme;

    // Resolve community data from repository-backed FutureProviders.
    final news = ref.watch(_communityNewsFutureProvider).when(
          data: (List<CommunityNewsItem> data) => data,
          loading: () => const <CommunityNewsItem>[],
          error: (_, __) => const <CommunityNewsItem>[],
        );
    final allVideos = ref.watch(_communityVideosFutureProvider).when(
          data: (List<CommunityVideo> data) => data,
          loading: () => const <CommunityVideo>[],
          error: (_, __) => const <CommunityVideo>[],
        );
    final categories = ref.watch(_communityCategoriesFutureProvider).when(
          data: (List<CommunityVideoCategory> data) => data,
          loading: () => const <CommunityVideoCategory>[],
          error: (_, __) => const <CommunityVideoCategory>[],
        );
    final filteredVideos = _filteredVideos(allVideos);

    return PayaboWarmScaffold(
      body: SlideTransition(
        position: _slideUp,
        child: FadeTransition(
          opacity: _fadeIn,
          child: CustomScrollView(
            slivers: <Widget>[
              // ── Header ──────────────────────────────────────
              SliverToBoxAdapter(
                child: Padding(
                  padding: const EdgeInsets.fromLTRB(
                    PayaboSpacing.xl,
                    PayaboSpacing.md,
                    PayaboSpacing.xl,
                    PayaboSpacing.lg,
                  ),
                  child: Row(
                    children: <Widget>[
                      InkWell(
                        onTap: () {
                        if (context.canPop()) {
                          context.pop();
                        } else {
                          context.go('/');
                        }
                      },
                        customBorder: const CircleBorder(),
                        child: Container(
                          width: 40,
                          height: 40,
                          decoration: BoxDecoration(
                            color: c.surfaceBase,
                            shape: BoxShape.circle,
                            border: Border.all(color: c.borderDefault),
                          ),
                          child: Icon(
                            Icons.arrow_back_rounded,
                            size: 20,
                            color: c.textPrimary,
                          ),
                        ),
                      ),
                      const SizedBox(width: PayaboSpacing.md),
                      Expanded(
                        child: Text(
                          'Community',
                          style: textTheme.headlineSmall?.copyWith(
                            fontWeight: FontWeight.w700,
                            color: c.headerTitle,
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
              ),

              // ── News & Updates carousel ─────────────────────
              SliverToBoxAdapter(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Padding(
                      padding: const EdgeInsets.symmetric(
                        horizontal: PayaboSpacing.xl,
                      ),
                      child: Text(
                        'Latest News & Updates',
                        style: textTheme.titleMedium?.copyWith(
                          fontWeight: FontWeight.w700,
                          color: c.textPrimary,
                        ),
                      ),
                    ),
                    const SizedBox(height: PayaboSpacing.md),
                    SizedBox(
                      height: 200,
                      child: ListView.separated(
                        scrollDirection: Axis.horizontal,
                        padding: const EdgeInsets.symmetric(
                          horizontal: PayaboSpacing.xl,
                        ),
                        itemCount: news.length,
                        separatorBuilder: (_, __) =>
                            const SizedBox(width: PayaboSpacing.md),
                        itemBuilder: (context, index) {
                          final newsItem = news[index];
                          return _StaggeredFadeItem(
                            index: index,
                            child: _NewsCard(news: newsItem),
                          );
                        },
                      ),
                    ),
                  ],
                ),
              ),

              const SliverToBoxAdapter(
                child: SizedBox(height: PayaboSpacing.x2),
              ),

              // ── Video Guides header ─────────────────────────
              SliverToBoxAdapter(
                child: Padding(
                  padding: const EdgeInsets.symmetric(
                    horizontal: PayaboSpacing.xl,
                  ),
                  child: Text(
                    'Video Guides',
                    style: textTheme.titleMedium?.copyWith(
                      fontWeight: FontWeight.w700,
                      color: c.textPrimary,
                    ),
                  ),
                ),
              ),

              const SliverToBoxAdapter(
                child: SizedBox(height: PayaboSpacing.md),
              ),

              // ── Category filter chips ───────────────────────
              SliverToBoxAdapter(
                child: SizedBox(
                  height: 40,
                  child: ListView.separated(
                    scrollDirection: Axis.horizontal,
                    padding: const EdgeInsets.symmetric(
                      horizontal: PayaboSpacing.xl,
                    ),
                    itemCount: categories.length,
                    separatorBuilder: (_, __) =>
                        const SizedBox(width: PayaboSpacing.sm),
                    itemBuilder: (context, index) {
                      final cat = categories[index];
                      final isSelected = cat.id == _selectedCategoryId;
                      return _CategoryChip(
                        category: cat,
                        isSelected: isSelected,
                        onTap: () =>
                            setState(() => _selectedCategoryId = cat.id),
                      );
                    },
                  ),
                ),
              ),

              const SliverToBoxAdapter(
                child: SizedBox(height: PayaboSpacing.lg),
              ),

              // ── Video grid (with animated content switch) ───
              filteredVideos.isEmpty
                  ? SliverToBoxAdapter(
                      child: AnimatedSwitcher(
                        duration: const Duration(milliseconds: 300),
                        switchInCurve: Curves.easeOutCubic,
                        switchOutCurve: Curves.easeInCubic,
                        child: _EmptyVideoState(
                          key: ValueKey<String>(
                            'empty-$_selectedCategoryId',
                          ),
                        ),
                      ),
                    )
                  : SliverPadding(
                      padding: const EdgeInsets.symmetric(
                        horizontal: PayaboSpacing.xl,
                      ),
                      sliver: SliverList(
                        delegate: SliverChildBuilderDelegate(
                          (context, index) {
                            final video = filteredVideos[index];
                            return Padding(
                              padding: const EdgeInsets.only(
                                bottom: PayaboSpacing.lg,
                              ),
                              child: _StaggeredFadeItem(
                                index: index,
                                child: _VideoCard(
                                  video: video,
                                  categories: categories,
                                  onTap: () => showCommunityVideoSheet(
                                    context: context,
                                    video: video,
                                  ),
                                ),
                              ),
                            );
                          },
                          childCount: filteredVideos.length,
                        ),
                      ),
                    ),

              // Bottom padding to clear the nav bar
              const SliverToBoxAdapter(
                child: SizedBox(height: PayaboSpacing.x4),
              ),
            ],
          ),
        ),
      ),
      bottomNavigationBar: const PayaboPrimaryAppShell(
        destination: PayaboPrimaryDestination.none,
      ),
    );
  }
}

// ── Staggered fade-in helper ─────────────────────────────────
//
// Each child fades + slides in with a staggered delay based on
// its [index]. Matches the app's 500ms / easeOutCubic entrance
// convention.

class _StaggeredFadeItem extends StatefulWidget {
  const _StaggeredFadeItem({
    required this.index,
    required this.child,
  });

  final int index;
  final Widget child;

  @override
  State<_StaggeredFadeItem> createState() => _StaggeredFadeItemState();
}

class _StaggeredFadeItemState extends State<_StaggeredFadeItem>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller;
  late final Animation<double> _opacity;
  late final Animation<Offset> _offset;

  @override
  void initState() {
    super.initState();
    _controller = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 500),
    );

    final curve = CurvedAnimation(
      parent: _controller,
      curve: Curves.easeOutCubic,
    );
    _opacity = curve;
    _offset = Tween<Offset>(
      begin: const Offset(0, 0.15),
      end: Offset.zero,
    ).animate(curve);

    // Stagger: 80ms per index, capped at 400ms total delay.
    final delay = Duration(milliseconds: (widget.index * 80).clamp(0, 400));
    Future<void>.delayed(delay, () {
      if (mounted) _controller.forward();
    });
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return SlideTransition(
      position: _offset,
      child: FadeTransition(
        opacity: _opacity,
        child: widget.child,
      ),
    );
  }
}

// ── Empty video state card ───────────────────────────────────
//
// Follows the app's Tier 2 inline empty state pattern:
// card with icon container + title + body text.

class _EmptyVideoState extends StatelessWidget {
  const _EmptyVideoState({super.key});

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final textTheme = Theme.of(context).textTheme;

    return Padding(
      padding: const EdgeInsets.symmetric(
        horizontal: PayaboSpacing.xl,
        vertical: PayaboSpacing.x2,
      ),
      child: Container(
        width: double.infinity,
        padding: PayaboSpacing.card,
        decoration: BoxDecoration(
          color: c.surfaceBase,
          borderRadius: PayaboRadii.radiusLg,
          border: Border.all(color: c.borderDefault),
          boxShadow: PayaboShadows.soft,
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Container(
              width: 56,
              height: 56,
              decoration: BoxDecoration(
                color: c.primary.withValues(alpha: 0.12),
                borderRadius: PayaboRadii.radiusLg,
              ),
              child: Icon(
                Icons.videocam_off_outlined,
                color: c.primary,
                size: 28,
              ),
            ),
            const SizedBox(height: PayaboSpacing.lg),
            Text(
              'No videos yet',
              style: textTheme.titleLarge?.copyWith(
                fontWeight: FontWeight.w700,
                color: c.textPrimary,
              ),
            ),
            const SizedBox(height: PayaboSpacing.sm),
            Text(
              'We\'re working on adding guides for this category. Check back soon!',
              textAlign: TextAlign.center,
              style: textTheme.bodyMedium?.copyWith(
                color: c.textSecondary,
                height: 1.45,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

// ── News card widget ─────────────────────────────────────────

class _NewsCard extends StatelessWidget {
  const _NewsCard({required this.news});

  final CommunityNewsItem news;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final textTheme = Theme.of(context).textTheme;

    return GestureDetector(
      onTap: () {
        ScaffoldMessenger.of(context)
          ..hideCurrentSnackBar()
          ..showSnackBar(
            const SnackBar(content: Text('News details coming soon.')),
          );
      },
      child: Container(
      width: 280,
      decoration: BoxDecoration(
        color: c.surfaceBase,
        borderRadius: PayaboRadii.radiusLg,
        border: Border.all(color: c.borderDefault),
        boxShadow: c.isDark ? PayaboShadows.soft : PayaboShadows.medium,
      ),
      clipBehavior: Clip.antiAlias,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          // Image area with gradient overlay
          SizedBox(
            height: 100,
            width: double.infinity,
            child: Stack(
              fit: StackFit.expand,
              children: <Widget>[
                Image.network(
                  news.imageUrl,
                  fit: BoxFit.cover,
                  loadingBuilder: _thumbnailLoadingBuilder,
                  errorBuilder: (_, __, ___) => _ThumbnailPlaceholder(
                    icon: Icons.campaign_outlined,
                    color: c.primary,
                  ),
                ),
                // Gradient scrim
                Positioned.fill(
                  child: DecoratedBox(
                    decoration: BoxDecoration(
                      gradient: LinearGradient(
                        begin: Alignment.topCenter,
                        end: Alignment.bottomCenter,
                        colors: <Color>[
                          Colors.transparent,
                          Colors.black.withValues(alpha: 0.5),
                        ],
                      ),
                    ),
                  ),
                ),
                // Tag pill
                if (news.tag != null)
                  Positioned(
                    top: PayaboSpacing.sm,
                    left: PayaboSpacing.sm,
                    child: Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: PayaboSpacing.sm,
                        vertical: PayaboSpacing.xxs,
                      ),
                      decoration: BoxDecoration(
                        color: c.primary,
                        borderRadius: PayaboRadii.radiusPill,
                      ),
                      child: Text(
                        news.tag!,
                        style: textTheme.bodySmall?.copyWith(
                          color: Colors.white,
                          fontSize: 10,
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                    ),
                  ),
              ],
            ),
          ),
          // Text area
          Expanded(
            child: Padding(
              padding: const EdgeInsets.all(PayaboSpacing.md),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    news.title,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: textTheme.titleSmall?.copyWith(
                      fontWeight: FontWeight.w700,
                      color: c.textPrimary,
                    ),
                  ),
                  const SizedBox(height: PayaboSpacing.xxs),
                  Expanded(
                    child: Text(
                      news.summary,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: textTheme.bodySmall?.copyWith(
                        color: c.textSecondary,
                        height: 1.3,
                      ),
                    ),
                  ),
                  Text(
                    news.date,
                    style: textTheme.bodySmall?.copyWith(
                      color: c.textMuted,
                      fontSize: 10,
                    ),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    ),
    );
  }
}

// ── Category chip widget ─────────────────────────────────────

class _CategoryChip extends StatelessWidget {
  const _CategoryChip({
    required this.category,
    required this.isSelected,
    required this.onTap,
  });

  final CommunityVideoCategory category;
  final bool isSelected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final textTheme = Theme.of(context).textTheme;

    return GestureDetector(
      onTap: onTap,
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 200),
        curve: Curves.easeOut,
        padding: const EdgeInsets.symmetric(
          horizontal: PayaboSpacing.lg,
        ),
        decoration: BoxDecoration(
          color: isSelected ? c.primary : c.surfaceBase,
          borderRadius: PayaboRadii.radiusPill,
          border: Border.all(
            color: isSelected ? c.primary : c.borderDefault,
          ),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Icon(
              IconData(
                category.iconCodePoint,
                fontFamily: category.iconFontFamily,
              ),
              size: 16,
              color: isSelected ? Colors.white : c.textSecondary,
            ),
            const SizedBox(width: PayaboSpacing.xs),
            Text(
              category.label,
              style: textTheme.bodySmall?.copyWith(
                color: isSelected ? Colors.white : c.textPrimary,
                fontWeight: isSelected ? FontWeight.w600 : FontWeight.w500,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

// ── Video card widget ────────────────────────────────────────

class _VideoCard extends StatelessWidget {
  const _VideoCard({
    required this.video,
    required this.onTap,
    required this.categories,
  });

  final CommunityVideo video;
  final VoidCallback onTap;
  final List<CommunityVideoCategory> categories;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final textTheme = Theme.of(context).textTheme;

    return GestureDetector(
      onTap: onTap,
      child: Container(
        decoration: BoxDecoration(
          color: c.surfaceBase,
          borderRadius: PayaboRadii.radiusLg,
          border: Border.all(color: c.borderDefault),
          boxShadow: c.isDark ? PayaboShadows.soft : PayaboShadows.medium,
        ),
        clipBehavior: Clip.antiAlias,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            // Thumbnail with play overlay
            SizedBox(
              height: 180,
              width: double.infinity,
              child: Stack(
                fit: StackFit.expand,
                children: <Widget>[
                  Image.network(
                    video.thumbnailUrl,
                    fit: BoxFit.cover,
                    loadingBuilder: _thumbnailLoadingBuilder,
                    errorBuilder: (_, __, ___) => _ThumbnailPlaceholder(
                      icon: Icons.play_circle_outline,
                      color: c.textMuted,
                    ),
                  ),
                  // Dark scrim
                  Positioned.fill(
                    child: DecoratedBox(
                      decoration: BoxDecoration(
                        color: Colors.black.withValues(alpha: 0.15),
                      ),
                    ),
                  ),
                  // Play button
                  Center(
                    child: Container(
                      width: 56,
                      height: 56,
                      decoration: BoxDecoration(
                        color: c.primary.withValues(alpha: 0.9),
                        shape: BoxShape.circle,
                      ),
                      child: const Icon(
                        Icons.play_arrow_rounded,
                        color: Colors.white,
                        size: 32,
                      ),
                    ),
                  ),
                  // Duration badge
                  Positioned(
                    bottom: PayaboSpacing.sm,
                    right: PayaboSpacing.sm,
                    child: Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: PayaboSpacing.sm,
                        vertical: PayaboSpacing.xxs,
                      ),
                      decoration: BoxDecoration(
                        color: Colors.black.withValues(alpha: 0.7),
                        borderRadius: PayaboRadii.radiusSm,
                      ),
                      child: Text(
                        video.duration,
                        style: textTheme.bodySmall?.copyWith(
                          color: Colors.white,
                          fontSize: 11,
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                    ),
                  ),
                ],
              ),
            ),
            // Info area
            Padding(
              padding: const EdgeInsets.all(PayaboSpacing.lg),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    video.title,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: textTheme.titleSmall?.copyWith(
                      fontWeight: FontWeight.w700,
                      color: c.textPrimary,
                    ),
                  ),
                  const SizedBox(height: PayaboSpacing.xs),
                  Text(
                    video.description,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: textTheme.bodySmall?.copyWith(
                      color: c.textSecondary,
                      height: 1.3,
                    ),
                  ),
                  const SizedBox(height: PayaboSpacing.sm),
                  Row(
                    children: <Widget>[
                      if (video.author != null) ...<Widget>[
                        Icon(
                          Icons.person_outline,
                          size: 14,
                          color: c.textMuted,
                        ),
                        const SizedBox(width: PayaboSpacing.xxs),
                        Text(
                          video.author!,
                          style: textTheme.bodySmall?.copyWith(
                            color: c.textMuted,
                            fontSize: 11,
                          ),
                        ),
                        const SizedBox(width: PayaboSpacing.md),
                      ],
                      Container(
                        padding: const EdgeInsets.symmetric(
                          horizontal: PayaboSpacing.sm,
                          vertical: PayaboSpacing.xxs,
                        ),
                        decoration: BoxDecoration(
                          color: c.primary.withValues(alpha: 0.1),
                          borderRadius: PayaboRadii.radiusPill,
                        ),
                        child: Text(
                          _categoryLabel(video.category),
                          style: textTheme.bodySmall?.copyWith(
                            color: c.primary,
                            fontSize: 10,
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  String _categoryLabel(String categoryId) {
    final match =
        categories.where((cat) => cat.id == categoryId);
    return match.isNotEmpty ? match.first.label : categoryId;
  }
}

// ── Thumbnail loading / error helpers ────────────────────────

/// Standard loading builder for network thumbnails.
/// Shows a subtle pulsing container while the image loads.
Widget _thumbnailLoadingBuilder(
  BuildContext context,
  Widget child,
  ImageChunkEvent? loadingProgress,
) {
  if (loadingProgress == null) return child;
  final c = context.colors;
  return Container(
    color: c.surfaceMuted,
    child: Center(
      child: SizedBox(
        width: 24,
        height: 24,
        child: CircularProgressIndicator(
          strokeWidth: 2.2,
          value: loadingProgress.expectedTotalBytes != null
              ? loadingProgress.cumulativeBytesLoaded /
                  loadingProgress.expectedTotalBytes!
              : null,
          color: c.primary,
        ),
      ),
    ),
  );
}

/// Placeholder shown when a thumbnail fails to load.
class _ThumbnailPlaceholder extends StatelessWidget {
  const _ThumbnailPlaceholder({
    required this.icon,
    required this.color,
  });

  final IconData icon;
  final Color color;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    return Container(
      color: c.surfaceMuted,
      child: Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Icon(icon, color: color, size: 36),
            const SizedBox(height: PayaboSpacing.xs),
            Text(
              'Image unavailable',
              style: Theme.of(context).textTheme.bodySmall?.copyWith(
                    color: c.textMuted,
                    fontSize: 10,
                  ),
            ),
          ],
        ),
      ),
    );
  }
}
