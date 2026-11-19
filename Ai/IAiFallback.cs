namespace Nxt.UI.Ai;

/// <summary>
/// An app-provided AI that <see cref="GeminiClient"/> uses when no Gemini key is set, for example a server
/// endpoint that signed-in users may call a few times a day. Register it in DI as <see cref="IAiFallback"/>.
/// </summary>
public interface IAiFallback
{
    /// <summary>Shown where a model name would be (e.g. "Workers AI").</summary>
    string Name { get; }

    /// <summary>Whether it can be used right now (e.g. the user is signed in).</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Answers <paramref name="prompt"/> with JSON text matching <paramref name="schema"/> (a Gemini
    /// <c>response_schema</c>). Failures are <see cref="AiFallbackException"/>.
    /// </summary>
    Task<string> GenerateJsonAsync(string prompt, object schema);
}

public enum AiFallbackErrorKind
{
    /// <summary>The user has to sign in (or add a Gemini key) first.</summary>
    SignInRequired,
    /// <summary>The daily allowance is used up.</summary>
    LimitReached,
    Other
}

/// <summary>An <see cref="IAiFallback"/> failure. Show it with <c>NxtText.AiError</c>.</summary>
public sealed class AiFallbackException : InvalidOperationException
{
    public AiFallbackException(AiFallbackErrorKind kind, string detail, Exception? inner = null) : base(detail, inner)
    {
        Kind = kind;
        Detail = detail;
    }

    public AiFallbackErrorKind Kind { get; }
    public string Detail { get; }
}
