using Aonik.Platform.Contracts.Models.ContactEnquiries;

namespace Aonik.Infrastructure.Storage;

internal sealed class ContactImageOutputStream(int maximumBytes) : MemoryStream
{
    public override void Write(byte[] buffer, int offset, int count)
    {
        CheckLength(count);
        base.Write(buffer, offset, count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        CheckLength(buffer.Length);
        base.Write(buffer);
    }

    public override void WriteByte(byte value)
    {
        CheckLength(1);
        base.WriteByte(value);
    }

    public override void SetLength(long value)
    {
        if (value > maximumBytes)
            throw TooLarge();
        base.SetLength(value);
    }

    private void CheckLength(int count)
    {
        if (Position + count > maximumBytes)
            throw TooLarge();
    }

    private static ContactImageValidationException TooLarge() =>
        new("image_size", "The processed image is too large. Choose a smaller image.");
}
