import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../app/demo/demo_data_mode.dart';
import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_spacing.dart';

const List<_ChatHistoryEntry> _historyEntries = <_ChatHistoryEntry>[
  _ChatHistoryEntry(
    id: 'sunday-reset',
    dateLabel: 'Today',
    title: 'Sunday reset',
  ),
  _ChatHistoryEntry(
    id: 'bill-rescue',
    dateLabel: '1 day ago',
    title: 'Current account balance inquiry',
  ),
  _ChatHistoryEntry(
    id: 'goal-sprint',
    dateLabel: '1 day ago',
    title: 'Track spending to see where money goes',
  ),
];

Color _historyBaseColor(BuildContext context) {
  final c = context.colors;

  return c.isDark ? const Color(0xFF070505) : const Color(0xFF0A0706);
}

LinearGradient _historyScreenGradient() {
  return const LinearGradient(
    colors: <Color>[
      Color(0xFF34231B),
      Color(0xFF1A120E),
      Color(0xFF070505),
    ],
    stops: <double>[0, 0.42, 1],
    begin: Alignment.topCenter,
    end: Alignment.bottomCenter,
  );
}

Color _historyTextColor(BuildContext context) {
  return Colors.white.withValues(alpha: 0.92);
}

Color _historyMutedTextColor(BuildContext context) {
  return Colors.white.withValues(alpha: 0.62);
}

LinearGradient _historySelectedGradient(BuildContext context) {
  final c = context.colors;

  return LinearGradient(
    colors: c.isDark
        ? const <Color>[
            Color(0x1EF4A027),
            Color(0x0FF4A027),
          ]
        : const <Color>[
            Color(0x22F4A027),
            Color(0x12F4A027),
          ],
    begin: Alignment.centerLeft,
    end: Alignment.centerRight,
  );
}

class ChatHistoryScreen extends ConsumerStatefulWidget {
  const ChatHistoryScreen({
    super.key,
    this.selectedConversationId,
  });

  final String? selectedConversationId;

  @override
  ConsumerState<ChatHistoryScreen> createState() => _ChatHistoryScreenState();
}

class _ChatHistoryScreenState extends ConsumerState<ChatHistoryScreen> {
  final TextEditingController _searchController = TextEditingController();

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final bool isFreshDemo =
        ref.watch(demoDataModeProvider) == DemoDataMode.fresh;
    final String query = _searchController.text.trim().toLowerCase();
    final List<_ChatHistoryEntry> sourceItems =
        isFreshDemo ? const <_ChatHistoryEntry>[] : _historyEntries;
    final List<_ChatHistoryEntry> items = sourceItems.where((entry) {
      return entry.title.toLowerCase().contains(query) ||
          entry.dateLabel.toLowerCase().contains(query);
    }).toList(growable: false);

