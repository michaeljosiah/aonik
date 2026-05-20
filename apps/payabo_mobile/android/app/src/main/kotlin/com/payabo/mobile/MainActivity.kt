package com.payabo.mobile

import android.content.Context
import android.media.AudioDeviceInfo
import android.media.AudioAttributes
import android.media.AudioFormat
import android.media.AudioManager
import android.media.AudioTrack
import android.os.Build
import android.os.Process
import android.util.Log
import io.flutter.embedding.android.FlutterFragmentActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel
import java.util.ArrayDeque
import java.util.concurrent.LinkedBlockingQueue
import java.util.concurrent.TimeUnit
import kotlin.math.max

class MainActivity : FlutterFragmentActivity() {
    private val voicePcmPlayer: VoicePcmPlayer by lazy {
        VoicePcmPlayer(applicationContext)
    }

    override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)

        MethodChannel(
            flutterEngine.dartExecutor.binaryMessenger,
            "payabo/voice_pcm_player"
        ).setMethodCallHandler { call, result ->
            try {
                when (call.method) {
                    "start" -> {
                        voicePcmPlayer.start(
                            sampleRate = call.argument<Int>("sampleRate") ?: 24000,
                            volume = (call.argument<Double>("volume") ?: 1.0).toFloat(),
                            maxQueuedBytes = call.argument<Int>("maxBufferBytes") ?: 100 * 1024 * 1024,
                            bufferMs = call.argument<Int>("bufferMs") ?: 1500,
                        )
                        result.success(null)
                    }

                    "write" -> {
                        val data = call.argument<ByteArray>("data")
                        result.success(data != null && voicePcmPlayer.write(data))
                    }

                    "position" -> result.success(voicePcmPlayer.positionSeconds())

                    "stop" -> {
                        voicePcmPlayer.stop()
                        result.success(null)
                    }

                    else -> result.notImplemented()
                }
            } catch (ex: Exception) {
                result.error("voice-pcm-player", ex.message, null)
            }
        }
    }

    override fun onDestroy() {
        voicePcmPlayer.stop()
        super.onDestroy()
    }
}

private class VoicePcmPlayer(context: Context) {
    private val lock = Any()
    private val queue = LinkedBlockingQueue<ByteArray>()
    private val writtenSegments = ArrayDeque<WrittenSegment>()
    private val audioManager = context.getSystemService(Context.AUDIO_SERVICE) as AudioManager

    @Volatile private var running = false
    @Volatile private var queuedBytes = 0

    private var track: AudioTrack? = null
    private var writerThread: Thread? = null
    private var sampleRate = 24000
    private var maxQueuedBytes = 100 * 1024 * 1024
    private var keepAliveSilence = ByteArray(0)
    private var playbackStarted = false
    private var outputVolume = 1f
    private var previousAudioMode: Int? = null
    private var previousSpeakerphoneOn: Boolean? = null
    private var routedToCommunicationSpeaker = false
    private var countedFramesWritten = 0L
    private var countedFramesPlayed = 0L
    private var playbackHeadFramesAccounted = 0L
    private var lastProgressLogFrame = 0L

    fun start(sampleRate: Int, volume: Float, maxQueuedBytes: Int, bufferMs: Int) {
        synchronized(lock) {
            stopLocked()

            this.sampleRate = sampleRate
            this.maxQueuedBytes = maxQueuedBytes
            outputVolume = volume.coerceIn(0f, 1f)
            queuedBytes = 0
            queue.clear()
            writtenSegments.clear()
            keepAliveSilence = ByteArray(sampleRate * BYTES_PER_FRAME * KEEP_ALIVE_CHUNK_MS / 1000)
            playbackStarted = false
            countedFramesWritten = 0L
            countedFramesPlayed = 0L
            playbackHeadFramesAccounted = 0L
            lastProgressLogFrame = 0L

            prepareAudioRouteLocked()

            val minBuffer = AudioTrack.getMinBufferSize(
                sampleRate,
                AudioFormat.CHANNEL_OUT_MONO,
                AudioFormat.ENCODING_PCM_16BIT,
            )
            val requestedBuffer = sampleRate * BYTES_PER_FRAME * bufferMs / 1000
            val bufferSize = max(minBuffer, requestedBuffer)

            val audioTrack = AudioTrack.Builder()
                .setAudioAttributes(
                    AudioAttributes.Builder()
                        .setUsage(AudioAttributes.USAGE_VOICE_COMMUNICATION)
                        .setContentType(AudioAttributes.CONTENT_TYPE_SPEECH)
                        .build()
                )
                .setAudioFormat(
                    AudioFormat.Builder()
                        .setSampleRate(sampleRate)
                        .setChannelMask(AudioFormat.CHANNEL_OUT_MONO)
                        .setEncoding(AudioFormat.ENCODING_PCM_16BIT)
                        .build()
                )
                .setBufferSizeInBytes(bufferSize)
                .setTransferMode(AudioTrack.MODE_STREAM)
                .build()

            audioTrack.setVolume(outputVolume)

            track = audioTrack
            running = true
            Log.i(
                TAG,
                "start sampleRate=$sampleRate bufferSize=$bufferSize minBuffer=$minBuffer keepAliveMs=$KEEP_ALIVE_CHUNK_MS"
            )
            writerThread = Thread(::writerLoop, "PayaboVoiceAudioTrack").also { thread ->
                thread.isDaemon = true
                thread.start()
            }
        }
    }

