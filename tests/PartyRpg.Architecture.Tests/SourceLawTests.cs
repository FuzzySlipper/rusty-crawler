using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.World;
using Xunit;

namespace PartyRpg.Architecture.Tests;

/// <summary>
/// The laws AGENTS.md states for every runtime project — one clock, safe code, one larder — read from the kit, the
/// ruleset, and the host as syntax bound to symbols.
/// </summary>
/// <remarks>
/// <para>
/// Each law names what it forbids through the symbol it means, so a comment that mentions a timer is not a timer, a
/// target-typed <c>new()</c> of a stopwatch is still a stopwatch, and a renamed type is followed by the compiler
/// rather than escaped. The three projects are compiled together from their sources (<see cref="SourceCode"/>), so a
/// host line that reaches the kit through a ruleset type binds as surely as a kit line.
/// </para>
/// <para>
/// Every law also proves its own detector on code this suite writes, spelled the ways the law must see through, so
/// a detector that stopped finding anything could not pass as a clean product.
/// </para>
/// </remarks>
public sealed class SourceLawTests
{
    private static readonly string[] Runtime = ["PartyRpg.Kit", "PartyRpg.Rulesets.MightAndMagic7", "PartyRpg.Host"];

    /// <summary>Types whose every use is an ambient time source, a second loop, or a thread beside the admitted update.</summary>
    private static readonly string[] AmbientTypes =
    [
        "System.DateTime",
        "System.DateTimeOffset",
        "System.TimeProvider",
        "System.Diagnostics.Stopwatch",
        "System.Threading.Timer",
        "System.Timers.Timer",
        "System.Threading.PeriodicTimer",
        "System.Threading.Thread",
        "System.Threading.ThreadPool",
        "System.Threading.Tasks.Parallel",
        "System.Threading.Tasks.TaskFactory",
    ];

    /// <summary>Members of otherwise ordinary types that start work or read time outside the admitted update.</summary>
    private static readonly (string Type, string Member)[] AmbientMembers =
    [
        ("System.Threading.Tasks.Task", "Run"),
        ("System.Threading.Tasks.Task", "Delay"),
        ("System.Threading.Tasks.Task", "Factory"),
        ("System.Threading.Tasks.Task", "Yield"),
        ("System.Environment", "TickCount"),
        ("System.Environment", "TickCount64"),
        ("System.Threading.CancellationTokenSource", "CancelAfter"),
    ];

    /// <summary>Types a safe product never reaches: handles, marshalling, native memory, and runtime loading.</summary>
    private static readonly string[] UnsafeTypes =
    [
        "System.Runtime.InteropServices.GCHandle",
        "System.Runtime.InteropServices.Marshal",
        "System.Runtime.InteropServices.NativeMemory",
        "System.Runtime.InteropServices.NativeLibrary",
        "System.Activator",
    ];

    [Fact]
    public void No_runtime_project_keeps_a_second_clock_timer_thread_or_loop()
    {
        // The one clock is advanced by its callers inside the one admitted update. A wall clock, a timer, a thread,
        // or work started on the pool beside it would be a second clock, and AGENTS.md forbids one in the host and
        // the ruleset as much as in the kit.
        Assert.Empty(AmbientTimeSites(SourceCode.Of(Runtime)));
    }

    [Fact]
    public void The_ambient_time_detector_sees_every_spelling_and_no_comment()
    {
        SourceCode probe = SourceCode.FromText(("probe.cs", """
            using System.Diagnostics;
            using System.Threading;
            using System.Threading.Tasks;

            // A Timer, a Thread, and DateTime.Now in a comment are words, not a clock.
            /// <summary>Nor is a Stopwatch in documentation.</summary>
            internal sealed class Probe
            {
                private readonly Stopwatch _watch = new();
                private static long Now() => System.DateTime.UtcNow.Ticks;
                private static void Start() => Task.Run(() => { });
                private static void Spread(int[] items) => Parallel.ForEach(items, _ => { });
                private static void Later(CancellationTokenSource source) => source.CancelAfter(10);
                private static int Ticks() => System.Environment.TickCount;
                private static System.Threading.Timer Tick() => new(_ => { });
            }
            """));

        string[] found = [.. AmbientTimeSites(probe).Select(site => site.Text)];
        Assert.Contains(found, text => text.StartsWith("Stopwatch", StringComparison.Ordinal));
        Assert.Contains("new()", found.Where(text => text.StartsWith("new", StringComparison.Ordinal)));
        Assert.Contains("DateTime", found);
        Assert.Contains("Run", found);
        Assert.Contains("Parallel", found);
        Assert.Contains("CancelAfter", found);
        Assert.Contains("TickCount", found);
        Assert.Contains(found, text => text.StartsWith("new(", StringComparison.Ordinal));
        Assert.DoesNotContain(AmbientTimeSites(probe), site => site.Line <= 6);
    }

