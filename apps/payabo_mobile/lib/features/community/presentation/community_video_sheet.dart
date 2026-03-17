import 'package:flutter/material.dart';
import 'package:youtube_player_iframe/youtube_player_iframe.dart';

import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_radii.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../community_data.dart';

/// A modal bottom sheet that plays a YouTube video and shows its details.
///
/// Call [showCommunityVideoSheet] to display it.
Future<void> showCommunityVideoSheet({
  required BuildContext context,
  required CommunityVideo video,
}) {
  return showModalBottomSheet<void>(
    context: context,
    isScrollControlled: true,
    isDismissible: true,
    enableDrag: true,
    backgroundColor: Colors.transparent,
    builder: (_) => _CommunityVideoSheet(video: video),
  );
}

class _CommunityVideoSheet extends StatefulWidget {
  const _CommunityVideoSheet({required this.video});

  final CommunityVideo video;

  @override
  State<_CommunityVideoSheet> createState() => _CommunityVideoSheetState();
}

class _CommunityVideoSheetState extends State<_CommunityVideoSheet>
    with SingleTickerProviderStateMixin {
  late final YoutubePlayerController _controller;
  bool _playerReady = false;
  bool _playerError = false;

  // ── Details entrance animation ────────────────────────────
  late final AnimationController _detailsAnimController;
  late final Animation<double> _detailsFade;
  late final Animation<Offset> _detailsSlide;

  @override
  void initState() {
    super.initState();

    _detailsAnimController = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 500),
    );
    final curve = CurvedAnimation(
      parent: _detailsAnimController,
      curve: Curves.easeOutCubic,
    );
    _detailsFade = curve;
    _detailsSlide = Tween<Offset>(
      begin: const Offset(0, 0.15),
      end: Offset.zero,
    ).animate(curve);

    _controller = YoutubePlayerController.fromVideoId(
      videoId: widget.video.youtubeVideoId,
      autoPlay: false,
      params: const YoutubePlayerParams(
        showControls: true,
        showFullscreenButton: true,
        mute: false,
      ),
    );

    // Listen for player readiness and errors.
    _controller.listen(
      (event) {
        if (!mounted) return;
        if (event.playerState == PlayerState.cued ||
            event.playerState == PlayerState.playing ||
            event.playerState == PlayerState.paused ||
            event.playerState == PlayerState.buffering) {
          if (!_playerReady) {
            setState(() => _playerReady = true);
            _detailsAnimController.forward();
          }
        }
      },
      onError: (_) {
        if (!mounted) return;
        setState(() => _playerError = true);
        _detailsAnimController.forward();
      },
    );

    // Kick off details animation after a short delay even if
    // the player hasn't fired an event yet (covers slow loads).
    Future<void>.delayed(const Duration(milliseconds: 600), () {
      if (mounted && !_detailsAnimController.isCompleted) {
        _detailsAnimController.forward();
      }
    });
  }

  @override
  void dispose() {
    _detailsAnimController.dispose();
    _controller.close();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final textTheme = Theme.of(context).textTheme;
    final bottomInset = MediaQuery.viewInsetsOf(context).bottom;

    return Padding(
      padding: EdgeInsets.only(bottom: bottomInset),
      child: DraggableScrollableSheet(
        initialChildSize: 0.75,
        minChildSize: 0.5,
        maxChildSize: 0.95,
        expand: false,
        builder: (context, scrollController) {
          return Container(
            decoration: BoxDecoration(
              color: c.surfaceBase,
              borderRadius: PayaboRadii.sheetTop,
              border: Border.all(color: c.borderStrong),
            ),
            child: Column(
              children: <Widget>[
                // ── Drag handle ──────────────────────────
                Padding(
                  padding: const EdgeInsets.only(top: PayaboSpacing.sm),
                  child: Center(
                    child: Container(
                      width: 36,
                      height: 4,
                      decoration: BoxDecoration(
                        color: c.borderDefault,
                        borderRadius: PayaboRadii.radiusPill,
                      ),
                    ),
                  ),
                ),

                // ── Close button row ─────────────────────
                Padding(
                  padding: const EdgeInsets.symmetric(
                    horizontal: PayaboSpacing.sm,
                  ),
                  child: Row(
                    mainAxisAlignment: MainAxisAlignment.end,
                    children: <Widget>[
                      IconButton(
                        onPressed: () => Navigator.of(context).pop(),
                        icon: Icon(Icons.close, color: c.primary),
                      ),
                    ],
                  ),
                ),

                // ── YouTube player / error state ─────────
                Padding(
                  padding: const EdgeInsets.symmetric(
                    horizontal: PayaboSpacing.xl,
                  ),
                  child: ClipRRect(
                    borderRadius: PayaboRadii.radiusLg,
                    child: AspectRatio(
                      aspectRatio: 16 / 9,
                      child: _playerError
                          ? _PlayerErrorState(
                              onRetry: _retryPlayer,
                            )
                          : YoutubePlayer(
                              controller: _controller,
                            ),
                    ),
                  ),
                ),

                const SizedBox(height: PayaboSpacing.lg),

                // ── Scrollable details (slides in) ───────
                Expanded(
                  child: SlideTransition(
                    position: _detailsSlide,
                    child: FadeTransition(
                      opacity: _detailsFade,
                      child: ListView(
                        controller: scrollController,
                        padding: const EdgeInsets.symmetric(
                          horizontal: PayaboSpacing.xl,
                        ),
                        children: <Widget>[
                          Text(
                            widget.video.title,
                            style: textTheme.titleLarge?.copyWith(
                              fontWeight: FontWeight.w700,
                              color: c.textPrimary,
                            ),
                          ),
                          const SizedBox(height: PayaboSpacing.sm),
                          Row(
                            children: <Widget>[
                              if (widget.video.author != null) ...<Widget>[
                                Icon(
                                  Icons.person_outline,
                                  size: 16,
                                  color: c.textSecondary,
                                ),
                                const SizedBox(width: PayaboSpacing.xs),
                                Text(
                                  widget.video.author!,
                                  style: textTheme.bodySmall?.copyWith(
                                    color: c.textSecondary,
                                  ),
                                ),
                                const SizedBox(width: PayaboSpacing.lg),
                              ],
                              Icon(
                                Icons.schedule,
                                size: 16,
                                color: c.textSecondary,
                              ),
                              const SizedBox(width: PayaboSpacing.xs),
                              Text(
                                widget.video.duration,
                                style: textTheme.bodySmall?.copyWith(
                                  color: c.textSecondary,
                                ),
                              ),
                            ],
                          ),
                          const SizedBox(height: PayaboSpacing.lg),
                          Text(
                            widget.video.description,
                            style: textTheme.bodyMedium?.copyWith(
                              color: c.textSecondary,
                              height: 1.5,
                            ),
                          ),
                          const SizedBox(height: PayaboSpacing.x2),

                          // ── Action row ───────────────────
                          Row(
                            mainAxisAlignment: MainAxisAlignment.spaceEvenly,
                            children: <Widget>[
                              _SheetAction(
                                icon: Icons.thumb_up_outlined,
                                label: 'Like',
                                onTap: () => _showFeedback(
                                  context,
                                  'Liked! Thanks for your feedback.',
                                ),
                              ),
                              _SheetAction(
                                icon: Icons.share_outlined,
                                label: 'Share',
                                onTap: () => _showFeedback(
                                  context,
                                  'Share link copied.',
                                ),
                              ),
                              _SheetAction(
                                icon: Icons.bookmark_border,
                                label: 'Save',
                                onTap: () => _showFeedback(
                                  context,
                                  'Video saved to your collection.',
                                ),
                              ),
                            ],
                          ),
                          const SizedBox(height: PayaboSpacing.x4),
                        ],
                      ),
                    ),
                  ),
                ),
              ],
            ),
          );
        },
      ),
    );
  }

  void _retryPlayer() {
    setState(() {
      _playerError = false;
      _playerReady = false;
    });
    _controller.loadVideoById(videoId: widget.video.youtubeVideoId);
  }

  static void _showFeedback(BuildContext context, String message) {
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(
        SnackBar(content: Text(message)),
      );
  }
}

