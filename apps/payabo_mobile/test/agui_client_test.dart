import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:payabo_mobile/data/agui/agui_client.dart';
import 'package:payabo_mobile/data/agui/agui_models.dart';

void main() {
  test('runWithTools reruns after frontend tool execution', () async {
    final adapter = _FakeHttpClientAdapter([
      'data: {"type":"TOOL_CALL_START","toolCallId":"call-1","toolCallName":"confirmAction"}\n\n'
          'data: {"type":"TOOL_CALL_ARGS","toolCallId":"call-1","delta":"{\\"action\\":\\"Create account\\",\\"description\\":\\"Create a starter account\\"}"}\n\n'
          'data: {"type":"TOOL_CALL_END","toolCallId":"call-1"}\n\n'
          'data: {"type":"RUN_FINISHED","threadId":"thread-1","runId":"run-1"}\n\n',
      'data: {"type":"TEXT_MESSAGE_CONTENT","messageId":"assistant-2","delta":"All set."}\n\n'
          'data: {"type":"RUN_FINISHED","threadId":"thread-1","runId":"run-2"}\n\n',
    ]);

    final dio = Dio(BaseOptions(baseUrl: 'https://example.test'));
    dio.httpClientAdapter = adapter;

    final client = AgUiClient(dio: dio);
    final input = AgUiRunInput(
      threadId: 'thread-1',
      agentId: 'personal-finance-agent',
      messages: [
        AgUiMessage.user(id: 'user-1', content: 'Create an account for me'),
      ],
    );

    final events = await client.runWithTools(
      input,
      frontendTools: {
        'confirmAction': FrontendToolRegistration(
          tool: const AgUiToolDefinition(
            name: 'confirmAction',
            description: 'Request user approval before mutating data.',
            parameters: {
              'type': 'object',
              'properties': {
                'action': {'type': 'string'},
                'description': {'type': 'string'},
              },
              'required': ['action', 'description'],
            },
          ),
          handler: (args, context) async {
            expect(context.toolCallId, 'call-1');
            expect(args['action'], 'Create account');
            return 'approved';
          },
        ),
      },
    ).toList();

    expect(adapter.requestBodies, hasLength(2));

    final rerunBody = adapter.requestBodies[1];
    final rerunMessages = rerunBody['messages'] as List<dynamic>;
    final assistantToolCall =
        rerunMessages[rerunMessages.length - 2] as Map<String, dynamic>;
    final toolResult = rerunMessages.last as Map<String, dynamic>;

    expect(assistantToolCall['role'], 'assistant');
    expect(assistantToolCall['toolCalls'], [
      {
        'id': 'call-1',
        'type': 'function',
        'function': {
          'name': 'confirmAction',
          'arguments':
              '{"action":"Create account","description":"Create a starter account"}',
        },
      },
    ]);

    expect(toolResult, {
      'id': 'tool-result-call-1',
      'role': 'tool',
      'content': 'approved',
      'toolCallId': 'call-1',
    });

    expect(
        events.whereType<TextMessageContentEvent>().map((e) => e.delta).join(),
        'All set.');
    expect(events.whereType<RunFinishedEvent>(), hasLength(1));
  });
}

class _FakeHttpClientAdapter implements HttpClientAdapter {
  _FakeHttpClientAdapter(this._responses);

  final List<String> _responses;
  final List<Map<String, dynamic>> requestBodies = [];
  var _index = 0;

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    final bodyBytes = <int>[];
    if (requestStream != null) {
      await for (final chunk in requestStream) {
        bodyBytes.addAll(chunk);
      }
    }

    if (bodyBytes.isNotEmpty) {
      requestBodies
          .add(jsonDecode(utf8.decode(bodyBytes)) as Map<String, dynamic>);
    } else {
      requestBodies.add(const {});
    }

    if (_index >= _responses.length) {
      throw StateError('No fake response configured for request $_index');
    }

    return ResponseBody.fromString(
      _responses[_index++],
      200,
      headers: {
        Headers.contentTypeHeader: ['text/event-stream'],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}
