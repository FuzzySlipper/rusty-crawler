using System.Text;
using Rusty.Engine;

namespace PartyRpg.Kit.Presentation;

/// <summary>
/// Builds one safe structured UI value without retaining borrowed storage: every string is copied
/// into the value's own UTF-8 block, so a published projection never points at caller memory.
/// </summary>
public sealed class UiValueBuilder
{
    private readonly List<StructuredValueNode> _nodes = [];
    private readonly List<uint> _edges = [];
    private readonly List<byte> _utf8 = [];

    /// <summary>Adds a null value.</summary>
    public uint Null() => Add(StructuredValueKind.Null);

    /// <summary>Adds a boolean value.</summary>
    public uint Boolean(bool value) => Add(StructuredValueKind.Bool, boolValue: value ? 1u : 0u);

    /// <summary>Adds a numeric value.</summary>
    public uint Number(double value) => Add(StructuredValueKind.Number, numberValue: value);

    /// <summary>Adds a string value.</summary>
    public uint String(string value)
    {
        (uint offset, uint length) = Bytes(value);
        return Add(StructuredValueKind.String, textOffset: offset, textLength: length);
    }

    /// <summary>Adds an object whose fields are written in the order given.</summary>
    public uint Object(params (string Key, uint Value)[] fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        uint firstEdge = checked((uint)_edges.Count);
        foreach ((string key, uint value) in fields)
        {
            if (value >= (uint)_nodes.Count) throw new ArgumentOutOfRangeException(nameof(fields), "A field value must be built before the object that contains it.");
            (uint offset, uint length) = Bytes(key);
            StructuredValueNode node = _nodes[checked((int)value)];
            uint keyedValue = checked((uint)_nodes.Count);
            _nodes.Add(node with { KeyOffset = offset, KeyLen = length });
            _edges.Add(keyedValue);
        }

        return Add(StructuredValueKind.Object, firstEdge: firstEdge, childCount: checked((uint)fields.Length));
    }

    /// <summary>Adds an array whose elements are written in the order given.</summary>
    public uint Array(params uint[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        uint firstEdge = checked((uint)_edges.Count);
        foreach (uint value in values)
        {
            if (value >= (uint)_nodes.Count) throw new ArgumentOutOfRangeException(nameof(values), "An element must be built before the array that contains it.");
            _edges.Add(value);
        }

        return Add(StructuredValueKind.Array, firstEdge: firstEdge, childCount: checked((uint)values.Length));
    }

    /// <summary>Produces the value rooted at <paramref name="root"/>.</summary>
    public UiValue Build(uint root)
    {
        if (root >= (uint)_nodes.Count) throw new ArgumentOutOfRangeException(nameof(root));
        return new UiValue(_nodes.ToArray(), _edges.ToArray(), root, _utf8.ToArray());
    }

    private uint Add(
        StructuredValueKind kind,
        uint boolValue = 0,
        double numberValue = 0,
        uint textOffset = 0,
        uint textLength = 0,
        uint firstEdge = 0,
        uint childCount = 0)
    {
        uint index = checked((uint)_nodes.Count);
        _nodes.Add(new StructuredValueNode(kind, boolValue, numberValue, KeyOffset: 0, KeyLen: 0, textOffset, textLength, firstEdge, childCount));
        return index;
    }

    private (uint Offset, uint Length) Bytes(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        uint offset = checked((uint)_utf8.Count);
        _utf8.AddRange(bytes);
        return (offset, checked((uint)bytes.Length));
    }
}
