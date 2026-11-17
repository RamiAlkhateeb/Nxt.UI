namespace Nxt.UI;

public sealed record NxtToastMessage(string Text, bool IsError, int Id, string? ActionLabel = null, Func<Task>? Action = null);

/// <summary>Queue-free toast: a newer message replaces the visible one. Rendered by &lt;NxtToastHost/&gt;.</summary>
public sealed class ToastService
{
    private int _nextId;

    public NxtToastMessage? Current { get; private set; }
    public event Action? Changed;

    public void Show(string text, bool isError = false, int durationMs = 3500) =>
        Present(new NxtToastMessage(text, isError, ++_nextId), durationMs);

    /// <summary>Shows a message with a button, e.g. "Task deleted · Undo". The action runs at most once.</summary>
    public void ShowWithAction(string text, string actionLabel, Func<Task> action, int durationMs = 5000) =>
        Present(new NxtToastMessage(text, false, ++_nextId, actionLabel, action), durationMs);

    /// <summary>Runs the visible toast's action (if any) and hides the toast.</summary>
    public async Task RunActionAsync()
    {
        var action = Current?.Action;
        Hide();
        if (action is not null) await action();
    }

    public void Hide()
    {
        Current = null;
        Changed?.Invoke();
    }

    private void Present(NxtToastMessage message, int durationMs)
    {
        Current = message;
        Changed?.Invoke();
        _ = HideLaterAsync(message.Id, durationMs);
    }

    private async Task HideLaterAsync(int id, int durationMs)
    {
        await Task.Delay(durationMs);
        if (Current?.Id == id) Hide();
    }
}