    [Fact]
    public void No_runtime_project_steps_outside_safe_managed_code()
    {
        // The engine is reached through its safe, named services. A pointer, an unsafe block, a handle, marshalling,
        // a native declaration, or loading code at runtime would put ABI and lifetime concerns inside gameplay code,
        // where the next reader cannot see them, and reflection discovery is how a second composition grows.
        SourceCode code = SourceCode.Of(Runtime);
        List<SourceSite> sites = [.. code.UnsafeConstructs()];
        foreach (string type in UnsafeTypes) sites.AddRange(code.UsesOfType(code.Type(type)));
        sites.AddRange(code.UsesOfNamespace("System.Reflection"));
        Assert.Empty(sites);
    }

    [Fact]
    public void The_safe_code_detector_sees_every_spelling()
    {
        SourceCode probe = SourceCode.FromText(("probe.cs", """
            using System.Runtime.InteropServices;
            internal static unsafe class Probe
            {
                [DllImport("native")] private static extern int Native();
                private static void Pin(object value) { GCHandle handle = GCHandle.Alloc(value); handle.Free(); }
                private static void Raw() { int x = 1; int* p = &x; fixed (byte* b = new byte[1]) { } }
                private static object Load() => System.Reflection.Assembly.Load("x");
            }
            """));

        Assert.True(probe.UnsafeConstructs().Count >= 4);
        Assert.NotEmpty(probe.UsesOfType(probe.Type("System.Runtime.InteropServices.GCHandle")));
        Assert.NotEmpty(probe.UsesOfNamespace("System.Reflection"));
    }

    [Fact]
    public void The_partys_larder_is_the_one_place_food_is_counted()
    {
        // A second counter beside the party's would be a number that can disagree with the one the larder reports,
        // which is exactly what a party-scoped resource exists to prevent. A policy may state a rate as a constant,
        // and a read-only property may hand a value on; what it may not do is keep one.
        SourceCode code = SourceCode.Of(Runtime);
        (SourceSite Site, string Name, ITypeSymbol Type)[] stores = [.. FoodStores(code)];
        Assert.Contains(stores, store => store.Site.File == "src/PartyRpg.Kit/Party/PartyFood.cs");
        Assert.Empty(stores.Where(store => store.Site.File != "src/PartyRpg.Kit/Party/PartyFood.cs").Select(store => store.Site));
    }

    [Fact]
    public void The_food_store_detector_sees_every_modifier_and_numeric_type()
    {
        SourceCode probe = SourceCode.FromText(("probe.cs", """
            internal sealed class Probe
            {
                private readonly int _foodLeft = 3;
                private static long s_rations;
                short portionsKept;
                public decimal ProvisionsHeld { get; set; }
                public double FoodRate { get; }
                private const int DailyPortions = 1;
                public int PortionsOffered { get; init; }
            }

            internal sealed record Report(int Coins)
            {
                private readonly int _provisionsAfter = Coins;
            }
            """));

        string[] found = [.. FoodStores(probe).Select(store => store.Name).Order(StringComparer.Ordinal)];
        Assert.Equal(["ProvisionsHeld", "_foodLeft", "portionsKept", "s_rations"], found);
    }

    private static IEnumerable<(SourceSite Site, string Name, ITypeSymbol Type)> FoodStores(SourceCode code)
    {
        Regex food = new("(Food|Portions|Provisions|Rations)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return code.Stores().Where(store =>
            food.IsMatch(store.Name) &&
            (store.Type.SpecialType is >= SpecialType.System_SByte and <= SpecialType.System_Double || store.Type.Name == nameof(Provisions)));
    }

    private static List<SourceSite> AmbientTimeSites(SourceCode code)
    {
        List<SourceSite> sites = [];
        foreach (string type in AmbientTypes) sites.AddRange(code.UsesOfType(code.Type(type)));
        foreach ((string type, string member) in AmbientMembers)
        {
            sites.AddRange(code.Uses([.. code.Type(type).GetMembers(member)]));
        }

        return sites;
    }
}
