# OpenAI speech safety

The optional speech transport sends the complete generated clip to the centrally routed OpenAI audio model. The composite requires transcription, transcript moderation and audio classification to succeed. It merges the highest category score from both classification legs. Missing routes, provider consent, credentials, incomplete answers and transport failures refuse delivery.

Enable transport registration with `Safety:OpenAiSpeech:Enabled=true` only after development evaluation. This switch does not enable a safety policy or seed any model. Configure active catalogue models and central routes for `safety-transcribe-speech`, `safety-classify-speech` and `safety-classify-text`. The audio routes require audio-input Chat Completions models; the text route uses moderation. Provider must be `openai` and must appear in the child's current consent terms. Credentials use the existing tenant-first `Ai.OpenAI.ApiKey` setting.

`POST /safety/screen` accepts modality `speech`, layer `output`, and inline `data:audio/wav;base64,...` or `data:audio/mpeg;base64,...`. Remote URLs are refused. The adapter accepts at most 8 MiB decoded audio and never samples a clip. The existing authenticated guardian and safety-consent checks apply. Decisions bind to the decoded audio hash, and younger-band output still needs the existing parent review before delivery.

Transcription records a started, completed or failed AiRun against the actual routed model, with the `speech-transcript-v1` prompt reference. Classification records `speech-safety-v1`. Audio and transcript content are not written to these audit references.

The audio classifier is a prompted model, not a calibrated moderation endpoint. Transport and refusal tests do not establish classification quality. Before enabling child-facing narration, evaluate benign narration, distressing delivery with benign words, harmful spoken content, silence, truncated audio, and adversarial spoken instructions against the configured model and thresholds. Retain the existing refusal and parent-review protections. No production policy is enabled by this change.

API transport reference: [OpenAI audio Chat Completions guide](https://developers.openai.com/api/docs/guides/audio-chat-completions).

## Explicit local evaluation

`OpenAISpeechLiveEvaluationTests` is skipped in ordinary test runs. To run the actual provider against a local corpus, set `AONIK_SPEECH_EVAL_ENABLED=true`, `AONIK_SPEECH_EVAL_API_KEY`, `AONIK_SPEECH_EVAL_MODEL`, `AONIK_SPEECH_EVAL_MANIFEST` and `AONIK_SPEECH_EVAL_REPORT`, then filter the infrastructure test project to that class. Credentials belong in process configuration, never the manifest or command arguments.

The JSON manifest contains `labelProvenance` and a `cases` array. Each case supplies `id`, local `path` (relative to the manifest), `contentType`, `band`, nullable `expectedTranscript`, `expectUnavailable`, `minimumScores` and `maximumScores`. Score objects map category names to the independently chosen bounds. The report records the model, prompt version, label provenance, content hashes, scores and outcomes; it excludes credentials, raw audio and transcript text. It never updates runtime routes or policies. This tests the actual provider adapter, not authenticated gateway routing or model quality by itself.

The initial fictional narration case passed against `gpt-audio-1.5`. A digital-silence case exposed a non-empty model transcript with low risk scores, so silence must not be left to the prompt alone. WAV input now requires a complete 16-bit PCM container with nonzero samples before either provider call. Silent, truncated and malformed WAV files fail before network access. Nonzero samples are not proof of intelligible speech; noisy audio, performance, harmful content and adversarial cases still require the independently reviewed corpus. MP3 has no equivalent local silence detector in this adapter and remains part of the outstanding evaluation. Kidz narration uses WAV.
