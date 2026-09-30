using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// The kit's own sources, and the whole runtime's, as the source laws beside each owner read them.
/// </summary>
/// <remarks>
/// A law about who may write an owner's state lives beside that owner's tests, and reads the code as syntax bound to
/// symbols (<see cref="SourceCode"/>), so a writer is found through the member it calls — however the call is spelled
/// and whatever the member is renamed to — and a comment that names the member is not a call.
/// </remarks>
internal static class ProductSource
{
    /// <summary>The kit's sources.</summary>
    internal static SourceCode Kit => SourceCode.Of("PartyRpg.Kit");

    /// <summary>The kit, the ruleset, and the host, compiled together: a second writer in any of them is the same defect.</summary>
    internal static SourceCode Runtime => SourceCode.Of("PartyRpg.Kit", "PartyRpg.Rulesets.MightAndMagic7", "PartyRpg.Host");

    /// <summary>Asserts that no site falls outside the files a law allows, and that the law found something to allow.</summary>
    /// <param name="sites">Every site the law's query found.</param>
    /// <param name="allowed">Whether a repository-relative file may hold such a site.</param>
    /// <param name="because">What the law protects, which a failure states.</param>
    internal static void OnlyIn(IReadOnlyList<SourceSite> sites, Func<string, bool> allowed, string because)
    {
        Assert.True(sites.Any(site => allowed(site.File)), $"The law found no allowed site, so it would pass however the code changed: {because}");
        SourceSite[] offenders = [.. sites.Where(site => !allowed(site.File))];
        Assert.True(offenders.Length == 0, because + "\n" + string.Join('\n', offenders.Select(site => site.ToString())));
    }
}
