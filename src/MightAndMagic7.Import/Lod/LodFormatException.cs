namespace MightAndMagic7.Import.Lod;

/// <summary>Raised when a container file is not a readable archive or an entry cannot be resolved.</summary>
public sealed class LodFormatException : Exception
{
    /// <summary>Creates the exception with a message that names the file and the defect.</summary>
    public LodFormatException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and an inner cause.</summary>
    public LodFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
