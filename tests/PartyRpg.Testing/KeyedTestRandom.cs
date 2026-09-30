using System.Text;
using Rusty.Engine;

namespace PartyRpg.Testing;

/// <summary>The engine's keyed randomness, answered deterministically from the request alone.</summary>
/// <remarks>
/// Unlike the fixed answer <see cref="TestRandomService"/> gives, every key draws its own value here — a hash of
/// the seed, the scope and the key — so a resolution that draws many times (a spawn record's count and each of
/// its creatures' grades) spreads over its range the way the engine's own service does, and asking under the same
/// key again answers the same value. Every other operation is refused rather than faked.
/// </remarks>
public sealed class KeyedTestRandom : IRandomService
{
    /// <summary>Every scope and key a draw was asked under, in order.</summary>
    public List<string> Keys { get; } = [];

    public KeyedRngReceipt DrawKeyed(KeyedRngRequest request)
    {
        Keys.Add($"{request.Scope}|{request.Key}");
        ulong hash = 14695981039346656037UL ^ request.Seed;
        foreach (byte value in Encoding.UTF8.GetBytes($"{request.Scope}|{request.Key}"))
        {
            hash = (hash ^ value) * 1099511628211UL;
        }

        long span = request.Maximum - request.Minimum + 1;
        return new KeyedRngReceipt(request.Minimum + (long)(hash % (ulong)span));
    }

    public Lcg15Receipt DrawLcg15(Lcg15Request request) => throw new NotSupportedException("Keyed draws only.");

    public Rng CreateScoped(ScopedRngCreateRequest request) => throw new NotSupportedException("Keyed draws only.");

    public Rng ForkScoped(ScopedRngForkRequest request) => throw new NotSupportedException("Keyed draws only.");

    public RngValue NextU64(Rng stream) => throw new NotSupportedException("Keyed draws only.");

    public RngValue NextBoundedU32(ScopedRngBoundedRequest request) => throw new NotSupportedException("Keyed draws only.");

    public RngValue NextBool(Rng stream) => throw new NotSupportedException("Keyed draws only.");
}
