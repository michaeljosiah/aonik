// AudioWorklet that buffers Float32 mic samples into fixed-size chunks and converts to 16-bit
// PCM, then posts each chunk to the main thread as a transferable ArrayBuffer.
//
// This is loaded with `audioContext.audioWorklet.addModule('/audio/voice-recorder-worklet.js')`
// and registered as the 'voice-recorder' processor. Sourced from
// `samples/Voxa.Samples.AspNetServer/wwwroot/recorder-worklet.js` in the Voxa repo — kept in sync
// because the admin "Test STT" / "Live pipeline test" cards need the same wire format that
// `WebSocketAudioSource` consumes.
class VoiceRecorderProcessor extends AudioWorkletProcessor {
  constructor(opts) {
    super();
    // Default 800 samples = 50 ms at 16 kHz, 33 ms at 24 kHz. Tunable via processorOptions.
    this.chunkSamples = opts?.processorOptions?.chunkSamples ?? 800;
    this.buffer = new Float32Array(this.chunkSamples);
    this.bufferIndex = 0;
  }

  process(inputs) {
    const input = inputs[0];
    if (!input || input.length === 0) return true;
    const channel = input[0];
    if (!channel) return true;

    for (let i = 0; i < channel.length; i++) {
      this.buffer[this.bufferIndex++] = channel[i];
      if (this.bufferIndex >= this.chunkSamples) {
        const int16 = new Int16Array(this.chunkSamples);
        for (let j = 0; j < this.chunkSamples; j++) {
          const s = Math.max(-1, Math.min(1, this.buffer[j]));
          int16[j] = s < 0 ? s * 0x8000 : s * 0x7fff;
        }
        // Transfer the underlying buffer — zero-copy across the worklet/main-thread boundary.
        this.port.postMessage(int16.buffer, [int16.buffer]);
        this.bufferIndex = 0;
      }
    }
    return true;
  }
}

registerProcessor('voice-recorder', VoiceRecorderProcessor);
