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
  const ChatStreamFinished({this.metrics});

  /// Server-side performance metrics from the RUN_FINISHED event.
  final ChatRunMetrics? metrics;
}

/// Server-reported performance metrics for a completed streaming run.
class ChatRunMetrics {
  const ChatRunMetrics({
    this.inputTokens = 0,
    this.outputTokens = 0,
    this.totalTokens = 0,
    this.latencyMs = 0,
    this.timeToFirstTokenMs = 0,
  });

  final int inputTokens;
  final int outputTokens;
  final int totalTokens;
  final int latencyMs;
  final int timeToFirstTokenMs;

  @override
  String toString() =>
      'ChatRunMetrics(latency=${latencyMs}ms, ttft=${timeToFirstTokenMs}ms, '
      'tokens=${inputTokens}in/${outputTokens}out)';
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

/// The agent wants to deep-link the user to a specific screen
/// (e.g., statement upload, transaction detail for attaching a receipt).
///
/// Non-blocking — resolves immediately so the AG-UI re-run loop continues.
/// The UI is expected to call `context.goNamed(screenName, ...)`.
class ChatStreamNavigationRequested extends ChatStreamEvent {
  const ChatStreamNavigationRequested({
    required this.toolCallId,
    required this.screenName,
    this.pathParameters = const <String, String>{},
    this.queryParameters = const <String, String>{},
  });

  final String toolCallId;

  /// Named route from go_router (e.g., "spending-accounts-upload-statement").
  final String screenName;

  /// Path parameters keyed by go_router placeholder name
  /// (e.g., `{transactionId: 'abc'}` for `/spending/transaction/:transactionId`).
  final Map<String, String> pathParameters;

  /// Query parameters appended to the URL
  /// (e.g., `{accountId: 'abc'}` for `?accountId=abc`).
  final Map<String, String> queryParameters;
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

/// A sentence-level speech chunk emitted during streaming so TTS can start
/// playing before the assistant finishes generating.
class ChatStreamSpeechChunk extends ChatStreamEvent {
  const ChatStreamSpeechChunk({
    required this.messageId,
    required this.chunkIndex,
    required this.speechText,
    required this.isFinal,
  });

  final String messageId;
  final int chunkIndex;
  final String speechText;
  final bool isFinal;
}

/// One window of synthesized TTS audio for a chunk identified by
/// [chunkIndex]. Voice-mode AGUI runs emit one or more of these per
/// [ChatStreamSpeechChunk]. The terminal frame for a chunk has [isFinal]
/// set to `true`; clients reassemble [data] across frames in arrival
/// order, then play the chunk in [chunkIndex] order.
///
/// [data] is the raw decoded audio bytes (the wire base64 has already
/// been decoded by the repository). [mime] is one of `audio/mpeg`,
/// `audio/opus`, `audio/wav`.
class ChatStreamSpeechAudio extends ChatStreamEvent {
  const ChatStreamSpeechAudio({
    required this.messageId,
    required this.chunkIndex,
    required this.seq,
    required this.mime,
    required this.data,
    required this.isFinal,
    required this.cached,
    this.provider,
    this.voiceId,
    this.ttsAiRunId,
  });

  final String messageId;
  final int chunkIndex;
  final int seq;
  final String mime;
  final List<int> data;
  final bool isFinal;
  final bool cached;
  final String? provider;
  final String? voiceId;
  final String? ttsAiRunId;
}

/// Audio synthesis failed for the chunk identified by [chunkIndex]. The
/// chunk's text was already delivered via [ChatStreamSpeechChunk]; the
/// terminal flag tells the client to advance playback past this chunk
/// without waiting on audio that will never arrive.
class ChatStreamSpeechAudioError extends ChatStreamEvent {
  const ChatStreamSpeechAudioError({
    required this.messageId,
    required this.chunkIndex,
    required this.code,
    required this.message,
  });

  final String messageId;
  final int chunkIndex;

  /// One of `timeout`, `backpressure_dropped`, `synth_failed` in v1.
  final String code;
  final String message;
}

/// The types of display widgets the agent can request.
enum DisplayWidgetType {
  fxRateChart,
  budgetBreakdown,
  spendingPieChart,
  autopilotProposal,
  followUpSuggestions,
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
  /// [history] is the full AG-UI message history for the current thread.
  ///
  /// When [voiceMode] is `true`, the server inlines TTS audio bytes as
  /// [ChatStreamSpeechAudio] / [ChatStreamSpeechAudioError] events on the
  /// same response stream. The client is expected to play audio inline
  /// instead of issuing per-chunk synthesize calls. [audioFormat] picks
  /// the wire container; defaults to `mp3` and is ignored when
  /// [voiceMode] is `false`.
  Stream<ChatStreamEvent> sendMessage({
    String? threadId,
    required String userMessage,
    List<ChatMessage> history,
    bool voiceMode = false,
    String? audioFormat,
  });

  /// Fetches a thread with its full message history from the backend.
  Future<ChatConversation?> getThread(String threadId);

  /// Archives (soft-deletes) a conversation thread by ID.
  Future<void> deleteConversation(String id);

  /// Sends a canned assistant reply for the given user prompt.
  ///
  /// Retained for backward compatibility with mock mode.
  Future<ChatMessage> getReply(String prompt);

  /// Reports client-side performance metrics for a completed chat run.
  /// Fire-and-forget — implementations must swallow failures silently.
  Future<void> reportMetrics({
    required int clientRoundTripMs,
    required int clientTtftMs,
    int serverLatencyMs = 0,
    int serverTtftMs = 0,
    int inputTokens = 0,
    int outputTokens = 0,
    String? threadId,
    String? runId,
    String? agentName,
  });

  /// Reports voice-mode milestone/debug events. Fire-and-forget —
  /// implementations must swallow failures silently.
  Future<void> reportVoiceEvent({
    required String eventName,
    int? clientElapsedMs,
    String? threadId,
    String? runId,
    String? agentName,
    int? voiceTurnId,
    String? stage,
    String? reason,
    Map<String, Object?> details = const <String, Object?>{},
  });
}
