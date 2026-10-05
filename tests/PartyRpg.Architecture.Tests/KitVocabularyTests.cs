using System.Text.RegularExpressions;
using Xunit;

namespace PartyRpg.Architecture.Tests;

/// <summary>
/// The kit names no game: not the ruleset, not its donors or source files, and not the game's own classes, skills,
/// spells, items, monsters, places, or people.
/// </summary>
/// <remarks>
/// <para>
/// This is a law about words, so it reads words: every C# file and project file of the kit, comments included,
/// because a comment that explains the kit by this game's example is the first step toward code that assumes it.
/// The kit's README explains the boundary by name and is not read — a rule that forbade explaining itself would be
/// a trap rather than a boundary.
/// </para>
/// <para>
/// A name matches as a word or as the leading word of an identifier (<c>Knight</c>, <c>knight</c>,
/// <c>KnightRank</c>), never inside another word, so a list of the game's names can grow without a common English
/// word tripping it. The game's names are chosen to be the game's own rather than ordinary English, and each one is
/// proved to be this game's word by appearing, spelled that way, in the ruleset, the importer, or the research
/// documents — a name nothing in the game's own half of the repository uses is not vocabulary this law protects.
/// </para>
/// </remarks>
public sealed class KitVocabularyTests
{
    /// <summary>The ruleset, the donors, and the original's source files, matched anywhere in the kit's text.</summary>
    private static readonly string[] Owners =
    [
        "MightAndMagic", "Might and Magic", "MM6", "MM7", "MM8",
        "OpenEnroth", "MMExtension", "OpenMM8",
        "PartyRpg.Host", "PartyRpg.Rulesets",
        ".lod", ".odm", ".ddm", ".blv", ".dlv", "events.lod", "games.lod",
    ];

    /// <summary>A representative sample of the game's own names, matched as words.</summary>
    private static readonly string[] GameNames =
    [
        // Classes and their promotions.
        "Knight", "Paladin", "Archer", "Druid", "Cleric", "Sorcerer", "Monk", "Thief", "Ranger", "Cavalier", "Crusader",
        "Warrior Mage", "Master Archer", "Great Druid", "Arch Druid", "Warlock", "Archmage", "Lich", "Ninja", "Assassin",
        "Ranger Lord", "Bounty Hunter", "Black Knight", "Priest of the Light", "Priest of the Dark", "Sniper",
        // Races.
        "Goblin", "Dwarf",
        // Skills.
        "Armsmaster", "Bodybuilding", "Disarm Trap", "Identify Monster", "Identify Item", "Repair Item", "Blaster",
        "Diplomacy", "Thievery",
        // Spells.
        "Fire Bolt", "Fireball", "Torch Light", "Wizard Eye", "Town Portal", "Lloyd's Beacon", "Meteor Shower",
        "Armageddon", "Souldrinker", "Day of the Gods", "Divine Intervention", "Dragon Breath", "Stone Skin",
        "Feather Fall", "Lightning Bolt", "Ice Bolt", "Hour of Power", "Water Walk", "Dispel Magic",
        // Potions and reagents.
        "Cure Wounds", "Magic Potion", "Widowsweep", "Rejuvenation",
        // Creatures.
        "Dragonfly",
        // The world and its people.
        "Erathia", "Enroth", "Harmondale", "Emerald Isle", "Emerald Island", "Tularean", "Deyja", "Bracada", "Avlee",
        "Nighon", "Evenmorn", "Tatalia", "Barrow Downs", "Celeste", "Stone City", "Markham", "Archibald", "Xenofex",
    ];

    [Fact]
    public void The_kit_names_no_ruleset_donor_or_source_file()
    {
        List<string> offenders = [];
        foreach ((string file, string text) in KitText())
        {
            foreach (string owner in Owners)
            {
                if (text.Contains(owner, StringComparison.OrdinalIgnoreCase)) offenders.Add($"{file}: '{owner}'");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "The kit must not name the ruleset, its donors, or the original's source files:\n" + string.Join('\n', offenders));
    }

    [Fact]
    public void The_kit_names_none_of_the_games_classes_skills_spells_items_creatures_places_or_people()
    {
        List<string> offenders = [];
        foreach ((string file, string text) in KitText())
        {
            foreach (string name in GameNames)
            {
                Match found = Word(name).Match(text);
                if (found.Success) offenders.Add($"{file}: '{name}' in \"...{Around(text, found.Index)}...\"");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "The kit must not name the game's own vocabulary; it belongs in the ruleset and its content:\n" + string.Join('\n', offenders));
    }

    [Fact]
    public void Every_forbidden_name_is_one_the_games_own_half_of_the_repository_uses()
    {
        string game = string.Join(
            '\n',
            new[] { "src/PartyRpg.Rulesets.MightAndMagic7", "src/MightAndMagic7.Import" }
                .SelectMany(directory => Repository.Files(directory, "*.cs"))
                .Concat(Repository.Files("docs", "*.md"))
                .Select(File.ReadAllText));

        Assert.All(GameNames, name => Assert.True(
            Word(name).IsMatch(game),
            $"'{name}' appears nowhere in the ruleset, the importer, or the research documents, so it is not this game's word as spelled."));

        // And the matcher is the one the law uses: a name as a word or an identifier's leading word, never inside
        // another word.
        Assert.Matches(Word("Knight"), "KnightRank");
        Assert.Matches(Word("Knight"), "a knight's due");
        Assert.DoesNotMatch(Word("Ranger"), "arranger");
        Assert.DoesNotMatch(Word("Monk"), "monkey");
    }

    /// <summary>The kit's C# and project files, each with its text.</summary>
    private static IEnumerable<(string File, string Text)> KitText()
    {
        string[] files =
        [
            .. Repository.Files("src/PartyRpg.Kit", "*.cs"),
            .. Repository.Files("src/PartyRpg.Kit", "*.csproj"),
        ];
        Assert.NotEmpty(files);
        return files.Select(file => (Repository.Relative(file), File.ReadAllText(file)));
    }

    /// <summary>A name as a word, or as the leading word of an identifier, whatever the case of its first letter.</summary>
    private static Regex Word(string name) =>
        new(
            $"(?<![A-Za-z])[{char.ToUpperInvariant(name[0])}{char.ToLowerInvariant(name[0])}]{Regex.Escape(name[1..])}(?![a-z])",
            RegexOptions.CultureInvariant);

    private static string Around(string text, int index) =>
        text.Substring(Math.Max(0, index - 20), Math.Min(60, text.Length - Math.Max(0, index - 20))).ReplaceLineEndings(" ");
}
