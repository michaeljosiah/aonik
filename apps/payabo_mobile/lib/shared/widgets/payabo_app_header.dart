import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../features/profile/presentation/profile_state.dart';
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
                    color: const Color(0xFF4D3120),
                  ),
            ),
          ],
          if (hasSubtitle) ...<Widget>[
            const SizedBox(height: PayaboSpacing.sm),
            Text(
              subtitle!,
              style: subtitleStyle ??
                  textTheme.titleSmall?.copyWith(
                    color: const Color(0xFF77594A),
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
      color: const Color(0xFFFFFBF8),
      shape: const CircleBorder(),
      child: Container(
        decoration: BoxDecoration(
          shape: BoxShape.circle,
          border: Border.all(color: const Color(0xFFDCCDB7), width: 1.2),
        ),
        child: InkWell(
          onTap: onTap,
          customBorder: const CircleBorder(),
          child: Padding(
            padding: const EdgeInsets.all(1.5),
            child: PayaboProfileAvatar(
              photoUrl: photoUrl,
              size: 42,
              backgroundColor: const Color(0xFFF4ECDE),
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
            color: const Color(0xFFFFFCF6),
            shape: BoxShape.circle,
            border: Border.all(color: const Color(0xFFDCCDB7)),
          ),
          child: Stack(
            clipBehavior: Clip.none,
            children: <Widget>[
              const Center(
                child: Icon(
                  Icons.notifications_none_rounded,
                  color: Color(0xFF9B7A43),
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
                    color: Color(0xFFD7A14E),
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
