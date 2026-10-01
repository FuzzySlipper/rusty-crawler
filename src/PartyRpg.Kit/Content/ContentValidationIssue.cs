namespace PartyRpg.Kit.Content;

/// <summary>One thing wrong with content, named so a report can point at the pack and document.</summary>
/// <param name="Code">A short stable code for the kind of problem.</param>
/// <param name="Message">What is wrong, in terms of the pack and document it is wrong in.</param>
/// <param name="PackId">The pack the problem is in.</param>
/// <param name="DocumentId">The document the problem is in, when it is document-specific.</param>
/// <param name="NotSelected">
/// When the problem is in a pack the product's selection does not load: why it was judged anyway and where it
/// was read from. It is null for a pack the selection loads and for a problem that is not one pack's. A start
/// refused by a pack nobody selected says so, because the operator's fix is then to remove or repair a pack
/// sitting on disk rather than to look for the defect in the game they chose.
/// </param>
public sealed record ContentValidationIssue(string Code, string Message, string PackId, string? DocumentId = null, string? NotSelected = null)
{
    /// <inheritdoc />
    public override string ToString()
    {
        string stated = DocumentId is null ? $"{PackId}: {Message}" : $"{PackId}/{DocumentId}: {Message}";
        return NotSelected is null ? stated : $"{stated} [{NotSelected}]";
    }
}

/// <summary>Raised when content that must be valid is not, carrying every issue found.</summary>
public sealed class ContentValidationException : Exception
{
    /// <summary>Creates the exception from the issues that made the content unusable.</summary>
    public ContentValidationException(string message, IReadOnlyList<ContentValidationIssue> issues)
        : base(message)
    {
        Issues = issues;
    }

    /// <summary>Every issue found, not just the first, so one run reports the whole picture.</summary>
    public IReadOnlyList<ContentValidationIssue> Issues { get; }
}
