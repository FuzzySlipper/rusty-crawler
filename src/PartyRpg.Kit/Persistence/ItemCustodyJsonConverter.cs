using System.Text.Json;
using System.Text.Json.Serialization;
using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Persistence;

/// <summary>
/// Writes where an item instance is held as data, rather than as the questions a live custody answers.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ItemCustody"/> is three places and nothing else, and the members it does not describe throw
/// when they are asked: asking a packed item which member wears it is a defect in live code. A save cannot
/// ask those questions — it has to write which of the three places an instance was in and read one back —
/// so the document states a kind, plus the member and slot when there is one, and this converter is what
/// turns that into the kit's value and back.
/// </para>
/// <para>
/// A record held by nobody is written and read like any other; what refuses it is the party factory, which
/// names it when a save is restored, so a loose world item can never arrive in the party's pack through a
/// load.
/// </para>
/// </remarks>
public sealed class ItemCustodyJsonConverter : JsonConverter<ItemCustody>
{
    private const string KindField = "kind";
    private const string MemberField = "member";
    private const string SlotField = "slot";
    private const string DetachedKind = "detached";
    private const string PackKind = "pack";
    private const string EquippedKind = "equipped";

    /// <inheritdoc />
    /// <exception cref="JsonException">The record does not name one of the three places an item can be held.</exception>
    public override ItemCustody Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("An item's custody is an object naming one of the three places an item can be held.");
        }

        string kind = string.Empty;
        ulong member = 0;
        string slot = string.Empty;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("An item's custody is an object of named fields.");
            }

            string name = reader.GetString() ?? string.Empty;
            reader.Read();
            switch (name)
            {
                case KindField:
                    kind = reader.GetString() ?? string.Empty;
                    break;
                case MemberField:
                    member = reader.GetUInt64();
                    break;
                case SlotField:
                    slot = reader.GetString() ?? string.Empty;
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        return kind switch
        {
            DetachedKind => ItemCustody.Detached,
            PackKind => ItemCustody.InSharedInventory,
            EquippedKind when member > 0 && slot.Length > 0 =>
                ItemCustody.EquippedBy(new PartyMemberId(member), new EquipmentSlot(slot)),
            EquippedKind => throw new JsonException(
                $"An equipped item names the member and the slot holding it, and this record names member {member} and slot '{slot}'."),
            _ => throw new JsonException(
                $"'{kind}' is not a place an item can be held; a custody is '{DetachedKind}', '{PackKind}', or '{EquippedKind}'."),
        };
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, ItemCustody value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStartObject();
        if (value.IsDetached)
        {
            writer.WriteString(KindField, DetachedKind);
        }
        else if (value.IsInSharedInventory)
        {
            writer.WriteString(KindField, PackKind);
        }
        else
        {
            // The remaining place is a member's slot, and only an equipped custody can answer with one.
            writer.WriteString(KindField, EquippedKind);
            writer.WriteNumber(MemberField, value.Member.Value);
            writer.WriteString(SlotField, value.Slot.Value);
        }

        writer.WriteEndObject();
    }
}
