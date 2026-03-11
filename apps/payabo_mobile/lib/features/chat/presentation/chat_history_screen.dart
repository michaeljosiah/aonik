import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../../shared/theme/payabo_colors.dart';
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

class ChatHistoryScreen extends StatefulWidget {
  const ChatHistoryScreen({
    super.key,
    this.selectedConversationId,
  });

  final String? selectedConversationId;

  @override
  State<ChatHistoryScreen> createState() => _ChatHistoryScreenState();
}

class _ChatHistoryScreenState extends State<ChatHistoryScreen> {
  final TextEditingController _searchController = TextEditingController();

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final query = _searchController.text.trim().toLowerCase();
    final items = _historyEntries.where((entry) {
      return entry.title.toLowerCase().contains(query) ||
          entry.dateLabel.toLowerCase().contains(query);
    }).toList(growable: false);

    return Scaffold(
      backgroundColor: const Color(0xFFF7F5F3),
      body: SafeArea(
        child: Column(
          children: <Widget>[
            Padding(
              padding: const EdgeInsets.fromLTRB(
                PayaboSpacing.xl,
                PayaboSpacing.lg,
                PayaboSpacing.xl,
                PayaboSpacing.md,
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
                  PayaboSpacing.md,
                  PayaboSpacing.xl,
                  PayaboSpacing.xl,
                ),
                children: <Widget>[
                  Text(
                    'Conversation history',
                    style: Theme.of(context).textTheme.titleLarge?.copyWith(
                          color: PayaboColors.chatTextPrimary,
                          fontWeight: FontWeight.w700,
                        ),
                  ),
                  const SizedBox(height: PayaboSpacing.md),
                  if (items.isEmpty)
                    Container(
                      padding: const EdgeInsets.all(PayaboSpacing.lg),
                      decoration: BoxDecoration(
                        color: PayaboColors.white,
                        borderRadius: BorderRadius.circular(20),
                        border: Border.all(color: const Color(0xFFE4E0DC)),
                      ),
                      child: Text(
                        'No conversations match your search.',
                        style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                              color: PayaboColors.chatTextSecondary,
                            ),
                      ),
                    )
                  else
                    Container(
                      decoration: BoxDecoration(
                        color: PayaboColors.white,
                        borderRadius: BorderRadius.circular(20),
                        border: Border.all(color: const Color(0xFFE4E0DC)),
                      ),
                      child: Column(
                        children: items
                            .asMap()
                            .entries
                            .map(
                              (entry) => _HistoryListItem(
                                item: entry.value,
                                isSelected: entry.value.id ==
                                    widget.selectedConversationId,
                                showDivider: entry.key != items.length - 1,
                                onTap: () => context.pop(entry.value.id),
                              ),
                            )
                            .toList(growable: false),
                      ),
                    ),
                ],
              ),
            ),
          ],
        ),
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
    return Container(
      height: 50,
      decoration: BoxDecoration(
        color: const Color(0xFFF0ECE8),
        borderRadius: BorderRadius.circular(28),
      ),
      alignment: Alignment.center,
      child: TextField(
        controller: controller,
        onChanged: onChanged,
        decoration: InputDecoration(
          hintText: 'Search conversations',
          hintStyle: Theme.of(context).textTheme.titleMedium?.copyWith(
                color: const Color(0xFF8B7F77),
                fontWeight: FontWeight.w500,
              ),
          prefixIcon:
              const Icon(Icons.search_rounded, color: Color(0xFF8B7F77)),
          border: InputBorder.none,
          enabledBorder: InputBorder.none,
          focusedBorder: InputBorder.none,
          contentPadding: const EdgeInsets.symmetric(
            horizontal: PayaboSpacing.sm,
            vertical: PayaboSpacing.md,
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
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(20),
      child: SizedBox(
        width: 40,
        height: 40,
        child: Icon(icon, color: const Color(0xFF4B2B1F), size: 24),
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
        padding: const EdgeInsets.fromLTRB(
          PayaboSpacing.lg,
          PayaboSpacing.lg,
          PayaboSpacing.lg,
          PayaboSpacing.md,
        ),
        decoration: BoxDecoration(
          color: isSelected ? const Color(0xFFF7F0E8) : Colors.transparent,
          border: showDivider
              ? const Border(
                  bottom: BorderSide(color: Color(0xFFE8E2DB)),
                )
              : null,
        ),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    item.dateLabel,
                    style: Theme.of(context).textTheme.titleSmall?.copyWith(
                          color: const Color(0xFF7D736C),
                          fontWeight: FontWeight.w700,
                        ),
                  ),
                  const SizedBox(height: PayaboSpacing.xs),
                  Text(
                    item.title,
                    style: Theme.of(context).textTheme.headlineMedium?.copyWith(
                          fontSize: 22,
                          color: const Color(0xFF321E17),
                          fontWeight: FontWeight.w500,
                        ),
                  ),
                ],
              ),
            ),
            const SizedBox(width: PayaboSpacing.sm),
            const Padding(
              padding: EdgeInsets.only(top: 6),
              child: Icon(
                Icons.more_vert_rounded,
                color: Color(0xFF4B2B1F),
              ),
            ),
          ],
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
