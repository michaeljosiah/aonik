// ignore_for_file: public_member_api_docs

import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'voxa_voice_client.dart';
import 'voxa_voice_session.dart';

/// Smoke-test screen for the new WSS voice mode pipeline (spec 024 Phase H follow-up).
///
/// Wires a [VoxaVoiceSession] to the simplest possible UI — Start / Stop button,
/// connection state, a transcript trail, and an inline error chip. Intended as a
/// pre-production smoke test, NOT a replacement for `chat_voice_service.dart`'s
/// production voice mode. Swapping the chat screen behind
/// [voxaVoiceModeEnabledProvider] is a separate piece of work that needs careful
/// coordination with the chat UI team.
class VoxaVoiceTestScreen extends ConsumerStatefulWidget {
  const VoxaVoiceTestScreen({super.key, this.agentId = 'personal-finance-agent'});

  /// Agent the WSS hello envelope will target. Defaults to the personal-finance agent
  /// — the most common Payabo voice scenario.
  final String agentId;

  @override
  ConsumerState<VoxaVoiceTestScreen> createState() => _VoxaVoiceTestScreenState();
}

class _VoxaVoiceTestScreenState extends ConsumerState<VoxaVoiceTestScreen> {
  final List<_TranscriptEntry> _transcript = <_TranscriptEntry>[];
  VoxaConnectionState _state = VoxaConnectionState.idle;
  String? _errorMessage;
  String? _whoIsSpeaking;
  bool _starting = false;

  StreamSubscription<VoxaVoiceEvent>? _eventSub;
  StreamSubscription<VoxaConnectionState>? _stateSub;

  @override
  void dispose() {
    unawaited(_eventSub?.cancel());
    unawaited(_stateSub?.cancel());
    super.dispose();
  }

  Future<void> _handleStart() async {
    setState(() {
      _starting = true;
      _errorMessage = null;
      _transcript.clear();
      _whoIsSpeaking = null;
    });

    final VoxaVoiceSession session = ref.read(voxaVoiceSessionProvider);

    // Subscribe BEFORE start so we don't miss the threadReady envelope.
    _eventSub?.cancel();
    _eventSub = session.events.listen(_onEvent);
    _stateSub?.cancel();
    _stateSub = session.stateChanges.listen((VoxaConnectionState state) {
      if (mounted) setState(() => _state = state);
    });

    try {
      await session.start(agentId: widget.agentId);
    } catch (err) {
      if (mounted) {
        setState(() {
          _errorMessage = err.toString();
        });
      }
    } finally {
      if (mounted) {
        setState(() {
          _starting = false;
          _state = session.connectionState;
        });
      }
    }
  }

  Future<void> _handleStop() async {
    final VoxaVoiceSession session = ref.read(voxaVoiceSessionProvider);
    await session.stop();
    if (mounted) {
      setState(() {
        _state = session.connectionState;
        _whoIsSpeaking = null;
      });
    }
  }

  void _onEvent(VoxaVoiceEvent event) {
    if (!mounted) return;
    setState(() {
      switch (event) {
        case TranscriptionEvent(:final String text, :final bool isFinal):
          if (text.trim().isEmpty) return;
          _appendOrReplaceLastPartial(
            who: 'user',
            text: text,
            isFinal: isFinal,
          );
          break;
        case BotTextEvent(:final String text):
          if (text.trim().isEmpty) return;
          _appendOrMergeBot(text);
          break;
        case SpeakingEvent(:final String who, :final bool started):
          _whoIsSpeaking = started ? who : null;
          break;
        case InterruptionEvent():
          // Player gets reset implicitly when the bot stops — nothing to update in UI.
          break;
        case StatusEvent(:final String message):
          _transcript.add(_TranscriptEntry(
            who: 'system',
            text: message,
            isFinal: true,
          ));
          break;
        case ErrorEvent(:final String message):
          _errorMessage = message;
          break;
        case EndedEvent():
          _transcript.add(const _TranscriptEntry(
            who: 'system',
            text: 'Session ended by server.',
            isFinal: true,
          ));
          break;
        case ThreadReadyEvent(:final String chatThreadId):
          _transcript.add(_TranscriptEntry(
            who: 'system',
            text: 'Thread ready: ${chatThreadId.substring(0, chatThreadId.length.clamp(0, 8))}…',
            isFinal: true,
          ));
          break;
        case ToolCallEvent(:final String name):
          _transcript.add(_TranscriptEntry(
            who: 'system',
            text: 'Tool call: $name',
            isFinal: true,
          ));
          break;
      }
    });
  }

  void _appendOrReplaceLastPartial({
    required String who,
    required String text,
    required bool isFinal,
  }) {
    if (_transcript.isNotEmpty) {
      final _TranscriptEntry last = _transcript.last;
      if (last.who == who && !last.isFinal && !isFinal) {
        _transcript[_transcript.length - 1] = last.copyWith(text: text);
        return;
      }
    }
    _transcript.add(_TranscriptEntry(who: who, text: text, isFinal: isFinal));
  }

