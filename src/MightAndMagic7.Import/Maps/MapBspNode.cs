namespace MightAndMagic7.Import.Maps;

/// <summary>One node of an indoor level's BSP tree.</summary>
/// <remarks>
/// The tree is a rendering accelerator: a consumer can enumerate every face of every sector without
/// it, and the node ordering is documented only by the donor. The records are decoded anyway because
/// the walk has to consume them, and keeping them costs nothing.
/// </remarks>
/// <param name="Index">The node's index in the level's node array.</param>
/// <param name="Front">The node in front of this one, or a negative value for a leaf.</param>
/// <param name="Back">The node behind this one, or a negative value for a leaf.</param>
/// <param name="FaceIdOffset">Offset of this node's faces into the level's face array.</param>
/// <param name="FaceCount">How many faces the node holds.</param>
public sealed record MapBspNode(int Index, short Front, short Back, short FaceIdOffset, short FaceCount);
