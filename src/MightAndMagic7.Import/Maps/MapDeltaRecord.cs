namespace MightAndMagic7.Import.Maps;

/// <summary>Reads the records both delta families carry: the sprite objects standing in a map and its chests.</summary>
/// <remarks>
/// An indoor and an outdoor delta are otherwise different payloads — one holds a door array and a reveal
/// bitfield, the other revealed cells and placed models — but they store the same sprite-object and chest
/// arrays between their flags and their map variables, at the same widths and with the same field
/// offsets, so their record layouts live here once. The field offsets are the ones the donor's snapshot
/// structures declare, and every field a record carries is consumed even when it is not kept, because the
/// record's width is what keeps the walk aligned.
/// </remarks>
internal static class MapDeltaRecord
{
    /// <summary>Size of a chest record, which is its two-word header, 140 items, and a 140-cell grid.</summary>
    internal const int ChestSize = 5324;

    /// <summary>Size of an actor record, which is the donor's own snapshot width.</summary>
    internal const int ActorSize = 0x344;

    /// <summary>Size of a sprite object record.</summary>
    internal const int SpriteObjectSize = 0x70;

    /// <summary>How many items a chest record has room for.</summary>
    internal const int ChestItemCount = 140;

    /// <summary>Size of one item structure inside a chest record.</summary>
    private const int ChestItemSize = 0x24;

    /// <summary>Where a chest's item array starts: after its type id and its flags.</summary>
    private const int ChestItemsOffset = 0x04;

    /// <summary>Where the identifier sits inside an item structure.</summary>
    private const int ItemIdOffset = 0x00;

    // Field offsets inside Actor_MM7 (OpenEnroth src/Engine/Snapshots/EntitySnapshots.h:764-809).
    private const int ActorNameWidth = 32;
    private const int ActorNpcIdOffset = 0x20;
    private const int ActorAttributesOffset = 0x24;
    private const int ActorHitPointsOffset = 0x28;
    private const int ActorMonsterIdOffset = 0x60;
    private const int ActorPositionOffset = 0x8E;
    private const int ActorYawAngleOffset = 0x9A;
    private const int ActorSectorIdOffset = 0x9E;
    private const int ActorGroupOffset = 0x2E8;
    private const int ActorUniqueNameIndexOffset = 0x334;

    // Field offsets inside SpriteObject_MM7.
    private const int SpriteIdOffset = 0x00;
    private const int SpriteObjectDescIdOffset = 0x02;
    private const int SpritePositionOffset = 0x04;
    private const int SpriteYawAngleOffset = 0x16;
    private const int SpriteAttributesOffset = 0x1A;
    private const int SpriteSectorIdOffset = 0x1C;
    private const int SpriteContainingItemOffset = 0x24;

    /// <summary>Reads every chest record the delta carries, with the items each one holds.</summary>
    /// <remarks>
    /// The grid that says where each item sits in the chest's nine-by-nine face is consumed and not kept:
    /// it is a layout for a window this product has no transfer surface for, and what a search yields is
    /// the chest's contents rather than a picture of them. An empty slot is a slot with no item, which is
    /// left out of the record's items instead of being kept as a hole.
    /// </remarks>
    /// <param name="records">The chest array, exactly as many records as the delta declared.</param>
    /// <param name="count">How many chest records the array holds.</param>
    internal static MapChest[] Chests(ReadOnlySpan<byte> records, int count)
    {
        MapChest[] chests = new MapChest[count];
        for (int index = 0; index < count; index++)
        {
            ReadOnlySpan<byte> record = records.Slice(index * ChestSize, ChestSize);
            List<MapChestItem> items = [];
            for (int slot = 0; slot < ChestItemCount; slot++)
            {
                int itemId = MapRecord.Int32(record, ChestItemsOffset + (slot * ChestItemSize) + ItemIdOffset);
                if (itemId != 0) items.Add(new MapChestItem(slot, itemId));
            }

            chests[index] = new MapChest(
                index,
                MapRecord.UInt16(record, 0x00),
                MapRecord.UInt16(record, 0x02),
                items);
        }

        return chests;
    }

