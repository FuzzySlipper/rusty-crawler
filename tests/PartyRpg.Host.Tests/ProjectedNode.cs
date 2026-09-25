using System.Text;
using Rusty.Engine;

namespace PartyRpg.Host.Tests;

/// <summary>
/// Navigates a published structured value by field name, so a host test asserts what the panel would
/// show rather than node offsets.
/// </summary>
/// <remarks>
/// The kit suite carries the same small reader for the channel it records directly. Sharing it would
/// mean a third project for forty lines, and the two suites read different things: the kit suite reads
/// a session's channel, this one reads what the product published to the engine's UI service.
/// </remarks>
internal readonly struct ProjectedNode(UiValue value, uint index)
{
    internal static ProjectedNode Of(UiValue value) => new(value, value.Root);

    internal ProjectedNode Field(string key)
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
    /// The projection publishes lists — what a place holds, what a counter's shelves carry — and a test
    /// that asserts a list must be able to reach its elements without counting offsets by hand.
    /// </remarks>
    /// <param name="position">The element's position, counted from zero.</param>
    /// <exception cref="InvalidOperationException">This node is not an array.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The position is outside the array.</exception>
    internal ProjectedNode Item(int position)
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
    internal int Length()
    {
        StructuredValueNode node = value.Nodes.Span[(int)index];
        if (node.Kind != StructuredValueKind.Array)
            throw new InvalidOperationException($"Projection node is {node.Kind}, not an array.");
        return (int)node.ChildCount;
    }

    internal string AsString()
    {
        StructuredValueNode node = value.Nodes.Span[(int)index];
        if (node.Kind != StructuredValueKind.String)
            throw new InvalidOperationException($"Projection node is {node.Kind}, not a string.");
        return Encoding.UTF8.GetString(value.Utf8.Span.Slice((int)node.TextOffset, (int)node.TextLen));
    }

    internal double AsNumber()
    {
        StructuredValueNode node = value.Nodes.Span[(int)index];
        if (node.Kind != StructuredValueKind.Number)
            throw new InvalidOperationException($"Projection node is {node.Kind}, not a number.");
        return node.NumberValue;
    }

    internal bool AsBoolean()
    {
        StructuredValueNode node = value.Nodes.Span[(int)index];
        if (node.Kind != StructuredValueKind.Bool)
            throw new InvalidOperationException($"Projection node is {node.Kind}, not a boolean.");
        return node.BoolValue != 0;
    }

    /// <summary>Returns one element of an array node, in the order the projection wrote it.</summary>
    internal ProjectedNode Element(int position)
    {
        StructuredValueNode node = value.Nodes.Span[(int)index];
        if (node.Kind != StructuredValueKind.Array)
            throw new InvalidOperationException($"Projection node is {node.Kind}, not an array.");
        if (position < 0 || position >= node.ChildCount)
            throw new ArgumentOutOfRangeException(nameof(position), position, $"The projected array holds {node.ChildCount} elements.");
        return new ProjectedNode(value, value.Edges.Span[(int)node.FirstEdge + position]);
    }

    /// <summary>How many elements an array node holds, or how many fields an object node carries.</summary>
    internal int Count() => (int)value.Nodes.Span[(int)index].ChildCount;
}
