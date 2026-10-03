using System.Buffers.Binary;
using System.Numerics;

namespace PartyRpg.Kit.Scene;

/// <summary>A range of a mesh part's indices drawn with one of its place's materials.</summary>
/// <param name="Material">The material's index in the place's material list.</param>
/// <param name="Start">The first index, relative to the part's own first index.</param>
/// <param name="Count">How many indices.</param>
public readonly record struct RenderMeshGroup(int Material, int Start, int Count);

/// <summary>
/// One contiguous range of a place's render mesh: static geometry, or the faces one door moves.
/// </summary>
/// <param name="Door">The index of the door that moves it, or null for static geometry.</param>
/// <param name="VertexStart">The part's first vertex.</param>
/// <param name="VertexCount">How many vertices it has.</param>
/// <param name="IndexStart">The part's first index; its indices count from its own first vertex.</param>
/// <param name="IndexCount">How many indices it has.</param>
/// <param name="Groups">Its material ranges.</param>
public sealed record RenderMeshPart(int? Door, int VertexStart, int VertexCount, int IndexStart, int IndexCount, IReadOnlyList<RenderMeshGroup> Groups);

/// <summary>
/// A place's render geometry as content stores it: positions, normals, texture coordinates and door travel per vertex,
/// in the Engine's axes, divided into parts drawn independently.
/// </summary>
/// <remarks>
/// <para>
/// The binary form is this kit's own document, written offline by whatever produced the content and read here once per
/// place entered. Little-endian: the eight bytes <c>PRMESH01</c>; four u32 counts — vertices, indices, parts, groups;
/// then f32 positions (three per vertex), normals (three), texture coordinates (two) and door travel (three); u32
/// indices; each part as i32 door (-1 for none), u32 vertex start, vertex count, index start, index count, first group
/// and group count; each group as u32 material, start and count.
/// </para>
/// <para>
/// A vertex's travel is where a door carries it when the door is closed: a closed door's corner stands at its position
/// plus its travel, an open one at its position, which is the pairing the place's collision uses for the same corner.
/// </para>
/// </remarks>
public sealed class RenderMesh
{
    /// <summary>The document's first eight bytes.</summary>
    public static ReadOnlySpan<byte> Magic => "PRMESH01"u8;

    private RenderMesh(Vector3[] positions, Vector3[] normals, Vector2[] uvs, Vector3[] travel, uint[] indices, RenderMeshPart[] parts)
    {
        Positions = positions;
        Normals = normals;
        Uvs = uvs;
        Travel = travel;
        Indices = indices;
        Parts = parts;
    }

    /// <summary>Every vertex's position at rest.</summary>
    public ReadOnlyMemory<Vector3> Positions { get; }

    /// <summary>Every vertex's normal.</summary>
    public ReadOnlyMemory<Vector3> Normals { get; }

    /// <summary>Every vertex's texture coordinate.</summary>
    public ReadOnlyMemory<Vector2> Uvs { get; }

    /// <summary>Every vertex's door travel; zero for a vertex no door moves.</summary>
    public ReadOnlyMemory<Vector3> Travel { get; }

    /// <summary>Every part's indices, each counted from its own part's first vertex.</summary>
    public ReadOnlyMemory<uint> Indices { get; }

    /// <summary>The parts, static geometry first.</summary>
    public IReadOnlyList<RenderMeshPart> Parts { get; }

    /// <summary>How many triangles the mesh holds.</summary>
    public int Triangles => Indices.Length / 3;

    /// <summary>Reads the binary form, refusing bytes that are not one whole document.</summary>
    /// <param name="bytes">The document's bytes.</param>
    /// <param name="name">Where they came from, for a refusal.</param>
    /// <exception cref="InvalidDataException">The bytes are not a whole, consistent document.</exception>
    public static RenderMesh Read(ReadOnlyMemory<byte> data, string name)
    {
        ReadOnlySpan<byte> bytes = data.Span;
        if (bytes.Length < 24 || !bytes[..8].SequenceEqual(Magic))
            throw new InvalidDataException($"'{name}' is not a render mesh: it does not begin with {System.Text.Encoding.ASCII.GetString(Magic)}.");
        int at = 8;
        uint U32(ReadOnlySpan<byte> source) { uint value = BinaryPrimitives.ReadUInt32LittleEndian(source[at..]); at += 4; return value; }
        float F32(ReadOnlySpan<byte> source) { float value = BinaryPrimitives.ReadSingleLittleEndian(source[at..]); at += 4; return value; }
        int vertices = checked((int)U32(bytes)), indexCount = checked((int)U32(bytes)), partCount = checked((int)U32(bytes)), groupCount = checked((int)U32(bytes));
        long expected = 24L + (vertices * 44L) + (indexCount * 4L) + (partCount * 28L) + (groupCount * 12L);
        if (bytes.Length != expected)
            throw new InvalidDataException($"'{name}' declares {vertices} vertices, {indexCount} indices, {partCount} parts and {groupCount} groups, which take {expected} bytes, and holds {bytes.Length}.");
        Vector3[] Triples()
        {
            Vector3[] values = new Vector3[vertices];
            for (int index = 0; index < vertices; index++) values[index] = new Vector3(F32(data.Span), F32(data.Span), F32(data.Span));
            return values;
        }

        Vector3[] positions = Triples();
        Vector3[] normals = Triples();
        Vector2[] uvs = new Vector2[vertices];
        for (int index = 0; index < vertices; index++) uvs[index] = new Vector2(F32(bytes), F32(bytes));
        Vector3[] travel = Triples();
        uint[] indices = new uint[indexCount];
        for (int index = 0; index < indexCount; index++) indices[index] = U32(bytes);
        (int Door, int VertexStart, int VertexCount, int IndexStart, int IndexCount, int FirstGroup, int Groups)[] rows = new (int, int, int, int, int, int, int)[partCount];
        for (int index = 0; index < partCount; index++)
        {
            int door = (int)U32(bytes);
            rows[index] = (door, (int)U32(bytes), (int)U32(bytes), (int)U32(bytes), (int)U32(bytes), (int)U32(bytes), (int)U32(bytes));
        }

        RenderMeshGroup[] groups = new RenderMeshGroup[groupCount];
        for (int index = 0; index < groupCount; index++) groups[index] = new RenderMeshGroup((int)U32(bytes), (int)U32(bytes), (int)U32(bytes));
        RenderMeshPart[] parts = new RenderMeshPart[partCount];
        for (int index = 0; index < partCount; index++)
        {
            var row = rows[index];
            if (row.VertexStart < 0 || row.VertexCount < 0 || row.VertexStart + row.VertexCount > vertices
                || row.IndexStart < 0 || row.IndexCount < 0 || row.IndexStart + row.IndexCount > indexCount
                || row.FirstGroup < 0 || row.Groups < 0 || row.FirstGroup + row.Groups > groupCount)
            {
                throw new InvalidDataException($"'{name}' part {index} lies outside the mesh it belongs to.");
            }

            for (int at2 = row.IndexStart; at2 < row.IndexStart + row.IndexCount; at2++)
            {
                if (indices[at2] >= row.VertexCount) throw new InvalidDataException($"'{name}' part {index} names a vertex it does not hold.");
            }

            parts[index] = new RenderMeshPart(row.Door < 0 ? null : row.Door, row.VertexStart, row.VertexCount, row.IndexStart, row.IndexCount,
                groups.AsSpan(row.FirstGroup, row.Groups).ToArray());
        }

        return new RenderMesh(positions, normals, uvs, travel, indices, parts);
    }
}
