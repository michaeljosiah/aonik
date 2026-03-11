import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../features/profile/presentation/profile_state.dart';
import '../theme/payabo_colors.dart';
import '../theme/payabo_spacing.dart';
import 'payabo_profile_avatar.dart';

class PayaboAppHeader extends ConsumerWidget {
  const PayaboAppHeader({
    super.key,
    this.title,
    this.subtitle,
    this.bottom,
    this.padding = const EdgeInsets.fromLTRB(
      PayaboSpacing.xl,
      PayaboSpacing.md,
      PayaboSpacing.xl,
      PayaboSpacing.lg,
    ),
    this.titleSpacing = PayaboSpacing.lg,
    this.bottomSpacing = PayaboSpacing.lg,
    this.titleStyle,
    this.subtitleStyle,
    this.onProfileTap,
    this.onNotificationsTap,
  });

  final String? title;
  final String? subtitle;
  final Widget? bottom;
  final EdgeInsets padding;
  final double titleSpacing;
  final double bottomSpacing;
  final TextStyle? titleStyle;
  final TextStyle? subtitleStyle;
  final VoidCallback? onProfileTap;
  final VoidCallback? onNotificationsTap;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final profileState = ref.watch(profileControllerProvider);
    final textTheme = Theme.of(context).textTheme;
    final hasTitle = title != null && title!.trim().isNotEmpty;
    final hasSubtitle = subtitle != null && subtitle!.trim().isNotEmpty;

    return Padding(
      padding: padding,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: <Widget>[
              _AppHeaderProfileButton(
                photoUrl: profileState.photoUrl,
                onTap: onProfileTap ?? () => context.go('/profile'),
              ),
              _AppHeaderNotificationButton(
                onTap: onNotificationsTap ??
                    () => _showNotificationsMessage(context),
              ),
            ],
          ),
          if (hasTitle) ...<Widget>[
            SizedBox(height: titleSpacing),
            Text(
              title!,
              style: titleStyle ??
                  textTheme.headlineMedium?.copyWith(
                    fontSize: 40,
                    fontWeight: FontWeight.w700,
                    color: PayaboColors.headerTitle,
                  ),
            ),
          ],
          if (hasSubtitle) ...<Widget>[
            const SizedBox(height: PayaboSpacing.sm),
            Text(
              subtitle!,
              style: subtitleStyle ??
                  textTheme.titleSmall?.copyWith(
                    color: PayaboColors.headerSubtitle,
                    fontWeight: FontWeight.w500,
                  ),
            ),
          ],
          if (bottom != null) ...<Widget>[
            SizedBox(height: hasTitle || hasSubtitle ? bottomSpacing : 0),
            bottom!,
          ],
        ],
      ),
    );
  }

  void _showNotificationsMessage(BuildContext context) {
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(
        const SnackBar(content: Text('Notifications are coming soon.')),
      );
  }
}

class _AppHeaderProfileButton extends StatelessWidget {
  const _AppHeaderProfileButton({
    required this.photoUrl,
    required this.onTap,
  });

  final String? photoUrl;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: PayaboColors.headerIconSurface,
      shape: const CircleBorder(),
      child: Container(
        decoration: BoxDecoration(
          shape: BoxShape.circle,
          border: Border.all(color: PayaboColors.headerIconBorder, width: 1.2),
        ),
        child: InkWell(
          onTap: onTap,
          customBorder: const CircleBorder(),
          child: Padding(
            padding: const EdgeInsets.all(1.5),
            child: PayaboProfileAvatar(
              photoUrl: photoUrl,
              size: 42,
              backgroundColor: PayaboColors.headerIconSurfaceAccent,
              placeholderIcon: Icons.person_outline_rounded,
              placeholderIconSize: 20,
            ),
          ),
        ),
      ),
    );
  }
}

class _AppHeaderNotificationButton extends StatelessWidget {
  const _AppHeaderNotificationButton({required this.onTap});

  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        customBorder: const CircleBorder(),
        child: Ink(
          width: 44,
          height: 44,
          decoration: BoxDecoration(
            color: PayaboColors.headerIconSurface,
            shape: BoxShape.circle,
            border: Border.all(color: PayaboColors.headerIconBorder),
          ),
          child: Stack(
            clipBehavior: Clip.none,
            children: <Widget>[
              const Center(
                child: Icon(
                  Icons.notifications_none_rounded,
                  color: PayaboColors.headerIconAccent,
                  size: 22,
                ),
              ),
              Positioned(
                right: 10,
                top: 9,
                child: Container(
                  width: 8,
                  height: 8,
                  decoration: const BoxDecoration(
                    color: PayaboColors.headerNotificationDot,
                    shape: BoxShape.circle,
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
