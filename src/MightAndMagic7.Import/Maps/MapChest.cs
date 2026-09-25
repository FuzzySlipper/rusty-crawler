namespace MightAndMagic7.Import.Maps;

/// <summary>One item a chest record holds, as the record stores it.</summary>
/// <remarks>
/// The item is a reference and not an item: the record holds the same fixed-size item structure the
/// party's own items use, and what a chest's contents mean is read from its identifier. A negative
/// identifier is the donor's random-item reference — a treasure level the item generator is asked for
/// later — which is kept exactly as stored rather than resolved here, because the map data records the
/// request and not its answer (OpenEnroth <c>src/Engine/Objects/ItemEnums.h:956-963</c>,
/// <c>ITEM_RANDOM_LEVEL_1..7</c>).
/// </remarks>
/// <param name="Slot">The item slot's index in the chest's inventory, which is what the record's grid refers to.</param>
/// <param name="ItemId">
/// The item's identifier, exactly as stored: a positive one names an item the item table carries, and a
/// negative one asks for a random item of that treasure level.
/// </param>
public readonly record struct MapChestItem(int Slot, int ItemId)
{
    /// <summary>Whether this reference asks for a random item rather than naming one.</summary>
    /// <remarks>
    /// The donor's random references are the negatives: <c>-1</c> is treasure level 1 through <c>-7</c>
    /// for level 7 (<c>src/Engine/Objects/ItemEnums.h:956-963</c>). The importer states that the
    /// reference is random and leaves which item it becomes to whoever owns item generation.
    /// </remarks>
    public bool IsRandom => ItemId < 0;

    /// <summary>The treasure level a random reference asks for, or 0 when the reference names an item.</summary>
    public int TreasureLevel => IsRandom ? -ItemId : 0;
}

/// <summary>One chest a delta carries, with what it holds.</summary>
/// <remarks>
/// <para>
/// A chest stands nowhere in a delta: the record is the container's contents and state, and where it
/// stands is the map face whose event opens it, so a chest is decoded here and placed from the map's
/// events rather than from a position of its own (OpenEnroth <c>src/Engine/Objects/Chest.cpp:370-407</c>
/// resolves a chest's position from the <c>EVENT_OpenChest</c> instructions of the faces that raise them).
/// </para>
/// <para>
/// Every shipped delta stores the same number of records — the runtime's own chest array, whose size the
/// donor caps at twenty (<c>src/Engine/Objects/Chest.cpp:52</c>, <c>assert(uChestID &lt; 20)</c>) —
/// whether or not a map places that many. A record no face opens is therefore a slot, not a container,
/// and the emitter counts those separately rather than inventing a position for them.
/// </para>
/// </remarks>
/// <param name="Index">The record's index in the delta's chest array, which is the identity an event names.</param>
/// <param name="TypeId">
/// The chest table row the record refers to, exactly as stored. The shipped data writes 0 everywhere;
/// the donor's table gives every row the same 9×9 grid and takes the texture from the row
/// (<c>src/Engine/Tables/ChestTable.cpp:5-19</c>), which is why the value is kept raw.
/// </param>
/// <param name="Flags">
/// The record's flag word, kept raw. The donor reads it as <c>CHEST_TRAPPED = 0x1</c>,
/// <c>CHEST_ITEMS_PLACED = 0x2</c>, and <c>CHEST_OPENED = 0x4</c>
/// (<c>src/Engine/Objects/ChestEnums.h:5-10</c>), and the reading belongs where the flag is answered.
/// </param>
/// <param name="Items">The items the record holds, in slot order, with the empty slots left out.</param>
public sealed record MapChest(int Index, int TypeId, int Flags, IReadOnlyList<MapChestItem> Items)
{
    /// <summary>The donor's flag for a chest that is trapped (<c>src/Engine/Objects/ChestEnums.h:7</c>).</summary>
    public const int TrappedFlag = 0x1;

    /// <summary>The donor's flag for a chest whose items have been placed (<c>src/Engine/Objects/ChestEnums.h:8</c>).</summary>
    public const int ItemsPlacedFlag = 0x2;

    /// <summary>The donor's flag for a chest that has been opened (<c>src/Engine/Objects/ChestEnums.h:9</c>).</summary>
    public const int OpenedFlag = 0x4;

    /// <summary>Whether the record says the chest is trapped.</summary>
    public bool IsTrapped => (Flags & TrappedFlag) != 0;

    /// <summary>Whether the record says the chest's items have been placed.</summary>
    public bool IsItemsPlaced => (Flags & ItemsPlacedFlag) != 0;

    /// <summary>Whether the record says the chest has been opened.</summary>
    public bool IsOpened => (Flags & OpenedFlag) != 0;
}

/// <summary>One sprite object a delta carries: something standing in the map that is not a decoration.</summary>
/// <remarks>
/// <para>
/// The donor reconstructs these as map objects, and a shipped delta's initial ones are the items lying on
/// the floor: each carries the item it holds in its own containing-item field, with no velocity, no spell,
/// and no attributes set. The same record is also what the donor uses for a projectile in flight, which is
/// why the containing item is decoded beside the sprite identity instead of being assumed: an object with
/// nothing in it is an object the map shows and nobody can pick up.
/// </para>
/// <para>
/// The record's spell, caster, target, and lifetime fields are consumed and not surfaced: they describe a
/// projectile already in flight, which a level's initial state does not hold, and nothing about a
/// stationary item needs them.
/// </para>
/// </remarks>
/// <param name="Index">The object's index in the delta's sprite-object array.</param>
/// <param name="SpriteId">The sprite the object is drawn with.</param>
/// <param name="ObjectDescId">The object description the sprite resolves through.</param>
/// <param name="Position">Where the object stands.</param>
/// <param name="YawAngle">The object's facing, in the game's own angle units.</param>
/// <param name="Attributes">
/// The object's attribute word, kept raw. The donor reads it as <c>SPRITE_*</c> bits
/// (<c>src/Engine/Objects/SpriteObject.h</c>); every shipped initial object has it zero.
/// </param>
/// <param name="SectorId">The sector the object stands in, or -1 when it belongs to none.</param>
/// <param name="ContainingItemId">
/// The item identifier the object holds, or 0 when it holds nothing. A positive one names an item the item
/// table carries; a negative one is a random-item reference like a chest's, and the donor resolves both the
/// same way when it reconstructs an object whose item is not a missile
/// (<c>src/Engine/Snapshots/CompositeSnapshots.cpp:368-375</c>).
/// </param>
public sealed record MapSpriteObject(
    int Index,
    int SpriteId,
    int ObjectDescId,
    MapPoint Position,
    int YawAngle,
    int Attributes,
    int SectorId,
    int ContainingItemId);
