namespace PartyRpg.Kit.Content;

/// <summary>One thing wrong with content, named so a report can point at the pack and document.</summary>
/// <param name="Code">A short stable code for the kind of problem.</param>
/// <param name="Message">What is wrong, in terms of the pack and document it is wrong in.</param>
/// <param name="PackId">The pack the problem is in.</param>
/// <param name="DocumentId">The document the problem is in, when it is document-specific.</param>
public sealed record ContentValidationIssue(string Code, string Message, string PackId, string? DocumentId = null)
{
    /// <inheritdoc />
    public override string ToString() => DocumentId is null ? $"{PackId}: {Message}" : $"{PackId}/{DocumentId}: {Message}";
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
