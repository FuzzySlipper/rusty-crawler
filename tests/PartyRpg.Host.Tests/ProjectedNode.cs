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
}
