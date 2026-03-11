import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../app/demo/demo_data_mode.dart';
import '../../../shared/theme/payabo_colors.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_list_row.dart';
import 'profile_scaffold.dart';
import 'profile_state.dart';

class DemoDataPreferencesScreen extends ConsumerWidget {
  const DemoDataPreferencesScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final demoDataMode = ref.watch(demoDataModeProvider);

    Future<void> selectMode(DemoDataMode mode) async {
      if (demoDataMode == mode) {
        return;
      }

      await ref.read(demoDataModeProvider.notifier).setMode(mode);
      await ref.read(profileDataCoordinatorProvider).reload();

      if (!context.mounted) {
        return;
      }

      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(
          SnackBar(content: Text('${mode.label} selected.')),
        );
    }

    return ProfileScaffold(
      title: 'Demo data',
      backRoute: '/profile',
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Text(
            'Choose which demo experience to use while the app is still in development.',
            style: Theme.of(context).textTheme.bodySmall,
          ),
          const SizedBox(height: PayaboSpacing.md),
          _DemoDataOptionRow(
            title: DemoDataMode.fresh.label,
            subtitle: DemoDataMode.fresh.description,
            selected: demoDataMode == DemoDataMode.fresh,
            icon: Icons.auto_awesome_outlined,
            onTap: () => selectMode(DemoDataMode.fresh),
          ),
          const SizedBox(height: PayaboSpacing.sm),
          _DemoDataOptionRow(
            title: DemoDataMode.populated.label,
            subtitle: DemoDataMode.populated.description,
            selected: demoDataMode == DemoDataMode.populated,
            icon: Icons.inventory_2_outlined,
            onTap: () => selectMode(DemoDataMode.populated),
          ),
          const SizedBox(height: PayaboSpacing.md),
          Text(
            'Applies to supported demo areas such as profile, dashboard, and checkout setup.',
            style: Theme.of(context).textTheme.bodySmall?.copyWith(
                  color: PayaboColors.muted,
                ),
          ),
        ],
      ),
    );
  }
}

class _DemoDataOptionRow extends StatelessWidget {
  const _DemoDataOptionRow({
    required this.title,
    required this.subtitle,
    required this.selected,
    required this.icon,
    required this.onTap,
  });

  final String title;
  final String subtitle;
  final bool selected;
  final IconData icon;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return PayaboListRow(
      title: title,
      subtitle: subtitle,
      leading: Icon(icon, size: 24, color: PayaboColors.muted),
      trailing: Icon(
        selected ? Icons.radio_button_checked : Icons.radio_button_off,
        color: selected ? PayaboColors.primary : PayaboColors.muted,
      ),
      onTap: onTap,
    );
  }
}
