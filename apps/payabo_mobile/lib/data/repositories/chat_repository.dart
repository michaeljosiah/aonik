// ─────────────────────────────────────────────────────────
//  ChatRepository — interface + DTOs
//
//  Surfaces conversation threads, messages, history entries,
//  and canned assistant replies for the chat feature.
// ─────────────────────────────────────────────────────────

// ─────────────────────────────────────────────────────────
//  DTOs
// ─────────────────────────────────────────────────────────

enum ChatSender {
  user,
  assistant,
}

class ChatMessage {
  const ChatMessage({
    required this.sender,
    required this.lines,
    this.planTitle,
    this.planItems = const <String>[],
  });

  final ChatSender sender;
  final List<String> lines;
  final String? planTitle;
  final List<String> planItems;

  bool get hasPlan => planTitle != null && planItems.isNotEmpty;
}

class ChatConversation {
  ChatConversation({
    required this.id,
    required this.title,
    required this.dateLabel,
    required this.messages,
  });

  final String id;
  final String title;
  final String dateLabel;
  final List<ChatMessage> messages;
}

class ChatHistoryEntry {
  const ChatHistoryEntry({
    required this.id,
    required this.dateLabel,
    required this.title,
  });

  final String id;
  final String dateLabel;
  final String title;
}

// ─────────────────────────────────────────────────────────
//  Repository interface
// ─────────────────────────────────────────────────────────

abstract class ChatRepository {
  /// Returns the list of demo conversations (with messages).
  Future<List<ChatConversation>> getConversations();

  /// Returns conversation history entries (for the history screen).
  Future<List<ChatHistoryEntry>> getHistoryEntries();

  /// Returns a canned assistant reply for the given user prompt.
  Future<ChatMessage> getReply(String prompt);
}
