namespace MightAndMagic7.Import.Lod;

/// <summary>What kind of defect made the operator's data unreadable.</summary>
/// <remarks>
/// The message names the file and the defect for a person; the fault is what a test and a caller compare,
/// so no reader of a failure has to match the sentence to know which kind it was.
/// </remarks>
public enum LodFault
{
    /// <summary>Something named is not there: an entry, a container, an installation, or a row it was looked up by.</summary>
    Missing,

    /// <summary>The bytes end before what they declare: a file too small, a record or a field running past the end.</summary>
    Truncated,

    /// <summary>The file does not carry the signature its format opens with.</summary>
    Signature,

    /// <summary>A compressed entry does not inflate to what it declares.</summary>
    Compression,

    /// <summary>A table or a pool holds a different number of rows, records, or values than the reader expects.</summary>
    Count,

    /// <summary>A cell, a field, or a byte holds something that is not a value of the kind its column states.</summary>
    Value,

    /// <summary>One record names another that does not exist, or states a number another source contradicts.</summary>
    Reference,

    /// <summary>Two records claim one identity, so neither can be told apart by it.</summary>
    Ambiguous,

    /// <summary>The operator's command line did not supply what the command needs.</summary>
    Usage,
}

/// <summary>Raised when a container file is not a readable archive or an entry cannot be resolved.</summary>
public sealed class LodFormatException : Exception
{
    /// <summary>Creates the exception with the kind of defect and a message that names the file and the defect.</summary>
    /// <param name="fault">The kind of defect, which is what a caller compares.</param>
    /// <param name="message">The file and the defect, in the words a person reads.</param>
    public LodFormatException(LodFault fault, string message)
        : base(message)
    {
        Fault = fault;
    }

    /// <summary>Creates the exception with the kind of defect, a message, and an inner cause.</summary>
    /// <param name="fault">The kind of defect, which is what a caller compares.</param>
    /// <param name="message">The file and the defect, in the words a person reads.</param>
    /// <param name="innerException">What the reader underneath failed with.</param>
    public LodFormatException(LodFault fault, string message, Exception innerException)
        : base(message, innerException)
    {
        Fault = fault;
    }

    /// <summary>The kind of defect.</summary>
    public LodFault Fault { get; }
}
