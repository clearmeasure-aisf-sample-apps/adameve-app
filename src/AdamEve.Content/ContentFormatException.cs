namespace AdamEve.Content;

/// <summary>
/// The content of the game is not what the build shipped: the canonical text or a content file is damaged, missing
/// or changed. The game shows no Scripture from content that raised it.
/// </summary>
public sealed class ContentFormatException : FormatException
{
    /// <summary>Creates the exception without a message.</summary>
    public ContentFormatException()
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What is wrong with the content.</param>
    public ContentFormatException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What is wrong with the content.</param>
    /// <param name="innerException">The error that showed it.</param>
    public ContentFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
