// ignore_for_file: experimental_member_use

import 'dart:async';

import 'package:flutter_test/flutter_test.dart';
import 'package:just_audio/just_audio.dart' as ja;
import 'package:payabo_mobile/features/chat/domain/chat_voice_service.dart';

/// Behavioural tests for [AppendableAudioSource]. These verify the wire
/// the just_audio player relies on: a single `request()` produces a
/// stream that yields buffered bytes immediately, then live appends,
/// and ends when the source is closed (cleanly or with an error).
///
/// We don't drive an actual just_audio player here — the platform
/// proxy needs Android/iOS service bindings — but the `request()`
/// contract is tested directly because that's the single integration
/// surface between our source and just_audio.
void main() {
  group('AppendableAudioSource', () {
    test('yields already-buffered bytes synchronously on request', () async {
      final source = AppendableAudioSource(contentType: 'audio/mpeg');
      source.append([1, 2, 3]);
      source.append([4, 5]);

      final response = await source.request();
      expect(response.contentType, 'audio/mpeg');
      expect(response.rangeRequestsSupported, isFalse);
      expect(response.sourceLength, isNull);
      expect(response.contentLength, isNull);
      expect(response.offset, 0);

      // Close so the stream completes deterministically.
      source.close();
      final collected = await response.stream.expand((c) => c).toList();
      expect(collected, [1, 2, 3, 4, 5]);
    });

    test('appends made after request reach the active reader', () async {
      final source = AppendableAudioSource(contentType: 'audio/mpeg');
      final response = await source.request();
      final received = <int>[];
      final completer = Completer<void>();
      response.stream.listen(
        received.addAll,
        onDone: completer.complete,
      );

      source.append([10, 20]);
      source.append([30]);
      source.close();

      await completer.future;
      expect(received, [10, 20, 30]);
    });

    test('mixes buffered + post-request appends in arrival order', () async {
      final source = AppendableAudioSource(contentType: 'audio/mpeg');
      source.append([1]);
      final response = await source.request();
      final received = <int>[];
      final completer = Completer<void>();
      response.stream.listen(
        received.addAll,
        onDone: completer.complete,
      );

      source.append([2, 3]);
      source.close();

      await completer.future;
      expect(received, [1, 2, 3]);
    });

    test('request after close yields buffered bytes then EOF immediately',
        () async {
      final source = AppendableAudioSource(contentType: 'audio/mpeg');
      source.append([7, 8, 9]);
      source.close();

      final response = await source.request();
      final received = await response.stream.expand((c) => c).toList();
      expect(received, [7, 8, 9]);
      expect(source.isClosed, isTrue);
    });

    test('closeWithError surfaces error to active reader', () async {
      final source = AppendableAudioSource(contentType: 'audio/mpeg');
      final response = await source.request();
      Object? capturedError;
      final completer = Completer<void>();
      response.stream.listen(
        (_) {},
        onError: (Object e) => capturedError = e,
        onDone: completer.complete,
      );

      source.append([1]);
      source.closeWithError(StateError('synth_failed'));

      await completer.future;
      expect(capturedError, isA<StateError>());
      expect(source.isClosed, isTrue);
    });

    test('empty source closed before request still yields a clean EOF',
        () async {
      final source = AppendableAudioSource(contentType: 'audio/mpeg');
      source.close();

      final response = await source.request();
      final received = await response.stream.expand((c) => c).toList();
      expect(received, isEmpty);
    });

    test('append after close is a no-op', () async {
      final source = AppendableAudioSource(contentType: 'audio/mpeg');
      source.close();
      source.append([1, 2, 3]);
      expect(source.bufferedLength, 0);
    });

    test('updateContentTypeBeforeFirstRead respects already-connected readers',
        () async {
      final source = AppendableAudioSource(contentType: 'audio/mpeg');
      // Before any reader: update succeeds.
      source.updateContentTypeBeforeFirstRead('audio/opus');
      expect(source.contentType, 'audio/opus');

      // After request(): updates are ignored.
      final response = await source.request();
      source.updateContentTypeBeforeFirstRead('audio/wav');
      expect(source.contentType, 'audio/opus');
      expect(response.contentType, 'audio/opus');

      source.close();
    });

    test('cancelling the response stream removes the reader from active set',
        () async {
      final source = AppendableAudioSource(contentType: 'audio/mpeg');
      final response = await source.request();
      final sub = response.stream.listen((_) {});

      // Reader registered.
      source.append([1]);
      await sub.cancel();

      // After cancel, append should be a no-op against the cancelled reader
      // (no exception). The source itself remains writable for any future
      // request — though just_audio only requests once with
      // rangeRequestsSupported=false, so this is mostly defensive.
      source.append([2]);
      // No assert — just verifying no throw on append-after-cancel.
      source.close();
    });

    test('bufferedLength tracks total appended bytes (uses ja name)',
        () async {
      // Sanity check that the source advertises a content type that
      // just_audio's `StreamAudioResponse` expects.
      final source = AppendableAudioSource(contentType: 'audio/mpeg');
      source.append(List<int>.filled(64, 0));
      source.append(List<int>.filled(32, 1));
      expect(source.bufferedLength, 96);

      final response = await source.request();
      // Reference the just_audio type for compile-time API stability.
      expect(response, isA<ja.StreamAudioResponse>());
      source.close();
    });

    test('contentTypeReady resolves on first append', () async {
      final source = AppendableAudioSource(contentType: 'audio/mpeg');
      var resolved = false;
      // ignore: unawaited_futures
      source.contentTypeReady.then((_) => resolved = true);

      // Microtask boundary so the .then callback can run if it would.
      await Future<void>.value();
      expect(resolved, isFalse);

      source.append([1, 2, 3]);
      await source.contentTypeReady;
      expect(resolved, isTrue);
      source.close();
    });

    test('contentTypeReady resolves on close even with no frames', () async {
      final source = AppendableAudioSource(contentType: 'audio/mpeg');
      var resolved = false;
      // ignore: unawaited_futures
      source.contentTypeReady.then((_) => resolved = true);

      source.close();
      await source.contentTypeReady;
      expect(resolved, isTrue);
      expect(source.isClosed, isTrue);
      expect(source.bufferedLength, 0);
    });

    test('contentTypeReady resolves on closeWithError', () async {
      final source = AppendableAudioSource(contentType: 'audio/mpeg');
      source.closeWithError(StateError('boom'));
      // Should resolve, not raise. Awaiters then proceed and check
      // [isClosed] / buffered length.
      await source.contentTypeReady;
      expect(source.isClosed, isTrue);
    });

    test('content type set by frame mime is observable after contentTypeReady',
        () async {
      final source = AppendableAudioSource(contentType: 'audio/mpeg');
      // Simulate the production flow: first frame sets the real mime
      // BEFORE the player's request lands.
      source.updateContentTypeBeforeFirstRead('audio/opus');
      source.append([1, 2, 3]);

      await source.contentTypeReady;
      expect(source.contentType, 'audio/opus');

      final response = await source.request();
      expect(response.contentType, 'audio/opus');
      source.close();
    });

    test('playbackBufferReady waits for the threshold before resolving',
        () async {
      // Tight threshold so the test stays small.
      final source = AppendableAudioSource(
        contentType: 'audio/mpeg',
        initialPlaybackBufferBytes: 8,
      );
      var resolved = false;
      // ignore: unawaited_futures
      source.playbackBufferReady.then((_) => resolved = true);

      // First small append: content type ready, buffer NOT yet ready.
      source.append([1, 2, 3]);
      await Future<void>.value();
      expect(source.bufferedLength, 3);
      expect(resolved, isFalse,
          reason: 'buffer below threshold should not resolve playbackBufferReady');

      // Cross the threshold.
      source.append([4, 5, 6, 7, 8]);
      await source.playbackBufferReady;
      expect(resolved, isTrue);
      expect(source.bufferedLength, 8);
      source.close();
    });

    test('playbackBufferReady resolves immediately once threshold is reached',
        () async {
      // Threshold smaller than one append — should resolve on that
      // first append without needing more bytes.
      final source = AppendableAudioSource(
        contentType: 'audio/mpeg',
        initialPlaybackBufferBytes: 4,
      );
      source.append([1, 2, 3, 4, 5, 6, 7, 8]);
      await source.playbackBufferReady;
      expect(source.bufferedLength, 8);
      source.close();
    });

    test('playbackBufferReady resolves on close even if below threshold',
        () async {
      // Small chunk that closes before reaching the threshold — the
      // gate must still resolve so the playback loop can play
      // whatever was buffered (or skip if empty).
      final source = AppendableAudioSource(
        contentType: 'audio/mpeg',
        initialPlaybackBufferBytes: 1024,
      );
      source.append([1, 2, 3]);
      source.close();
      await source.playbackBufferReady;
      expect(source.isClosed, isTrue);
      expect(source.bufferedLength, 3);
    });

    test('playbackBufferReady resolves on closeWithError', () async {
      final source = AppendableAudioSource(
        contentType: 'audio/mpeg',
        initialPlaybackBufferBytes: 1024,
      );
      source.closeWithError(StateError('synth_failed'));
      await source.playbackBufferReady;
      expect(source.isClosed, isTrue);
      expect(source.bufferedLength, 0);
    });
  });
}
