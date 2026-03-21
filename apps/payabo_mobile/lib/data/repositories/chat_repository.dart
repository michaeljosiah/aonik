// ─────────────────────────────────────────────────────────
//  ChatRepository — interface + DTOs
//
//  Surfaces conversation threads, messages, and streaming
//  agent interactions for the chat feature.
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
    this.id,
    this.planTitle,
    this.planItems = const <String>[],
    this.toolCalls = const <ChatToolCallInfo>[],
  });

  final String? id;
  final ChatSender sender;
  final List<String> lines;
  final String? planTitle;
  final List<String> planItems;
  final List<ChatToolCallInfo> toolCalls;

  bool get hasPlan => planTitle != null && planItems.isNotEmpty;
  bool get hasToolCalls => toolCalls.isNotEmpty;
}

/// Information about a tool call made by the agent during a response.
class ChatToolCallInfo {
  const ChatToolCallInfo({
    required this.toolCallId,
    required this.name,
    this.arguments,
    this.result,
    this.isComplete = false,
  });

  final String toolCallId;
  final String name;
  final String? arguments;
  final String? result;
  final bool isComplete;

  ChatToolCallInfo copyWith({
    String? arguments,
    String? result,
    bool? isComplete,
  }) {
    return ChatToolCallInfo(
      toolCallId: toolCallId,
      name: name,
      arguments: arguments ?? this.arguments,
      result: result ?? this.result,
      isComplete: isComplete ?? this.isComplete,
    );
  }
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
//  Streaming events (chat-level abstractions over AG-UI)
// ─────────────────────────────────────────────────────────

/// Events emitted during a streaming agent response.
sealed class ChatStreamEvent {
  const ChatStreamEvent();
}

/// The streaming run has started.
class ChatStreamStarted extends ChatStreamEvent {
  const ChatStreamStarted({this.threadId, this.runId});

  final String? threadId;
  final String? runId;
}

/// A chunk of text from the assistant's response.
class ChatStreamTextDelta extends ChatStreamEvent {
  const ChatStreamTextDelta(this.delta, {required this.messageId});

  final String delta;
  final String messageId;
}

/// The assistant's text message is complete.
class ChatStreamTextDone extends ChatStreamEvent {
  const ChatStreamTextDone({required this.messageId});

  final String messageId;
}

/// The agent started calling a server-side tool.
class ChatStreamToolCallStarted extends ChatStreamEvent {
  const ChatStreamToolCallStarted({
    required this.toolCallId,
    required this.toolName,
  });

  final String toolCallId;
  final String toolName;
}

/// The agent's tool call received a result.
class ChatStreamToolCallResult extends ChatStreamEvent {
  const ChatStreamToolCallResult({
    required this.toolCallId,
    this.content,
  });

  final String toolCallId;
  final String? content;
}

/// The streaming run finished successfully.
class ChatStreamFinished extends ChatStreamEvent {
  const ChatStreamFinished();
}

/// An error occurred during the streaming run.
class ChatStreamError extends ChatStreamEvent {
  const ChatStreamError(this.message, {this.code});

  final String message;
  final String? code;
}

// ─────────────────────────────────────────────────────────
//  Repository interface
// ─────────────────────────────────────────────────────────

abstract class ChatRepository {
  /// Returns the list of demo conversations (with messages).
  Future<List<ChatConversation>> getConversations();

  /// Returns conversation history entries (for the history screen).
  Future<List<ChatHistoryEntry>> getHistoryEntries();

  /// Sends a user message (with full conversation history) to the agent
  /// and streams back incremental events.
  ///
  /// [threadId] identifies the conversation thread (null to start a new one).
  /// [messages] is the full AG-UI message history for the current thread.
  Stream<ChatStreamEvent> sendMessage({
    String? threadId,
    required String userMessage,
    List<ChatMessage> history,
  });

  /// Sends a canned assistant reply for the given user prompt.
  ///
  /// Retained for backward compatibility with mock mode.
  Future<ChatMessage> getReply(String prompt);
}
