namespace MightAndMagic7.Import.Events;

/// <summary>
/// The event language's opcodes, and the word a pack names each of them by.
/// </summary>
/// <remarks>
/// <para>
/// The numbers and their meanings are the donor's own enumeration (OpenEnroth
/// <c>src/Engine/Evt/EvtEnums.h:9-78</c>, <c>EvtOpcode</c>); the words are this importer's spelling of those
/// names, so a pack states which instruction a step is without carrying the byte a reader would have to look
/// up. Every opcode the language has is named, including the ones no shipped program of this game uses, so a
/// step is never written as an unnamed number: a byte the table does not name is written as
/// <see cref="UnknownWord"/> with the byte beside it.
/// </para>
/// <para>
/// Naming an opcode is not reading it. Which operands are decoded is <see cref="EvtInstruction"/>'s, and what a
/// step does in play is the ruleset's; this table is only what the step is called.
/// </para>
/// </remarks>
public static class EvtOpcodes
{
    /// <summary>The word a step whose opcode this table does not name is written with.</summary>
    public const string UnknownWord = "unknown";

    /// <summary>Ends the event (and, as a face's own event, leaves the current map through an entrance).</summary>
    public const byte Exit = 1;

    /// <summary>Opens a building's own screen.</summary>
    public const byte SpeakInHouse = 2;

    /// <summary>Plays a sound.</summary>
    public const byte PlaySound = 3;

    /// <summary>The text shown while the party's aim rests on what raises the event; never executed.</summary>
    public const byte MouseOver = 4;

    /// <summary>Moves the party to a position, optionally on another map.</summary>
    public const byte MoveToMap = 6;

    /// <summary>
    /// Opens one of the map's containers, by the index the map's own container array gives it
    /// (OpenEnroth <c>src/Engine/Evt/EvtEnums.h:17</c>, <c>EVENT_OpenChest = 7</c>).
    /// </summary>
    public const byte OpenChest = 7;

    /// <summary>Harms characters.</summary>
    public const byte ReceiveDamage = 9;

    /// <summary>Moves one of the map's doors.</summary>
    public const byte ChangeDoorState = 15;

    /// <summary>Jumps when a variable compares.</summary>
    public const byte Compare = 14;

    /// <summary>Adds to a variable.</summary>
    public const byte Add = 16;

    /// <summary>Subtracts from a variable.</summary>
    public const byte Subtract = 17;

    /// <summary>Sets a variable.</summary>
    public const byte Set = 18;

    /// <summary>Jumps to one of several steps at random.</summary>
    public const byte RandomGoTo = 25;

    /// <summary>Shows a line of the map's own text in the status bar.</summary>
    public const byte StatusText = 29;

    /// <summary>Shows a line of text in a dialogue.</summary>
    public const byte ShowMessage = 30;

    /// <summary>A trigger: the steps after it run on a short timer, not when the event is raised.</summary>
    public const byte OnTimer = 31;

    /// <summary>Chooses which characters the following steps act on.</summary>
    public const byte ForPartyMember = 35;

    /// <summary>Jumps to a step.</summary>
    public const byte Jmp = 36;

    /// <summary>A trigger: the steps after it run when the map is loaded.</summary>
    public const byte OnMapReload = 37;

    /// <summary>A trigger: the steps after it run on a long timer.</summary>
    public const byte OnLongTimer = 38;

    /// <summary>Jumps when the calendar stands in a season.</summary>
    public const byte CheckSeason = 56;

    /// <summary>Puts creatures of one of the map's encounter slots on the field at a point of the map.</summary>
    public const byte SummonMonsters = 19;

    /// <summary>Casts a spell from one point of the map at another.</summary>
    public const byte CastSpell = 21;

    /// <summary>Opens a conversation with one of the game's people.</summary>
    public const byte SpeakNpc = 22;

    /// <summary>Sets or clears an attribute bit on every face of one face group.</summary>
    public const byte SetFacesBit = 23;

    /// <summary>Gives every face of a cog another bitmap (OpenEnroth <c>src/Engine/Evt/EvtEnums.h:21</c>).</summary>
    public const byte SetTexture = 11;

    /// <summary>Shows or hides every decoration of a cog and may give it another look (<c>EvtEnums.h:23</c>).</summary>
    public const byte SetSprite = 13;

    /// <summary>Turns one of an interior's lights on or off (<c>EvtEnums.h:41</c>).</summary>
    public const byte ToggleIndoorLight = 32;

    /// <summary>Changes which event one of a person's topics raises.</summary>
    public const byte SetNpcTopic = 39;

    /// <summary>Moves a person to another house (OpenEnroth <c>src/Engine/Evt/EvtInterpreter.cpp:470-471</c>).</summary>
    public const byte MoveNpc = 40;

    /// <summary>Puts an item, named or drawn from a treasure level, in the party's hands.</summary>
    public const byte GiveItem = 41;

    /// <summary>Jumps when a character holds a skill at a rank and mastery.</summary>
    public const byte CheckSkill = 43;

