// ─────────────────────────────────────────────────────────
//  AG-UI Protocol Models
//
//  Strongly-typed Dart representations of the AG-UI event
//  protocol. Mirrors the SSE events emitted by the AONIK
//  backend at POST /ai/agui.
//
//  Reference: https://docs.ag-ui.com/concepts/events
// ─────────────────────────────────────────────────────────

import 'dart:convert';

// ─────────────────────────────────────────────────────────
//  Event type discriminator
// ─────────────────────────────────────────────────────────

/// All AG-UI event types the client can receive.
enum AgUiEventType {
  runStarted('RUN_STARTED'),
  runFinished('RUN_FINISHED'),
  runError('RUN_ERROR'),
  stepStarted('STEP_STARTED'),
  stepFinished('STEP_FINISHED'),
  textMessageStart('TEXT_MESSAGE_START'),
  textMessageContent('TEXT_MESSAGE_CONTENT'),
  textMessageEnd('TEXT_MESSAGE_END'),
  toolCallStart('TOOL_CALL_START'),
  toolCallArgs('TOOL_CALL_ARGS'),
  toolCallEnd('TOOL_CALL_END'),
  toolCallResult('TOOL_CALL_RESULT'),
  stateSnapshot('STATE_SNAPSHOT'),
  stateDelta('STATE_DELTA'),
  messagesSnapshot('MESSAGES_SNAPSHOT'),
  custom('CUSTOM'),
  raw('RAW'),
  unknown('UNKNOWN');

  const AgUiEventType(this.wire);

  /// The string value as it appears on the SSE wire.
  final String wire;

  static AgUiEventType fromWire(String value) {
    for (final t in AgUiEventType.values) {
      if (t.wire == value) return t;
    }
    return AgUiEventType.unknown;
  }
}

// ─────────────────────────────────────────────────────────
//  Events — sealed class hierarchy
// ─────────────────────────────────────────────────────────

/// Base class for all AG-UI events received from the SSE stream.
sealed class AgUiEvent {
  const AgUiEvent({required this.type, this.raw});

  final AgUiEventType type;

  /// The raw JSON map from the SSE `data:` line.
  final Map<String, dynamic>? raw;

  /// Factory that deserialises a JSON map into the correct subtype.
  factory AgUiEvent.fromJson(Map<String, dynamic> json) {
    final type = AgUiEventType.fromWire(json['type'] as String? ?? '');

    switch (type) {
      case AgUiEventType.runStarted:
        return RunStartedEvent(
          threadId: json['threadId'] as String?,
          runId: json['runId'] as String?,
          raw: json,
        );

      case AgUiEventType.runFinished:
        return RunFinishedEvent(
          threadId: json['threadId'] as String?,
          runId: json['runId'] as String?,
          raw: json,
        );

      case AgUiEventType.runError:
        return RunErrorEvent(
          message: json['message'] as String? ?? 'Unknown error',
          code: json['code'] as String?,
          raw: json,
        );

      case AgUiEventType.stepStarted:
        return StepStartedEvent(
          stepName: json['stepName'] as String?,
          raw: json,
        );

      case AgUiEventType.stepFinished:
        return StepFinishedEvent(
          stepName: json['stepName'] as String?,
          raw: json,
        );

      case AgUiEventType.textMessageStart:
        return TextMessageStartEvent(
          messageId: json['messageId'] as String? ?? '',
          role: json['role'] as String? ?? 'assistant',
          raw: json,
        );

      case AgUiEventType.textMessageContent:
        return TextMessageContentEvent(
          messageId: json['messageId'] as String? ?? '',
          delta: json['delta'] as String? ?? '',
          raw: json,
        );

      case AgUiEventType.textMessageEnd:
        return TextMessageEndEvent(
          messageId: json['messageId'] as String? ?? '',
          raw: json,
        );

      case AgUiEventType.toolCallStart:
        return ToolCallStartEvent(
          toolCallId: json['toolCallId'] as String? ?? '',
          toolCallName: json['toolCallName'] as String? ?? '',
          parentMessageId: json['parentMessageId'] as String?,
          raw: json,
        );

      case AgUiEventType.toolCallArgs:
        return ToolCallArgsEvent(
          toolCallId: json['toolCallId'] as String? ?? '',
          delta: json['delta'] as String? ?? '',
          raw: json,
        );

      case AgUiEventType.toolCallEnd:
        return ToolCallEndEvent(
          toolCallId: json['toolCallId'] as String? ?? '',
          raw: json,
        );

      case AgUiEventType.toolCallResult:
        return ToolCallResultEvent(
          messageId: json['messageId'] as String?,
          toolCallId: json['toolCallId'] as String? ?? '',
          content: json['content'] as String?,
          role: json['role'] as String?,
          raw: json,
        );

      case AgUiEventType.stateSnapshot:
        return StateSnapshotEvent(
          snapshot: json['snapshot'] as Map<String, dynamic>? ?? const {},
          raw: json,
        );

      case AgUiEventType.stateDelta:
        final deltaList = json['delta'] as List<dynamic>?;
        return StateDeltaEvent(
          delta: deltaList ?? const [],
          raw: json,
        );

      case AgUiEventType.messagesSnapshot:
        return MessagesSnapshotEvent(
          messages: json['messages'] as List<dynamic>? ?? const [],
          raw: json,
        );

      case AgUiEventType.custom:
        return CustomEvent(
          name: json['name'] as String? ?? '',
          value: json['value'],
          raw: json,
        );

      case AgUiEventType.raw:
      case AgUiEventType.unknown:
        return UnknownEvent(raw: json);
    }
  }
}

