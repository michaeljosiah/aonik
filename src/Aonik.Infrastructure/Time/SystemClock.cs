using Aonik.SharedKernel.Abstractions;

namespace Aonik.Infrastructure.Time;

public class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
