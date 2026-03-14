import 'package:flutter/material.dart';

import '../theme/payabo_color_resolver.dart';
import '../theme/payabo_shadows.dart';

class PayaboProfileAvatar extends StatelessWidget {
  const PayaboProfileAvatar({
    super.key,
    this.photoUrl,
    this.size = 100,
    this.backgroundColor,
    this.placeholderIcon = Icons.camera_alt_outlined,
    this.placeholderIconSize = 36,
    this.showShadow = false,
  });

  final String? photoUrl;
  final double size;
  final Color? backgroundColor;
  final IconData placeholderIcon;
  final double placeholderIconSize;
  final bool showShadow;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final resolvedPhotoUrl = photoUrl?.trim();
    final hasPhoto = resolvedPhotoUrl != null && resolvedPhotoUrl.isNotEmpty;

    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        color: backgroundColor ?? c.background,
        boxShadow: showShadow ? PayaboShadows.soft : null,
      ),
      clipBehavior: Clip.antiAlias,
      child: hasPhoto
          ? Image.network(
              resolvedPhotoUrl,
              fit: BoxFit.cover,
              errorBuilder: (_, __, ___) => _placeholder(context),
            )
          : _placeholder(context),
    );
  }

  Widget _placeholder(BuildContext context) {
    final c = context.colors;

    return Center(
      child: Icon(
        placeholderIcon,
        size: placeholderIconSize,
        color: c.muted,
      ),
    );
  }
}