// ── Lifecycle ───────────────────────────────────────────

class RunStartedEvent extends AgUiEvent {
  const RunStartedEvent({
    this.threadId,
    this.runId,
    super.raw,
  }) : super(type: AgUiEventType.runStarted);

  final String? threadId;
  final String? runId;
}

class RunFinishedEvent extends AgUiEvent {
  const RunFinishedEvent({
    this.threadId,
    this.runId,
    super.raw,
  }) : super(type: AgUiEventType.runFinished);

  final String? threadId;
  final String? runId;
}

class RunErrorEvent extends AgUiEvent {
  const RunErrorEvent({
    required this.message,
    this.code,
    super.raw,
  }) : super(type: AgUiEventType.runError);

  final String message;
  final String? code;
}

class StepStartedEvent extends AgUiEvent {
  const StepStartedEvent({
    this.stepName,
    super.raw,
  }) : super(type: AgUiEventType.stepStarted);

  final String? stepName;
}

class StepFinishedEvent extends AgUiEvent {
  const StepFinishedEvent({
    this.stepName,
    super.raw,
  }) : super(type: AgUiEventType.stepFinished);

  final String? stepName;
}

// ── Text messages ───────────────────────────────────────

class TextMessageStartEvent extends AgUiEvent {
  const TextMessageStartEvent({
    required this.messageId,
    required this.role,
    super.raw,
  }) : super(type: AgUiEventType.textMessageStart);

  final String messageId;
  final String role;
}

class TextMessageContentEvent extends AgUiEvent {
  const TextMessageContentEvent({
    required this.messageId,
    required this.delta,
    super.raw,
  }) : super(type: AgUiEventType.textMessageContent);

  final String messageId;
  final String delta;
}

class TextMessageEndEvent extends AgUiEvent {
  const TextMessageEndEvent({
    required this.messageId,
    super.raw,
  }) : super(type: AgUiEventType.textMessageEnd);

  final String messageId;
}

// ── Tool calls ──────────────────────────────────────────

class ToolCallStartEvent extends AgUiEvent {
  const ToolCallStartEvent({
    required this.toolCallId,
    required this.toolCallName,
    this.parentMessageId,
    super.raw,
  }) : super(type: AgUiEventType.toolCallStart);

  final String toolCallId;
  final String toolCallName;
  final String? parentMessageId;
}

class ToolCallArgsEvent extends AgUiEvent {
  const ToolCallArgsEvent({
    required this.toolCallId,
    required this.delta,
    super.raw,
  }) : super(type: AgUiEventType.toolCallArgs);

  final String toolCallId;
  final String delta;
}

class ToolCallEndEvent extends AgUiEvent {
  const ToolCallEndEvent({
    required this.toolCallId,
    super.raw,
  }) : super(type: AgUiEventType.toolCallEnd);

  final String toolCallId;
}

class ToolCallResultEvent extends AgUiEvent {
  const ToolCallResultEvent({
    this.messageId,
    required this.toolCallId,
    this.content,
    this.role,
    super.raw,
  }) : super(type: AgUiEventType.toolCallResult);

  final String? messageId;
  final String toolCallId;
  final String? content;
  final String? role;
}

// ── State management ────────────────────────────────────

class StateSnapshotEvent extends AgUiEvent {
  const StateSnapshotEvent({
    required this.snapshot,
    super.raw,
  }) : super(type: AgUiEventType.stateSnapshot);

  final Map<String, dynamic> snapshot;
}

class StateDeltaEvent extends AgUiEvent {
  const StateDeltaEvent({
    required this.delta,
    super.raw,
  }) : super(type: AgUiEventType.stateDelta);

  final List<dynamic> delta;
}

class MessagesSnapshotEvent extends AgUiEvent {
  const MessagesSnapshotEvent({
    required this.messages,
    super.raw,
  }) : super(type: AgUiEventType.messagesSnapshot);

