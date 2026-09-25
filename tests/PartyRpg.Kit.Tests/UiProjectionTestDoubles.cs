using System.Text;
using PartyRpg.Kit.Presentation;
using Rusty.Engine;

namespace PartyRpg.Kit.Tests;

/// <summary>Records the projections a session published, so a test can assert what the product shows.</summary>
internal sealed class RecordingUiProjectionChannel : IUiProjectionChannel
{
    private readonly List<UiValue> _published = [];

    /// <summary>How many projections were published.</summary>
    public int Count => _published.Count;

    /// <summary>The most recently published projection, as a navigable node.</summary>
    public ProjectedNode Latest() => new(_published[^1], _published[^1].Root);

    /// <inheritdoc />
    public void Publish(UiValue value) => _published.Add(value);

    /// <inheritdoc />
    public void Dispose()
    {
    }
}

/// <summary>Navigates one published structured value by field name, so tests assert content, not offsets.</summary>
internal readonly struct ProjectedNode(UiValue value, uint index)
{
    /// <summary>Returns the named field of an object node.</summary>
    public ProjectedNode Field(string key)
    {
        StructuredValueNode node = value.Nodes.Span[(int)index];
        if (node.Kind != StructuredValueKind.Object)
            throw new InvalidOperationException($"Projection node is {node.Kind}, not an object.");

        for (uint edge = node.FirstEdge; edge < node.FirstEdge + node.ChildCount; edge++)
        {
            uint child = value.Edges.Span[(int)edge];
            StructuredValueNode childNode = value.Nodes.Span[(int)child];
            string childKey = Encoding.UTF8.GetString(value.Utf8.Span.Slice((int)childNode.KeyOffset, (int)childNode.KeyLen));
            if (childKey == key) return new ProjectedNode(value, child);
        }

        throw new KeyNotFoundException($"Projection has no field '{key}'.");
    }

    /// <summary>Returns the element at one position of an array node.</summary>
    /// <remarks>
    /// The projection publishes lists — what a place holds, what a counter's shelves carry — and a test that
    /// asserts a list must be able to reach its elements without counting offsets by hand.
    /// </remarks>
    /// <param name="position">The element's position, counted from zero.</param>
    /// <exception cref="InvalidOperationException">This node is not an array.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The position is outside the array.</exception>
    public ProjectedNode Item(int position)
    {
        StructuredValueNode node = value.Nodes.Span[(int)index];
        if (node.Kind != StructuredValueKind.Array)
            throw new InvalidOperationException($"Projection node is {node.Kind}, not an array.");
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        if (position >= node.ChildCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(position),
                position,
                $"The array holds {node.ChildCount} element(s), so there is no element {position}.");
        }

        return new ProjectedNode(value, value.Edges.Span[(int)(node.FirstEdge + position)]);
    }

    /// <summary>How many elements an array node holds.</summary>
    /// <exception cref="InvalidOperationException">This node is not an array.</exception>
    public int Length()
    {
        StructuredValueNode node = value.Nodes.Span[(int)index];
        if (node.Kind != StructuredValueKind.Array)
            throw new InvalidOperationException($"Projection node is {node.Kind}, not an array.");
        return (int)node.ChildCount;
    }

    /// <summary>Reads a string node.</summary>
    public string AsString()
    {
        StructuredValueNode node = value.Nodes.Span[(int)index];
        if (node.Kind != StructuredValueKind.String)
            throw new InvalidOperationException($"Projection node is {node.Kind}, not a string.");
        return Encoding.UTF8.GetString(value.Utf8.Span.Slice((int)node.TextOffset, (int)node.TextLen));
    }

    /// <summary>Reads a boolean node.</summary>
    public bool AsBoolean()
    {
        StructuredValueNode node = value.Nodes.Span[(int)index];
        if (node.Kind != StructuredValueKind.Bool)
            throw new InvalidOperationException($"Projection node is {node.Kind}, not a boolean.");
        return node.BoolValue != 0;
    }

    /// <summary>Reads a numeric node.</summary>
    public double AsNumber()
    {
        StructuredValueNode node = value.Nodes.Span[(int)index];
        if (node.Kind != StructuredValueKind.Number)
            throw new InvalidOperationException($"Projection node is {node.Kind}, not a number.");
        return node.NumberValue;
    }
}