    fun write(data: ByteArray): Boolean {
        if (data.isEmpty()) return true
        synchronized(lock) {
            if (!running || track == null) {
                Log.w(TAG, "write rejected because player is not running")
                return false
            }
            if (queuedBytes + data.size > maxQueuedBytes) {
                Log.w(TAG, "write rejected because queue is full: queuedBytes=$queuedBytes size=${data.size}")
                return false
            }
            queuedBytes += data.size
            queue.offer(data.copyOf())
            return true
        }
    }

    fun positionSeconds(): Double {
        synchronized(lock) {
            updatePlaybackAccountingLocked()
            return countedFramesPlayed.toDouble() / sampleRate
        }
    }

    fun stop() {
        synchronized(lock) {
            stopLocked()
        }
    }

    private fun stopLocked() {
        running = false
        queuedBytes = 0
        queue.clear()
        writtenSegments.clear()
        keepAliveSilence = ByteArray(0)
        playbackStarted = false
        outputVolume = 1f
        countedFramesWritten = 0L
        countedFramesPlayed = 0L
        playbackHeadFramesAccounted = 0L
        lastProgressLogFrame = 0L
        writerThread?.interrupt()
        writerThread = null

        val currentTrack = track
        track = null
        if (currentTrack != null) {
            Log.i(TAG, "stop")
            try {
                currentTrack.pause()
                currentTrack.flush()
                currentTrack.stop()
            } catch (_: IllegalStateException) {
                // Already stopped or not fully initialised.
            } finally {
                currentTrack.release()
            }
        }
        restoreAudioRouteLocked()
    }

    private fun writerLoop() {
        Process.setThreadPriority(Process.THREAD_PRIORITY_AUDIO)

        while (running) {
            val queuedData = try {
                queue.poll(KEEP_ALIVE_CHUNK_MS.toLong(), TimeUnit.MILLISECONDS)
            } catch (_: InterruptedException) {
                break
            }
            val counted = queuedData != null
            val data = queuedData ?: synchronized(lock) {
                if (!playbackStarted) return@synchronized null
                keepAliveSilence
            } ?: continue

            if (counted) synchronized(lock) {
                queuedBytes = (queuedBytes - data.size).coerceAtLeast(0)
            }

            var offset = 0
            while (running && offset < data.size) {
                val currentTrack = track ?: return
                val primingPlayback = synchronized(lock) { !playbackStarted && counted }
                val written = currentTrack.write(
                    data,
                    offset,
                    data.size - offset,
                    if (primingPlayback) AudioTrack.WRITE_NON_BLOCKING else AudioTrack.WRITE_BLOCKING,
                )
                if (written < 0) {
                    Log.w(TAG, "AudioTrack.write failed: $written")
                    return
                }
                if (written == 0) {
                    Thread.yield()
                    continue
                }
                val writtenFrames = (written / BYTES_PER_FRAME).toLong()
                var shouldStartPlayback = false
                synchronized(lock) {
                    writtenSegments.add(WrittenSegment(writtenFrames, counted))
                    if (counted) {
                        countedFramesWritten += writtenFrames
                    }
                    if (!playbackStarted && countedFramesWritten > 0) {
                        playbackStarted = true
                        shouldStartPlayback = true
                    }
                }
                if (shouldStartPlayback) {
                    try {
                        currentTrack.play()
                        Log.i(TAG, "play after $countedFramesWritten counted frames")
                        restoreAudibleOutputLocked()
                    } catch (ex: IllegalStateException) {
                        Log.w(TAG, "AudioTrack.play failed: ${ex.message}")
                        return
                    }
                }
                offset += written
            }
        }
    }