    /// <summary>
    /// Jumps, in a person's topic's offer check, when a character's variable holds (OpenEnroth
    /// <c>src/Engine/Evt/EvtInterpreter.cpp:156-163</c>); a regular run passes over it.
    /// </summary>
    public const byte CanShowDialogItemCompare = 44;

    /// <summary>Ends a person's topic's offer check (OpenEnroth <c>src/Engine/Evt/EvtInterpreter.cpp:164-165</c>).</summary>
    public const byte EndCanShowDialogItem = 45;

    /// <summary>States, in a person's topic's offer check, whether the topic is offered (OpenEnroth <c>src/Engine/Evt/EvtInterpreter.cpp:166-169</c>).</summary>
    public const byte SetCanShowDialogItem = 46;

    /// <summary>
    /// Gives an item to, or takes one from, every creature standing for a person (OpenEnroth
    /// <c>src/Engine/Evt/EvtInterpreter.cpp:538-539</c>, <c>src/Engine/Objects/Actor.cpp:139-165</c>).
    /// </summary>
    public const byte NpcSetItem = 49;

    /// <summary>Changes the greeting row a person greets the party with (OpenEnroth <c>src/Engine/Evt/EvtInterpreter.cpp:541-545</c>).</summary>
    public const byte SetNpcGreeting = 50;

    /// <summary>Jumps when enough of a set of creatures are dead.</summary>
    public const byte IsActorKilled = 51;

    /// <summary>Sets or clears an attribute bit on every creature of one group.</summary>
    public const byte ToggleActorGroupFlag = 57;

    /// <summary>The word each opcode is written with, from the donor's enumeration.</summary>
    private static readonly Dictionary<byte, string> Words = new()
    {
        [Exit] = "exit",
        [SpeakInHouse] = "speak-in-house",
        [PlaySound] = "play-sound",
        [MouseOver] = "mouse-over",
        [5] = "location-name",
        [MoveToMap] = "move-to-map",
        [OpenChest] = "open-chest",
        [8] = "show-face",
        [ReceiveDamage] = "receive-damage",
        [10] = "set-snow",
        [11] = "set-texture",
        [12] = "show-movie",
        [13] = "set-sprite",
        [Compare] = "compare",
        [ChangeDoorState] = "change-door-state",
        [Add] = "add",
        [Subtract] = "subtract",
        [Set] = "set",
        [SummonMonsters] = "summon-monsters",
        [21] = "cast-spell",
        [22] = "speak-npc",
        [23] = "set-faces-bit",
        [24] = "toggle-actor-flag",
        [RandomGoTo] = "random-go-to",
        [26] = "input-string",
        [StatusText] = "status-text",
        [ShowMessage] = "show-message",
        [OnTimer] = "on-timer",
        [32] = "toggle-indoor-light",
        [33] = "press-any-key",
        [34] = "summon-item",
        [ForPartyMember] = "for-party-member",
        [Jmp] = "jump",
        [OnMapReload] = "on-map-reload",
        [OnLongTimer] = "on-long-timer",
        [39] = "set-npc-topic",
        [MoveNpc] = "move-npc",
        [41] = "give-item",
        [42] = "change-event",
        [43] = "check-skill",
        [CanShowDialogItemCompare] = "can-show-dialog-item-compare",
        [EndCanShowDialogItem] = "end-can-show-dialog-item",
        [SetCanShowDialogItem] = "set-can-show-dialog-item",
        [47] = "set-npc-group-news",
        [48] = "set-actor-group",
        [49] = "npc-set-item",
        [SetNpcGreeting] = "set-npc-greeting",
        [51] = "is-actor-killed",
        [52] = "can-show-topic-is-actor-killed",
        [53] = "on-map-leave",
        [54] = "change-group",
        [55] = "change-group-ally",
        [CheckSeason] = "check-season",
        [57] = "toggle-actor-group-flag",
        [58] = "toggle-chest-flag",
        [59] = "character-animation",
        [60] = "set-actor-item",
        [61] = "on-date-timer",
        [62] = "enable-date-timer",
        [63] = "stop-animation",
        [64] = "check-items-count",
        [65] = "remove-items",
        [66] = "special-jump",
        [67] = "is-total-bounty-hunting-award-in-range",
        [68] = "is-npc-in-party",
    };

    /// <summary>The word an opcode is written with, or <see cref="UnknownWord"/> when the language has none.</summary>
    /// <param name="opcode">The opcode.</param>
    /// <returns>The word.</returns>
    public static string Word(byte opcode) => Words.GetValueOrDefault(opcode, UnknownWord);

    /// <summary>Whether an opcode is a trigger: a step that starts steps run by the map rather than by a use.</summary>
    /// <remarks>
    /// The donor skips a trigger when it meets one while running an event, ending the run there
    /// (OpenEnroth <c>src/Engine/Evt/EvtInterpreter.cpp:425-448</c>), and registers the steps after it with the
    /// map's own timers or its load (<c>src/Engine/Evt/Processor.cpp:89-140</c>).
    /// </remarks>
    /// <param name="opcode">The opcode.</param>
    /// <returns>Whether it is a trigger.</returns>
    public static bool IsTrigger(byte opcode) => opcode is OnTimer or OnLongTimer or OnMapReload or 53 or 61;
}
