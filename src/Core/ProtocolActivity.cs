namespace PetTracker;

public sealed record ActivityFrameSample(DateTimeOffset Time, string Message, string Result, int Length, string Prefix);
public sealed record ProtocolActivityView(
    IReadOnlyDictionary<string, long> PetMessages,
    IReadOnlyDictionary<string, long> ActiveFrames,
    IReadOnlyDictionary<string, long> Outcomes,
    DateTimeOffset? CheckStarted,
    DateTimeOffset? CheckUntil,
    IReadOnlyList<ActivityFrameSample> Samples);
// Local edition records typed counters only. Raw packet samples stay disabled.
public sealed class ProtocolActivity
{
    readonly Dictionary<string, long> pets = new(), active = new(), outcomes = new();
    readonly List<ActivityFrameSample> samples = new();
    DateTimeOffset? started, until;
    static void Count(Dictionary<string, long> values, string key)
    {
        if (values.ContainsKey(key) || values.Count < 96)
            values[key] = values.GetValueOrDefault(key) + 1;
    }

    public void Start(DateTimeOffset now)
    {
        started = now;
        until = now.AddSeconds(90);
        samples.Clear();
    }

    public void Frame(byte op, byte family, bool bound)
    {
        if (family == 0x90)
            Count(pets, op.ToString("X2"));
        if (bound && family is 0x36 or 0x38 or 0x90)
            Count(active, $"{family:X2}:{op:X2}");
    }

    public void Observe(byte op, byte family, ReadOnlySpan<byte> body, string result, DateTimeOffset now)
    {
        bool allowed = family == 0x38 && op == 4 || family == 0x36 && op is 0x40 or 0x41 or 0x42 || family == 0x90 && op is 0x0d or 0x0e or 0x14;
        if (!allowed)
            return;
        Count(outcomes, result);
        // Local edition intentionally never retains raw packet byte samples.
    }

    public ProtocolActivityView View() => new(new Dictionary<string, long>(pets), new Dictionary<string, long>(active), new Dictionary<string, long>(outcomes), started, until, samples.ToArray());
}