    /// <summary>Reads every actor record the delta carries.</summary>
    /// <remarks>
    /// Every field the walk steps over is consumed by the record's fixed width; the ones read here are the
    /// ones that say who the actor is and where it stands. The offsets are the donor's snapshot layout, and
    /// the identity fields are read as signed values because that is how the record stores them — a zero
    /// identity means "no NPC" and "no monster row" respectively, which is a fact rather than a missing one.
    /// </remarks>
    /// <remarks>
    /// <b>The monster row is the one inside the embedded monster info.</b> An actor record carries both a
    /// whole <c>MonsterInfo_MM7</c> block and, after it, a descriptor id
    /// (<c>OpenEnroth src/Engine/Snapshots/EntitySnapshots.h:764-809</c>): the block's own <c>id</c> is the
    /// monster row the actor is, and the descriptor names the model it is drawn with. Every person the
    /// shipped maps place states a peasant row there — the donor's own reading of a person standing in a
    /// level is an actor with a monster row
    /// (<c>src/Engine/Objects/MonsterEnumFunctions.h:56-58</c>, <c>isPeasant</c>) — while the descriptor is
    /// zero on the shipped deltas for people and beasts alike, which is why the row is read from the block
    /// and not from the field after it.
    /// </remarks>
    /// <param name="records">The actor array, exactly as many records as the delta declared.</param>
    /// <param name="count">How many actor records the array holds.</param>
    internal static MapActor[] Actors(ReadOnlySpan<byte> records, int count)
    {
        MapActor[] actors = new MapActor[count];
        for (int index = 0; index < count; index++)
        {
            ReadOnlySpan<byte> record = records.Slice(index * ActorSize, ActorSize);
            actors[index] = new MapActor(
                index,
                MapRecord.Text(record, 0, ActorNameWidth),
                MapRecord.Int16(record, ActorNpcIdOffset),
                MapRecord.Int16(record, ActorMonsterIdOffset),
                MapRecord.Int16(record, ActorHitPointsOffset),
                MapRecord.Int32(record, ActorAttributesOffset),
                MapRecord.ShortPoint(record, ActorPositionOffset),
                MapRecord.UInt16(record, ActorYawAngleOffset),
                MapRecord.Int16(record, ActorSectorIdOffset),
                MapRecord.Int32(record, ActorGroupOffset),
                MapRecord.Int32(record, ActorUniqueNameIndexOffset));
        }

        return actors;
    }

    /// <summary>Reads every sprite object the delta carries.</summary>
    /// <param name="records">The sprite-object array, exactly as many records as the delta declared.</param>
    /// <param name="count">How many sprite-object records the array holds.</param>
    internal static MapSpriteObject[] SpriteObjects(ReadOnlySpan<byte> records, int count)
    {
        MapSpriteObject[] objects = new MapSpriteObject[count];
        for (int index = 0; index < count; index++)
        {
            ReadOnlySpan<byte> record = records.Slice(index * SpriteObjectSize, SpriteObjectSize);

            // The containing item is a whole item structure inside the record; only its identifier decides
            // whether the object holds anything at all, which is the one thing a pile of items is.
            objects[index] = new MapSpriteObject(
                index,
                MapRecord.UInt16(record, SpriteIdOffset),
                MapRecord.UInt16(record, SpriteObjectDescIdOffset),
                MapRecord.Point(record, SpritePositionOffset),
                MapRecord.UInt16(record, SpriteYawAngleOffset),
                MapRecord.UInt16(record, SpriteAttributesOffset),
                MapRecord.Int16(record, SpriteSectorIdOffset),
                MapRecord.Int32(record, SpriteContainingItemOffset));
        }

        return objects;
    }
}
