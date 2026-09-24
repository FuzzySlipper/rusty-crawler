namespace MightAndMagic7.Import.Maps;

/// <summary>A face's plane, normalized to unit-scale floats.</summary>
/// <remarks>
/// The plane equation is <c>dot(normal, x) + distance == 0</c>. Indoor faces store the usable plane as
/// 16.16 fixed point and the decoder divides by 65536; outdoor faces store it the same way. The float
/// copy both formats also carry is not used, so a face's plane here is never two slightly different
/// answers to the same question.
/// </remarks>
/// <param name="NormalX">X component of the normal.</param>
/// <param name="NormalY">Y component of the normal.</param>
/// <param name="NormalZ">Z component of the normal.</param>
/// <param name="Distance">Signed distance from the origin.</param>
public readonly record struct MapPlane(float NormalX, float NormalY, float NormalZ, float Distance);
