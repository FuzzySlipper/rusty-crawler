using System.Reflection;
using Rusty.Engine;

namespace PartyRpg.Testing;

/// <summary>
/// A double for one Engine service interface that records every call the product makes, by method name and arguments,
/// and answers each with a fresh handle object of the type the method returns.
/// </summary>
/// <remarks>
/// The world view creates materials, meshes, appearances, lights and a camera and publishes snapshots; what a test
/// proves is what it asked of the Engine, not what the Engine drew. A method whose answer a test must state — the bytes
/// a content reference reads — is answered by the handler the test supplies; every other method answers a new handle
/// (or the type's default) and is recorded.
/// </remarks>
/// <typeparam name="T">The Engine service interface.</typeparam>
public class RecordingEngineService<T> : DispatchProxy where T : class
{
    private readonly List<(string Method, object?[] Arguments)> _calls = [];
    private Func<MethodInfo, object?[], (bool Answered, object? Value)>? _handler;
    private ulong _next = 1;

    /// <summary>Every call made, in order.</summary>
    public IReadOnlyList<(string Method, object?[] Arguments)> Calls => _calls;

    /// <summary>Creates the double.</summary>
    /// <param name="handler">Answers a method the test states the result of, or declines with false.</param>
    public static (T Service, RecordingEngineService<T> Recorder) Create(Func<MethodInfo, object?[], (bool, object?)>? handler = null)
    {
        T service = Create<T, RecordingEngineService<T>>();
        RecordingEngineService<T> recorder = (RecordingEngineService<T>)(object)service;
        recorder._handler = handler;
        return (service, recorder);
    }

    /// <summary>The calls made to one method.</summary>
    public IReadOnlyList<object?[]> CallsTo(string method) => [.. _calls.Where(call => call.Method == method).Select(call => call.Arguments)];

    /// <inheritdoc />
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        object?[] arguments = args ?? [];
        _calls.Add((targetMethod.Name, arguments));
        if (_handler?.Invoke(targetMethod, arguments) is { Answered: true } answer) return answer.Value;
        return Handle(targetMethod.ReturnType);
    }

    private object? Handle(Type type)
    {
        if (type == typeof(void)) return null;
        ulong id = _next++;
        if (type == typeof(Material)) return new Material(new MaterialHandle(id), static () => { });
        if (type == typeof(MeshResource)) return new MeshResource(new MeshResourceHandle(id), static () => { });
        if (type == typeof(Appearance)) return new Appearance(new AppearanceHandle(id), static () => { });
        if (type == typeof(Light)) return new Light(new LightHandle(id), static () => { });
        if (type == typeof(Camera)) return new Camera(new CameraHandle(id), static () => { });
        if (type == typeof(SpriteAtlas)) return new SpriteAtlas(new SpriteAtlasHandle(id), static () => { });
        if (type == typeof(ContentReference)) return new ContentReference(new ContentReferenceHandle(id), static () => { });
        if (type == typeof(RenderResourceInfo))
            return new RenderResourceInfo(new RenderResource(new RenderResourceHandle(id), static () => { }), RenderResourceKind.Texture, 0);
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }
}