  final List<dynamic> messages;
}

// ── Special ─────────────────────────────────────────────

class CustomEvent extends AgUiEvent {
  const CustomEvent({
    required this.name,
    this.value,
    super.raw,
  }) : super(type: AgUiEventType.custom);

  final String name;
  final Object? value;
}

class UnknownEvent extends AgUiEvent {
  const UnknownEvent({super.raw}) : super(type: AgUiEventType.unknown);
}

// ─────────────────────────────────────────────────────────
//  AG-UI message types (sent in the request body)
// ─────────────────────────────────────────────────────────

/// A message within the AG-UI conversation history.
class AgUiMessage {
  const AgUiMessage({
    required this.id,
    required this.role,
    this.content,
    this.toolCalls,
    this.toolCallId,
    this.name,
  });

  final String id;
  final String role;
  final String? content;
  final List<AgUiToolCall>? toolCalls;
  final String? toolCallId;
  final String? name;

  Map<String, dynamic> toJson() {
    final json = <String, dynamic>{
      'id': id,
      'role': role,
    };
    if (content != null) json['content'] = content;
    if (toolCalls != null && toolCalls!.isNotEmpty) {
      json['toolCalls'] = toolCalls!.map((tc) => tc.toJson()).toList();
    }
    if (toolCallId != null) json['toolCallId'] = toolCallId;
    if (name != null) json['name'] = name;
    return json;
  }

  /// Convenience: create a user message.
  factory AgUiMessage.user({required String id, required String content}) {
    return AgUiMessage(id: id, role: 'user', content: content);
  }

  /// Convenience: create an assistant message.
  factory AgUiMessage.assistant({
    required String id,
    String? content,
    List<AgUiToolCall>? toolCalls,
  }) {
    return AgUiMessage(
      id: id,
      role: 'assistant',
      content: content,
      toolCalls: toolCalls,
    );
  }

  /// Convenience: create a tool result message.
  factory AgUiMessage.tool({
    required String id,
    required String toolCallId,
    required String content,
  }) {
    return AgUiMessage(
      id: id,
      role: 'tool',
      content: content,
      toolCallId: toolCallId,
    );
  }
}

/// A tool call embedded in an assistant message.
class AgUiToolCall {
  const AgUiToolCall({
    required this.id,
    required this.function,
    this.type = 'function',
  });

  final String id;
  final String type;
  final AgUiFunctionCall function;

  Map<String, dynamic> toJson() => {
        'id': id,
        'type': type,
        'function': function.toJson(),
      };
}

/// Function name + arguments within a tool call.
class AgUiFunctionCall {
  const AgUiFunctionCall({
    required this.name,
    required this.arguments,
  });

  final String name;
  final String arguments;

  Map<String, dynamic> toJson() => {
        'name': name,
        'arguments': arguments,
      };
}

// ─────────────────────────────────────────────────────────
//  AG-UI run input (request body)
// ─────────────────────────────────────────────────────────

/// The POST body sent to the AG-UI streaming endpoint.
class AgUiRunInput {
  const AgUiRunInput({
    this.threadId,
    this.runId,
    this.messages = const [],
    this.state,
    this.tools,
    this.context,
    this.forwardedProps,
  });

  final String? threadId;
  final String? runId;
  final List<AgUiMessage> messages;
  final Map<String, dynamic>? state;
  final List<Map<String, dynamic>>? tools;
  final List<dynamic>? context;
  final Map<String, dynamic>? forwardedProps;

  Map<String, dynamic> toJson() {
    final json = <String, dynamic>{};
    if (threadId != null) json['threadId'] = threadId;
    if (runId != null) json['runId'] = runId;
    json['messages'] = messages.map((m) => m.toJson()).toList();
    if (state != null) json['state'] = state;
    if (tools != null) json['tools'] = tools;
    if (context != null) json['context'] = context;
    if (forwardedProps != null) json['forwardedProps'] = forwardedProps;
    return json;
  }
}

// ─────────────────────────────────────────────────────────
//  SSE line parser
// ─────────────────────────────────────────────────────────

/// Parses a single SSE `data: {...}` line into an [AgUiEvent].
///
/// Returns `null` for empty lines, comments, or malformed data.
AgUiEvent? parseSseLine(String line) {
  final trimmed = line.trim();
  if (trimmed.isEmpty || trimmed.startsWith(':')) return null;

  if (!trimmed.startsWith('data:')) return null;

  final payload = trimmed.substring(5).trim();
  if (payload.isEmpty) return null;

  try {
    final json = jsonDecode(payload) as Map<String, dynamic>;
    return AgUiEvent.fromJson(json);
  } on FormatException {
    return null;
  }
}
