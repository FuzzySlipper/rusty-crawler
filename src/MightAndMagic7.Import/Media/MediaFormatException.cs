namespace MightAndMagic7.Import.Media;

/// <summary>Raised when an entry's bytes are not a media wrapper this decoder understands.</summary>
public sealed class MediaFormatException : Exception
{
    /// <summary>Creates the exception with a message that names the defect.</summary>
    public MediaFormatException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and an inner cause.</summary>
    public MediaFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