  void _appendOrMergeBot(String text) {
    if (_transcript.isNotEmpty && _transcript.last.who == 'bot') {
      final _TranscriptEntry last = _transcript.last;
      _transcript[_transcript.length - 1] = last.copyWith(text: last.text + text);
      return;
    }
    _transcript.add(_TranscriptEntry(who: 'bot', text: text, isFinal: true));
  }

  @override
  Widget build(BuildContext context) {
    final ThemeData theme = Theme.of(context);
    final bool isLive = _state == VoxaConnectionState.connected ||
        _state == VoxaConnectionState.connecting ||
        _starting;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Voice mode (Voxa / WSS)'),
      ),
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: <Widget>[
              _ConnectionPill(state: _state, starting: _starting),
              const SizedBox(height: 12),
              Text(
                'Talking to agent: ${widget.agentId}',
                style: theme.textTheme.bodyMedium,
              ),
              if (_whoIsSpeaking != null) ...<Widget>[
                const SizedBox(height: 6),
                Text(
                  _whoIsSpeaking == 'bot'
                      ? 'Bot is speaking…'
                      : 'You are speaking…',
                  style: theme.textTheme.bodySmall
                      ?.copyWith(color: theme.colorScheme.primary),
                ),
              ],
              const SizedBox(height: 16),
              if (_errorMessage != null)
                Card(
                  color: theme.colorScheme.errorContainer,
                  child: Padding(
                    padding: const EdgeInsets.all(12),
                    child: Text(
                      _errorMessage!,
                      style: TextStyle(color: theme.colorScheme.onErrorContainer),
                    ),
                  ),
                ),
              const SizedBox(height: 8),
              if (isLive)
                FilledButton.tonal(
                  onPressed: _starting ? null : _handleStop,
                  child: const Text('Stop'),
                )
              else
                FilledButton(
                  onPressed: _starting ? null : _handleStart,
                  child: Text(_starting ? 'Connecting…' : 'Start voice test'),
                ),
              const SizedBox(height: 16),
              Expanded(
                child: _transcript.isEmpty
                    ? Center(
                        child: Text(
                          'Transcript will appear here once you start talking.',
                          style: theme.textTheme.bodySmall,
                          textAlign: TextAlign.center,
                        ),
                      )
                    : ListView.separated(
                        itemCount: _transcript.length,
                        separatorBuilder: (_, __) => const SizedBox(height: 8),
                        itemBuilder: (BuildContext context, int index) =>
                            _TranscriptRow(entry: _transcript[index]),
                      ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _TranscriptEntry {
  const _TranscriptEntry({
    required this.who,
    required this.text,
    required this.isFinal,
  });

  final String who;
  final String text;
  final bool isFinal;

  _TranscriptEntry copyWith({String? text, bool? isFinal}) => _TranscriptEntry(
        who: who,
        text: text ?? this.text,
        isFinal: isFinal ?? this.isFinal,
      );
}

class _TranscriptRow extends StatelessWidget {
  const _TranscriptRow({required this.entry});

  final _TranscriptEntry entry;

  @override
  Widget build(BuildContext context) {
    final ThemeData theme = Theme.of(context);
    final Color badgeColour = switch (entry.who) {
      'user' => theme.colorScheme.primary,
      'bot' => theme.colorScheme.tertiary,
      _ => theme.colorScheme.onSurfaceVariant,
    };
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Container(
          padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
          decoration: BoxDecoration(
            border: Border.all(color: badgeColour),
            borderRadius: BorderRadius.circular(4),
          ),
          child: Text(
            entry.who,
            style: TextStyle(fontSize: 10, color: badgeColour),
          ),
        ),
        const SizedBox(width: 8),
        Expanded(
          child: Text(
            entry.text,
            style: theme.textTheme.bodyMedium?.copyWith(
              fontStyle: entry.isFinal ? FontStyle.normal : FontStyle.italic,
              color: entry.isFinal
                  ? theme.textTheme.bodyMedium?.color
                  : theme.colorScheme.onSurfaceVariant,
            ),
          ),
        ),
      ],
    );
  }
}

class _ConnectionPill extends StatelessWidget {
  const _ConnectionPill({required this.state, required this.starting});

  final VoxaConnectionState state;
  final bool starting;

  @override
  Widget build(BuildContext context) {
    final ThemeData theme = Theme.of(context);
    final (String label, Color colour) = switch (state) {
      VoxaConnectionState.connected => ('Live', theme.colorScheme.primary),
      VoxaConnectionState.connecting => ('Connecting', theme.colorScheme.secondary),
      VoxaConnectionState.error => ('Error', theme.colorScheme.error),
      VoxaConnectionState.closed => ('Closed', theme.colorScheme.onSurfaceVariant),
      VoxaConnectionState.idle =>
        starting ? ('Connecting', theme.colorScheme.secondary) : ('Idle', theme.colorScheme.onSurfaceVariant),
    };
    return Align(
      alignment: Alignment.centerLeft,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
        decoration: BoxDecoration(
          border: Border.all(color: colour),
          borderRadius: BorderRadius.circular(99),
        ),
        child: Text(
          label,
          style: theme.textTheme.labelSmall?.copyWith(color: colour),
        ),
      ),
    );
  }
}