    return Scaffold(
      backgroundColor: _historyBaseColor(context),
      body: Stack(
        children: <Widget>[
          Positioned.fill(
            child: ColoredBox(color: _historyBaseColor(context)),
          ),
          Positioned.fill(
            child: IgnorePointer(
              child: DecoratedBox(
                decoration: BoxDecoration(gradient: _historyScreenGradient()),
              ),
            ),
          ),
          const Positioned(
            top: -100,
            left: -80,
            child: _HistoryGlowOrb(
              size: 280,
              color: Color(0x2238251B),
            ),
          ),
          const Positioned(
            top: -70,
            right: -80,
            child: _HistoryGlowOrb(
              size: 260,
              color: Color(0x1A462D1C),
            ),
          ),
          SafeArea(
            child: Column(
              children: <Widget>[
                Padding(
                  padding: const EdgeInsets.fromLTRB(
                    PayaboSpacing.xl,
                    PayaboSpacing.md,
                    PayaboSpacing.xl,
                    0,
                  ),
                  child: Row(
                    children: <Widget>[
                      Expanded(
                        child: _SearchField(
                          controller: _searchController,
                          onChanged: (_) => setState(() {}),
                        ),
                      ),
                      const SizedBox(width: PayaboSpacing.sm),
                      _TopIconButton(
                        icon: Icons.notifications_none_rounded,
                        onTap: () => context.push('/notifications'),
                      ),
                      const SizedBox(width: PayaboSpacing.xs),
                      _TopIconButton(
                        icon: Icons.person_outline_rounded,
                        onTap: () => context.push('/profile'),
                      ),
                    ],
                  ),
                ),
                Expanded(
                  child: ListView(
                    padding: const EdgeInsets.fromLTRB(
                      PayaboSpacing.xl,
                      PayaboSpacing.xl,
                      PayaboSpacing.xl,
                      PayaboSpacing.xl,
                    ),
                    children: <Widget>[
                      Text(
                        'Conversation history',
                        style:
                            Theme.of(context).textTheme.headlineSmall?.copyWith(
                                  color: _historyTextColor(context),
                                  fontWeight: FontWeight.w700,
                                  letterSpacing: -0.5,
                                ),
                      ),
                      const SizedBox(height: PayaboSpacing.xs),
                      Text(
                        'Every thread with Simi, ready to pick back up.',
                        style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                              color: _historyMutedTextColor(context),
                              height: 1.45,
                            ),
                      ),
                      const SizedBox(height: PayaboSpacing.xl),
                      if (items.isEmpty)
                        _EmptyHistoryCard(
                          text: isFreshDemo && query.isEmpty
                              ? 'No conversation history yet in this demo state.'
                              : 'No conversations match your search.',
                        )
                      else
                        Column(
                          children: items
                              .asMap()
                              .entries
                              .map(
                                (MapEntry<int, _ChatHistoryEntry> entry) =>
                                    _HistoryListItem(
                                  item: entry.value,
                                  isSelected: entry.value.id ==
                                      widget.selectedConversationId,
                                  showDivider:
                                      entry.key != items.length - 1,
                                  onTap: () =>
                                      context.pop(entry.value.id),
                                ),
                              )
                              .toList(growable: false),
                        ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _SearchField extends StatelessWidget {
  const _SearchField({
    required this.controller,
    required this.onChanged,
  });

  final TextEditingController controller;
  final ValueChanged<String> onChanged;

  @override
  Widget build(BuildContext context) {
    return DecoratedBox(
      decoration: BoxDecoration(
        color: Colors.white.withValues(alpha: 0.06),
        borderRadius: BorderRadius.circular(14),
      ),
      child: SizedBox(
        height: 46,
        child: TextField(
          controller: controller,
          onChanged: onChanged,
          cursorColor: context.colors.primary,
          style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                color: _historyTextColor(context),
              ),
          decoration: InputDecoration(
            hintText: 'Search conversations',
            hintStyle: Theme.of(context).textTheme.bodyMedium?.copyWith(
                  color: _historyMutedTextColor(context),
                ),
            prefixIcon: Icon(
              Icons.search_rounded,
              color: _historyMutedTextColor(context),
              size: 20,
            ),
            border: InputBorder.none,
            enabledBorder: InputBorder.none,
            focusedBorder: InputBorder.none,
            contentPadding: const EdgeInsets.symmetric(
              horizontal: PayaboSpacing.sm,
              vertical: PayaboSpacing.md,
            ),
          ),
        ),
      ),
    );
  }
}

class _TopIconButton extends StatelessWidget {
  const _TopIconButton({
    required this.icon,
    required this.onTap,
  });

  final IconData icon;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        customBorder: const CircleBorder(),
        child: Ink(
          width: 46,
          height: 46,
          decoration: BoxDecoration(
            color: Colors.white.withValues(alpha: 0.06),
            shape: BoxShape.circle,
          ),
          child: Icon(
            icon,
            color: _historyTextColor(context),
            size: 21,
          ),
        ),
      ),
    );
  }
}

class _EmptyHistoryCard extends StatelessWidget {
  const _EmptyHistoryCard({required this.text});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: PayaboSpacing.xl),
      child: Text(
        text,
        style: Theme.of(context).textTheme.bodyMedium?.copyWith(
              color: _historyMutedTextColor(context),
              height: 1.45,
            ),
      ),
    );
  }
}

class _HistoryListItem extends StatelessWidget {
  const _HistoryListItem({
    required this.item,
    required this.isSelected,
    required this.showDivider,
    required this.onTap,
  });

  final _ChatHistoryEntry item;
  final bool isSelected;
  final bool showDivider;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.symmetric(
          horizontal: PayaboSpacing.xs,
          vertical: PayaboSpacing.md,
        ),
        decoration: BoxDecoration(
          gradient: isSelected ? _historySelectedGradient(context) : null,
          border: showDivider
              ? Border(
                  bottom: BorderSide(
                    color: Colors.white.withValues(alpha: 0.06),
                  ),
                )
              : null,
        ),
        child: Row(
          children: <Widget>[
            Container(
              width: 46,
              height: 46,
              decoration: const BoxDecoration(
                color: Color(0xFF1E1611),
                shape: BoxShape.circle,
              ),
              alignment: Alignment.center,
              child: const Icon(
                Icons.chat_bubble_outline_rounded,
                color: Color(0xFFC8A882),
                size: 20,
              ),
            ),
            const SizedBox(width: PayaboSpacing.md),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    item.title,
                    style: Theme.of(context).textTheme.titleSmall?.copyWith(
                          color: _historyTextColor(context),
                        ),
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                  ),
                  const SizedBox(height: PayaboSpacing.xxs),
                  Text(
                    item.dateLabel,
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                          color: _historyMutedTextColor(context),
                        ),
                  ),
                ],
              ),
            ),
            const SizedBox(width: PayaboSpacing.md),
            Icon(
              Icons.chevron_right_rounded,
              color: _historyMutedTextColor(context),
              size: 20,
            ),
          ],
        ),
      ),
    );
  }
}

class _HistoryGlowOrb extends StatelessWidget {
  const _HistoryGlowOrb({
    required this.size,
    required this.color,
  });

  final double size;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return IgnorePointer(
      child: Container(
        width: size,
        height: size,
        decoration: BoxDecoration(
          shape: BoxShape.circle,
          gradient: RadialGradient(
            colors: <Color>[color, Colors.transparent],
          ),
        ),
      ),
    );
  }
}

class _ChatHistoryEntry {
  const _ChatHistoryEntry({
    required this.id,
    required this.dateLabel,
    required this.title,
  });

  final String id;
  final String dateLabel;
  final String title;
}
