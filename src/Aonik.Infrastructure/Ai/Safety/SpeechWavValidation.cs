using System.Buffers.Binary;

namespace Aonik.Infrastructure.Ai.Safety;

/// <summary>Deterministic checks for complete PCM narration before asking a model to transcribe it.</summary>
internal static class SpeechWavValidation
{
    internal static void Validate(ReadOnlySpan<byte> audio)
    {
        if (audio.Length < 44 || !audio[..4].SequenceEqual("RIFF"u8) || !audio.Slice(8, 4).SequenceEqual("WAVE"u8)
            || BinaryPrimitives.ReadUInt32LittleEndian(audio.Slice(4, 4)) != audio.Length - 8)
            throw new ArgumentException("Speech requires a complete WAV container.");
        int offset = 12;
        ushort alignment = 0;
        bool hasData = false, audible = false;
        while (offset <= audio.Length - 8)
        {
            var kind = audio.Slice(offset, 4);
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(audio.Slice(offset + 4, 4));
            if (size > audio.Length - offset - 8) throw new ArgumentException("Speech WAV contains a truncated chunk.");
            var chunk = audio.Slice(offset + 8, (int)size);
            if (kind.SequenceEqual("fmt "u8))
            {
                if (alignment != 0 || size < 16 || BinaryPrimitives.ReadUInt16LittleEndian(chunk) != 1
                    || BinaryPrimitives.ReadUInt16LittleEndian(chunk.Slice(14, 2)) != 16)
                    throw new ArgumentException("Speech WAV requires 16-bit PCM audio.");
                ushort channels = BinaryPrimitives.ReadUInt16LittleEndian(chunk.Slice(2, 2));
                alignment = BinaryPrimitives.ReadUInt16LittleEndian(chunk.Slice(12, 2));
                if (channels == 0 || alignment != channels * 2 || BinaryPrimitives.ReadUInt32LittleEndian(chunk.Slice(4, 4)) == 0)
                    throw new ArgumentException("Speech WAV has an invalid PCM format.");
            }
            if (kind.SequenceEqual("data"u8))
            {
                if (hasData || alignment == 0 || size == 0 || size % alignment != 0)
                    throw new ArgumentException("Speech WAV requires complete PCM frames.");
                hasData = true;
                for (int i = 0; i < chunk.Length; i += 2)
                    audible |= BinaryPrimitives.ReadInt16LittleEndian(chunk.Slice(i, 2)) != 0;
            }
            offset += 8 + (int)size + (int)(size % 2);
        }
        if (offset != audio.Length || !hasData || !audible)
            throw new ArgumentException("Speech WAV is empty, silent or incomplete.");
    }
}
