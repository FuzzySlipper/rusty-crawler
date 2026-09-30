using System.Globalization;
using System.Text.Json;

namespace PartyRpg.Kit.Content;

/// <summary>
/// One adjustable value a ruleset declares: its name, what it means, the range it may take, and what it is
/// when no tuning pack states it.
/// </summary>
/// <remarks>
/// A handle is how an adjustable value is discoverable: every one a ruleset reads is declared as a handle, so
/// the set of what a tuning pack may state is a list in code rather than a scattering of constants, and a
/// value outside the handle's range is refused where the content is loaded rather than met in play.
/// </remarks>
/// <param name="Id">The name a tuning pack states the value under.</param>
/// <param name="Default">What the value is when no tuning pack states it.</param>
/// <param name="Minimum">The least the value may be.</param>
/// <param name="Maximum">The most the value may be.</param>
/// <param name="Meaning">What the value decides, in the ruleset's own words.</param>
/// <param name="Whole">Whether the value must be a whole number.</param>
public sealed record TuningHandle(string Id, double Default, double Minimum, double Maximum, string Meaning, bool Whole = true)
{
    /// <summary>Whether a value is one this handle may take.</summary>
    /// <param name="value">The value to judge.</param>
    public bool Admits(double value) =>
        double.IsFinite(value) && value >= Minimum && value <= Maximum && (!Whole || value == Math.Floor(value));
}

/// <summary>
/// The values a ruleset's tuning handles take for one session: what the selected tuning pack states, and each
/// handle's default where it states nothing.
/// </summary>
/// <remarks>
/// <para>
/// A tuning pack is a content pack of the tuning kind whose documents hold entries of kind
/// <see cref="DefinitionKind"/>, each an <c>id</c> naming a handle and a <c>value</c>. The profile is read once,
/// when a session is composed, and every problem in it is named at once: a value for no handle the ruleset
/// declares, a value that is not a number, and a value outside its handle's range. Changing a value needs no
/// rebuild, only the pack.
/// </para>
/// <para>
/// Values are read here and nowhere else: a rule is handed the profile and asks it for a handle, so where a
/// value comes from — the pack or the handle's default — is one answer rather than one per rule.
/// </para>
/// </remarks>
public sealed class TuningProfile
{
    /// <summary>The definition kind a tuning pack's entries are declared under.</summary>
    public const string DefinitionKind = "tuning";

    /// <summary>The entry field that holds a tuning value.</summary>
    public const string ValueField = "value";

    private readonly Dictionary<string, double> _values;

    private TuningProfile(IReadOnlyList<TuningHandle> handles, Dictionary<string, double> values)
    {
        Handles = handles;
        _values = values;
    }

    /// <summary>Every handle the profile answers for.</summary>
    public IReadOnlyList<TuningHandle> Handles { get; }

    /// <summary>A profile of every handle's default, which is what a session with no tuning pack plays.</summary>
    /// <param name="handles">The handles the ruleset declares.</param>
    public static TuningProfile Defaults(IReadOnlyList<TuningHandle> handles) => new(handles, new Dictionary<string, double>(StringComparer.Ordinal));

    /// <summary>Reads the values the selected content states for the given handles.</summary>
    /// <param name="catalog">The selected content, whose tuning entries are read; null reads every default.</param>
    /// <param name="handles">The handles the ruleset declares.</param>
    /// <returns>The profile.</returns>
    /// <exception cref="ContentValidationException">A stated value is for no handle, not a number, or out of its handle's range; every problem is named.</exception>
    public static TuningProfile Read(ContentCatalog? catalog, IReadOnlyList<TuningHandle> handles)
    {
        ArgumentNullException.ThrowIfNull(handles);
        Dictionary<string, TuningHandle> known = handles.ToDictionary(handle => handle.Id, StringComparer.Ordinal);
        Dictionary<string, double> values = new(StringComparer.Ordinal);
        if (catalog is null) return new TuningProfile(handles, values);

        List<ContentValidationIssue> issues = [];
        foreach ((LoadedPack pack, ContentDocument document, ContentEntry entry) in catalog.Entries(DefinitionKind))
        {
            if (!known.TryGetValue(entry.Id, out TuningHandle? handle))
            {
                issues.Add(new ContentValidationIssue(
                    "tuning-unknown",
                    $"tuning states '{entry.Id}', which is no value this ruleset declares; it declares {string.Join(", ", handles.Select(candidate => candidate.Id))}.",
                    pack.PackId,
                    document.DocumentId));
                continue;
            }

            if (!entry.Payload.TryGetProperty(ValueField, out JsonElement stated) || stated.ValueKind != JsonValueKind.Number)
            {
                issues.Add(new ContentValidationIssue(
                    "tuning-unreadable",
                    $"tuning '{handle.Id}' states no number as its value.",
                    pack.PackId,
                    document.DocumentId));
                continue;
            }

            double value = stated.GetDouble();
            if (!handle.Admits(value))
            {
                issues.Add(new ContentValidationIssue(
                    "tuning-out-of-range",
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"tuning '{handle.Id}' states {value}, and it takes {(handle.Whole ? "a whole number " : string.Empty)}from {handle.Minimum} to {handle.Maximum} ({handle.Meaning})."),
                    pack.PackId,
                    document.DocumentId));
                continue;
            }

            values[handle.Id] = value;
        }

        if (issues.Count > 0)
        {
            throw new ContentValidationException($"The tuning cannot be read: {issues[0].Message}", issues);
        }

        return new TuningProfile(handles, values);
    }

    /// <summary>The value a handle takes in this profile.</summary>
    /// <param name="handle">The handle to read.</param>
    /// <exception cref="ArgumentException">The handle is not one this profile was read for.</exception>
    public double this[TuningHandle handle]
    {
        get
        {
            ArgumentNullException.ThrowIfNull(handle);
            if (!Handles.Contains(handle))
            {
                throw new ArgumentException($"Tuning '{handle.Id}' is not a handle this profile was read for.", nameof(handle));
            }

            return _values.TryGetValue(handle.Id, out double value) ? value : handle.Default;
        }
    }

    /// <summary>The value a whole-number handle takes in this profile.</summary>
    /// <param name="handle">The handle to read.</param>
    public int Whole(TuningHandle handle) => (int)this[handle];

    /// <summary>Whether the tuning pack stated a value for a handle, rather than the handle's default standing.</summary>
    /// <param name="handle">The handle to ask about.</param>
    public bool States(TuningHandle handle) => _values.ContainsKey(handle.Id);
}