    private fun updatePlaybackAccountingLocked() {
        val currentTrack = track ?: return
        val playbackHeadFrames = currentTrack.playbackHeadPosition.toLong()
        var framesToAccount = (playbackHeadFrames - playbackHeadFramesAccounted).coerceAtLeast(0)
        playbackHeadFramesAccounted = playbackHeadFrames

        while (framesToAccount > 0 && !writtenSegments.isEmpty()) {
            val segment = writtenSegments.peekFirst()
            val consumed = minOf(framesToAccount, segment.frames)
            if (segment.counted) {
                countedFramesPlayed += consumed
            }
            segment.frames -= consumed
            framesToAccount -= consumed
            if (segment.frames == 0L) {
                writtenSegments.removeFirst()
            }
        }

        if (countedFramesPlayed - lastProgressLogFrame >= sampleRate * PROGRESS_LOG_SECONDS) {
            lastProgressLogFrame = countedFramesPlayed
            Log.i(
                TAG,
                "progress played=${countedFramesPlayed / sampleRate}s " +
                    "written=${countedFramesWritten / sampleRate}s queuedBytes=$queuedBytes " +
                    "segments=${writtenSegments.size} route=${routeSummary()} volume=$outputVolume"
            )
        }
    }

    private fun prepareAudioRouteLocked() {
        previousAudioMode = previousAudioMode ?: audioManager.mode
        previousSpeakerphoneOn = previousSpeakerphoneOn ?: audioManager.isSpeakerphoneOn
        audioManager.mode = AudioManager.MODE_IN_COMMUNICATION
        routeToSpeakerLocked()
    }

    private fun restoreAudibleOutputLocked() {
        audioManager.mode = AudioManager.MODE_IN_COMMUNICATION
        routeToSpeakerLocked()
        track?.setVolume(outputVolume)
        val currentTrack = track
        if (playbackStarted && currentTrack != null && currentTrack.playState != AudioTrack.PLAYSTATE_PLAYING) {
            try {
                currentTrack.play()
                Log.i(TAG, "resumed AudioTrack after focus/route change")
            } catch (ex: IllegalStateException) {
                Log.w(TAG, "AudioTrack resume failed: ${ex.message}")
            }
        }
    }

    private fun routeToSpeakerLocked() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
            val speaker = audioManager.availableCommunicationDevices.firstOrNull {
                it.type == AudioDeviceInfo.TYPE_BUILTIN_SPEAKER
            }
            if (speaker != null && audioManager.communicationDevice?.id != speaker.id) {
                routedToCommunicationSpeaker = audioManager.setCommunicationDevice(speaker)
                Log.i(TAG, "setCommunicationDevice speaker=$routedToCommunicationSpeaker")
            }
        } else {
            audioManager.isSpeakerphoneOn = true
        }
    }

    private fun restoreAudioRouteLocked() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S && routedToCommunicationSpeaker) {
            audioManager.clearCommunicationDevice()
            routedToCommunicationSpeaker = false
        } else {
            previousSpeakerphoneOn?.let { audioManager.isSpeakerphoneOn = it }
        }
        previousAudioMode?.let { audioManager.mode = it }
        previousSpeakerphoneOn = null
        previousAudioMode = null
    }

    private fun routeSummary(): String {
        return if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
            audioManager.communicationDevice?.type?.toString() ?: "none"
        } else {
            "speaker=${audioManager.isSpeakerphoneOn}"
        }
    }

    private data class WrittenSegment(
        var frames: Long,
        val counted: Boolean,
    )

    private companion object {
        private const val TAG = "PayaboVoicePcmPlayer"
        private const val BYTES_PER_FRAME = 2
        private const val KEEP_ALIVE_CHUNK_MS = 20
        private const val PROGRESS_LOG_SECONDS = 5
    }
}
