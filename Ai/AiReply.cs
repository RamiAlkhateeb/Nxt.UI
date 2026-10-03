namespace Nxt.UI.Ai;

/// <summary>A labelled row in a reply list, e.g. ("Tue 7", "Pizza").</summary>
public sealed record AiListItem(string Value, string? Label = null);

/// <summary>A titled list inside a reply bubble ("Meals added", "Tasks").</summary>
public sealed record AiListSection(string Heading, IReadOnlyList<AiListItem> Items);

/// <summary>What an app's <c>OnSend</c> handler returns for &lt;AiChat&gt; to render.</summary>
public sealed class AiReply
{
    public string Text { get; init; } = "";
    public bool IsError { get; init; }
    public bool ShowSettingsLink { get; init; }
    public IReadOnlyList<AiListSection> Sections { get; init; } = [];

    /// <summary>Optional action button under the bubble (e.g. "Open project").</summary>
    public string? ActionText { get; init; }
    public string? ActionHref { get; init; }

    public static AiReply Error(string text) => new() { Text = text, IsError = true };
}

/// <summary>One bubble in the thread.</summary>
public sealed class AiMessage
{
    public bool FromUser { get; init; }
    public required AiReply Reply { get; init; }
}
