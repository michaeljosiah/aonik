# OpenAI speech safety

The optional speech transport sends the complete generated clip to the centrally routed OpenAI audio model. The composite requires transcription, transcript moderation and audio classification to succeed. It merges the highest category score from both classification legs. Missing routes, provider consent, credentials, incomplete answers and transport failures refuse delivery.

Enable transport registration with `Safety:OpenAiSpeech:Enabled=true` only after development evaluation. This switch does not enable a safety policy or seed any model. Configure active catalogue models and central routes for `safety-transcribe-speech`, `safety-classify-speech` and `safety-classify-text`. The audio routes require audio-input Chat Completions models; the text route uses moderation. Provider must be `openai` and must appear in the child's current consent terms. Credentials use the existing tenant-first `Ai.OpenAI.ApiKey` setting.

`POST /safety/screen` accepts modality `speech`, layer `output`, and inline `data:audio/wav;base64,...` or `data:audio/mpeg;base64,...`. Remote URLs are refused. The adapter accepts at most 8 MiB decoded audio and never samples a clip. The existing authenticated guardian and safety-consent checks apply. Decisions bind to the decoded audio hash, and younger-band output still needs the existing parent review before delivery.

Transcription records a started, completed or failed AiRun against the actual routed model, with the `speech-transcript-v1` prompt reference. Classification records `speech-safety-v1`. Audio and transcript content are not written to these audit references.

The audio classifier is a prompted model, not a calibrated moderation endpoint. Transport and refusal tests do not establish classification quality. Before enabling child-facing narration, evaluate benign narration, distressing delivery with benign words, harmful spoken content, silence, truncated audio, and adversarial spoken instructions against the configured model and thresholds. Retain the existing refusal and parent-review protections. No production policy is enabled by this change.

API transport reference: [OpenAI audio Chat Completions guide](https://developers.openai.com/api/docs/guides/audio-chat-completions).
