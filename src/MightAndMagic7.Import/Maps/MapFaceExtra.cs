namespace MightAndMagic7.Import.Maps;

/// <summary>One face's extra data, referenced by the face's extra id.</summary>
/// <remarks>
/// The extras array is where an indoor face's texture offsets, clickable-object number and event come
/// from; the face record itself only holds the index. The name stored beside each extra is loaded by
/// the donor and then discarded, so it is kept here for completeness but has no effect.
/// </remarks>
/// <param name="Index">The extra's index in the level's extras array.</param>
/// <param name="FaceId">The face identity the extra carries.</param>
/// <param name="AdditionalBitmapId">The extra bitmap's id; the donor always resolves it to none.</param>
/// <param name="TextureDeltaU">U offset applied when drawing the face's texture.</param>
/// <param name="TextureDeltaV">V offset applied when drawing the face's texture.</param>
/// <param name="CogNumber">The face's clickable-object number, or 0 when it is not clickable.</param>
/// <param name="EventId">The event the face raises, or 0 when it raises none.</param>
/// <param name="AdditionalTexture">The name stored beside the extra, which the donor discards.</param>
public sealed record MapFaceExtra(
    int Index,
    int FaceId,
    int AdditionalBitmapId,
    int TextureDeltaU,
    int TextureDeltaV,
    int CogNumber,
    int EventId,
    string AdditionalTexture);
