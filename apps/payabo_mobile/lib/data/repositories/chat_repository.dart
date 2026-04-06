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
    this.displayWidgets = const <ChatDisplayWidgetInfo>[],
  });

  final String? id;
  final ChatSender sender;
  final List<String> lines;
  final String? planTitle;
  final List<String> planItems;
  final List<ChatToolCallInfo> toolCalls;
  final List<ChatDisplayWidgetInfo> displayWidgets;

  bool get hasPlan => planTitle != null && planItems.isNotEmpty;
  bool get hasToolCalls => toolCalls.isNotEmpty;
  bool get hasDisplayWidgets => displayWidgets.isNotEmpty;
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

/// A display widget that was rendered inline during an assistant response.
/// Persisted in [ChatMessage.displayWidgets] so the widget survives in history.
class ChatDisplayWidgetInfo {
  const ChatDisplayWidgetInfo({
    required this.toolCallId,
    required this.widgetType,
    required this.data,
  });

  final String toolCallId;
  final DisplayWidgetType widgetType;
  final Map<String, dynamic> data;
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

/// The agent started calling a tool.
class ChatStreamToolCallStarted extends ChatStreamEvent {
  const ChatStreamToolCallStarted({
    required this.toolCallId,
    required this.toolName,
  });

  final String toolCallId;
  final String toolName;
}

/// A chunk of tool call arguments (JSON fragment) is streaming.
class ChatStreamToolCallArgs extends ChatStreamEvent {
  const ChatStreamToolCallArgs({
    required this.toolCallId,
    required this.delta,
  });

  final String toolCallId;
  final String delta;
}

/// The tool call specification is complete (all args received).
/// The tool is now pending execution.
class ChatStreamToolCallEnd extends ChatStreamEvent {
  const ChatStreamToolCallEnd({required this.toolCallId});

  final String toolCallId;
}

/// The agent's tool call received a result (server-side execution).
class ChatStreamToolCallResult extends ChatStreamEvent {
  const ChatStreamToolCallResult({
    required this.toolCallId,
    this.content,
  });

  final String toolCallId;
  final String? content;
}

/// The agent wants to perform a mutating action and is requesting user
/// approval via the human-in-the-loop pattern.
///
/// The UI should display an approval card with the action details.
/// Call the [onApprove] or [onReject] callbacks to resolve the approval
/// and allow the AG-UI re-run loop to continue.
class ChatStreamApprovalRequested extends ChatStreamEvent {
  ChatStreamApprovalRequested({
    required this.toolCallId,
    required this.action,
    required this.description,
    this.severity = 'medium',
    required this.onApprove,
    required this.onReject,
  });

  final String toolCallId;

  /// Short name of the action (e.g., "Create Transaction").
  final String action;

  /// Detailed description of what will happen if approved.
  final String description;

  /// Risk level: 'low', 'medium', or 'high'.
  final String severity;

  /// Call to approve the action. Resolves the confirmAction tool call
  /// and allows the AG-UI re-run loop to continue.
  final void Function() onApprove;

  /// Call to reject the action with an optional reason.
  final void Function([String? reason]) onReject;
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

/// A frontend display tool was invoked — the UI should render a rich widget
/// inline in the conversation.
///
/// Unlike [ChatStreamApprovalRequested], display widgets resolve immediately
/// (no user interaction needed to continue the AG-UI re-run loop).
class ChatStreamDisplayWidget extends ChatStreamEvent {
  const ChatStreamDisplayWidget({
    required this.toolCallId,
    required this.widgetType,
    required this.data,
  });

  final String toolCallId;

  /// Discriminator for which widget to render.
  final DisplayWidgetType widgetType;

  /// Parsed parameters from the tool call.
  final Map<String, dynamic> data;
}

class ChatStreamSpeechRender extends ChatStreamEvent {
  const ChatStreamSpeechRender({
    required this.messageId,
    required this.speechText,
    required this.requiresVisualAttention,
    required this.requiresApproval,
  });

  final String messageId;
  final String speechText;
  final bool requiresVisualAttention;
  final bool requiresApproval;
}

/// The types of display widgets the agent can request.
enum DisplayWidgetType {
  fxRateChart,
  budgetBreakdown,
  autopilotProposal,
  optionSelector,
}

/// A single option presented in an option selector card.
class OptionItem {
  const OptionItem({required this.label, this.description});

  final String label;
  final String? description;
}

/// The agent wants the user to choose from a set of options before proceeding.
///
/// This is a **blocking** frontend tool — the AG-UI re-run loop waits until
/// the user selects an option. Call [onSelect] with the chosen label(s) to
/// resolve the tool call and allow the agent to continue.
class ChatStreamOptionSelectionRequested extends ChatStreamEvent {
  ChatStreamOptionSelectionRequested({
    required this.toolCallId,
    required this.question,
    required this.options,
    this.multiSelect = false,
    required this.onSelect,
  });

  final String toolCallId;

  /// The prompt text (e.g., "Which account should I use?").
  final String question;

  /// The available options to choose from.
  final List<OptionItem> options;

  /// If true, the user may select multiple options.
  final bool multiSelect;

  /// Call with the selected label(s) to resolve the tool call.
  final void Function(List<String> selected) onSelect;
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

  /// Fetches a thread with its full message history from the backend.
  Future<ChatConversation?> getThread(String threadId);

  /// Archives (soft-deletes) a conversation thread by ID.
  Future<void> deleteConversation(String id);

  /// Sends a canned assistant reply for the given user prompt.
  ///
  /// Retained for backward compatibility with mock mode.
  Future<ChatMessage> getReply(String prompt);
}