// ── Player error state ───────────────────────────────────────

class _PlayerErrorState extends StatelessWidget {
  const _PlayerErrorState({required this.onRetry});

  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final textTheme = Theme.of(context).textTheme;

    return Container(
      color: c.surfaceMuted,
      child: Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Container(
              width: 48,
              height: 48,
              decoration: BoxDecoration(
                color: c.primary.withValues(alpha: 0.12),
                shape: BoxShape.circle,
              ),
              child: Icon(
                Icons.error_outline_rounded,
                color: c.primary,
                size: 24,
              ),
            ),
            const SizedBox(height: PayaboSpacing.md),
            Text(
              'Unable to load video',
              style: textTheme.titleSmall?.copyWith(
                fontWeight: FontWeight.w600,
                color: c.textPrimary,
              ),
            ),
            const SizedBox(height: PayaboSpacing.xs),
            Text(
              'Check your connection and try again.',
              style: textTheme.bodySmall?.copyWith(
                color: c.textSecondary,
              ),
            ),
            const SizedBox(height: PayaboSpacing.lg),
            GestureDetector(
              onTap: onRetry,
              child: Container(
                padding: const EdgeInsets.symmetric(
                  horizontal: PayaboSpacing.xl,
                  vertical: PayaboSpacing.sm,
                ),
                decoration: BoxDecoration(
                  color: c.primary,
                  borderRadius: PayaboRadii.radiusPill,
                ),
                child: Text(
                  'Retry',
                  style: textTheme.bodySmall?.copyWith(
                    color: Colors.white,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

// ── Sheet action button ──────────────────────────────────────

class _SheetAction extends StatelessWidget {
  const _SheetAction({
    required this.icon,
    required this.label,
    required this.onTap,
  });

  final IconData icon;
  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return InkWell(
      onTap: onTap,
      borderRadius: PayaboRadii.radiusLg,
      child: Padding(
        padding: const EdgeInsets.symmetric(
          horizontal: PayaboSpacing.lg,
          vertical: PayaboSpacing.sm,
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Icon(icon, color: c.primary, size: 24),
            const SizedBox(height: PayaboSpacing.xs),
            Text(
              label,
              style: Theme.of(context).textTheme.bodySmall?.copyWith(
                    color: c.textSecondary,
                  ),
            ),
          ],
        ),
      ),
    );
  }
}
