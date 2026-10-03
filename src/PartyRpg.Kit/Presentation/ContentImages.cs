using Rusty.Engine;

namespace PartyRpg.Kit.Presentation;

/// <summary>
/// The images a game draws something with — its portraits, its items' pictures — granted to the DOM companion through
/// the Engine: an image is opened once and its same-origin URL put in the projection, so the panel shows the picture
/// and never receives image bytes.
/// </summary>
/// <remarks>
/// <para>
/// Which image a key is drawn with is the game's answer (<paramref name="pathOf"/> in the constructor): a content
/// path beside its packs, or null for a key it draws with none. An image the Engine refuses is said once in
/// <see cref="Notes"/> and on the Engine's diagnostics, and the key is published without a URL, so the panel names
/// the thing rather than showing a broken picture; a missing picture changes nothing the thing stands for.
/// </para>
/// <para>
/// Each granted image is kept until the session releases this owner, which revokes its URL.
/// </para>
/// </remarks>
public sealed class ContentImages : IDisposable
{
    private readonly IEngineContext _engine;
    private readonly Func<string, string?> _pathOf;
    private readonly string _source;
    private readonly Dictionary<string, (UiImage? Image, string Url)> _granted = new(StringComparer.Ordinal);
    private readonly List<string> _notes = [];
    private bool _disposed;

    /// <summary>Creates the owner. Nothing is opened until a key is first asked for.</summary>
    /// <param name="engine">The Engine whose content and interface services open and grant the images.</param>
    /// <param name="pathOf">The content path a key is drawn with, or null when the game draws it with none.</param>
    /// <param name="source">What the images are of, as a note and the Engine's diagnostics name them (<c>portraits</c>).</param>
    public ContentImages(IEngineContext engine, Func<string, string?> pathOf, string source)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _pathOf = pathOf ?? throw new ArgumentNullException(nameof(pathOf));
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        _source = source;
    }

    /// <summary>What could not be granted, each said once.</summary>
    public IReadOnlyList<string> Notes => _notes;

    /// <summary>The URL a key's image is served at, or empty when it has none or the Engine refused it.</summary>
    /// <param name="key">The identity the image is drawn for: a portrait, an item definition.</param>
    public string Url(string key)
    {
        if (_disposed || string.IsNullOrEmpty(key)) return string.Empty;
        if (_granted.TryGetValue(key, out var granted)) return granted.Url;
        UiImage? image = null;
        string url = string.Empty;
        if (_pathOf(key) is { } path)
        {
            try
            {
                using ContentReference reference = _engine.Content.OpenReference(new ContentOpenRequest(path));
                image = _engine.Ui.OpenImage(new UiImageRequest(reference));
                url = image.Url();
            }
            catch (EngineCallException refused)
            {
                image?.Dispose();
                image = null;
                url = string.Empty;
                Note($"The {_source} image '{key}' could not be shown from '{path}': {refused.Message}");
            }
        }

        _granted[key] = (image, url);
        return url;
    }

    /// <summary>Revokes every granted image; one the Engine will not revoke is said once and the rest are still revoked.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach ((UiImage? image, _) in _granted.Values)
        {
            try
            {
                image?.Dispose();
            }
            catch (EngineCallException refused)
            {
                Note($"A {_source} image could not be revoked: {refused.Message}");
            }
        }

        _granted.Clear();
    }

    private void Note(string note)
    {
        if (_notes.Contains(note, StringComparer.Ordinal)) return;
        _notes.Add(note);
        try
        {
            _engine.Diagnostics?.Publish(new DiagnosticsPublishRequest(
                DiagnosticsSeverity.Warning, DiagnosticsDisposition.Degraded, Source: _source, Code: "image-note", Message: note, Correlation: string.Empty));
        }
        catch (EngineCallException)
        {
            // The note is kept in Notes; a diagnostics sink that refuses it must not stop the projection it describes.
        }
    }
}
