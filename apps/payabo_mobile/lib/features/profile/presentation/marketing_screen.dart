import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../shared/theme/payabo_colors.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_card.dart';
import 'profile_scaffold.dart';
import 'profile_state.dart';

class MarketingScreen extends ConsumerWidget {
  const MarketingScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(profileControllerProvider);

    void showError(String message) {
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(SnackBar(content: Text(message)));
    }

    Future<void> toggleMarketing({bool? news, bool? offers, bool? surveys}) async {
      try {
        await ref
            .read(profileControllerProvider.notifier)
            .setMarketingToggle(news: news, offers: offers, surveys: surveys);
      } catch (_) {
        showError('Unable to update marketing preferences right now.');
      }
    }

    return ProfileScaffold(
      title: 'Marketing',
      backRoute: '/profile',
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Text(
            'Choose what marketing emails you want to receive from us.',
            style: Theme.of(context).textTheme.bodySmall,
          ),
          const SizedBox(height: PayaboSpacing.md),
          InkWell(
            onTap: () => context.go('/profile/marketing/email'),
            child: PayaboCard(
              child: Row(
                children: <Widget>[
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: <Widget>[
                        Text('Email for marketing',
                            style: Theme.of(context).textTheme.titleSmall),
                        Text(state.marketingEmail,
                            style: Theme.of(context).textTheme.bodySmall),
                      ],
                    ),
                  ),
                  const Icon(Icons.chevron_right, color: PayaboColors.muted),
                ],
              ),
            ),
          ),
          const SizedBox(height: PayaboSpacing.sm),
          _ToggleCard(
            label: 'News',
            subtitle: 'Updates and news services',
            value: state.marketingNews,
            onChanged: (v) => toggleMarketing(news: v),
          ),
          _ToggleCard(
            label: 'Offers',
            subtitle: 'Offers and promotional campaigns',
            value: state.marketingOffers,
            onChanged: (v) => toggleMarketing(offers: v),
          ),
          _ToggleCard(
            label: 'Surveys',
            subtitle: 'To help us improve our services',
            value: state.marketingSurveys,
            onChanged: (v) => toggleMarketing(surveys: v),
          ),
        ],
      ),
    );
  }
}

class _ToggleCard extends StatelessWidget {
  const _ToggleCard({
    required this.label,
    required this.subtitle,
    required this.value,
    required this.onChanged,
  });

  final String label;
  final String subtitle;
  final bool value;
  final ValueChanged<bool> onChanged;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: PayaboSpacing.sm),
      child: PayaboCard(
        child: Row(
          children: <Widget>[
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(label, style: Theme.of(context).textTheme.titleSmall),
                  const SizedBox(height: 2),
                  Text(subtitle, style: Theme.of(context).textTheme.bodySmall),
                ],
              ),
            ),
            SizedBox(
              width: 60,
              height: 30,
              child: FittedBox(
                fit: BoxFit.fill,
                child: Switch.adaptive(
                  value: value,
                  onChanged: onChanged,
                  activeThumbColor: PayaboColors.white,
                  activeTrackColor: PayaboColors.success,
                  inactiveThumbColor: PayaboColors.white,
                  inactiveTrackColor: PayaboColors.background,
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
