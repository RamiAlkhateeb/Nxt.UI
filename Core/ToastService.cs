namespace Nxt.UI;

public sealed record NxtToastMessage(string Text, bool IsError, int Id);

/// <summary>Queue-free toast: a newer message replaces the visible one. Rendered by &lt;NxtToastHost/&gt;.</summary>
public sealed class ToastService
{
    private int _nextId;

    public NxtToastMessage? Current { get; private set; }
    public event Action? Changed;

    public void Show(string text, bool isError = false, int durationMs = 3500)
    {
        var message = new NxtToastMessage(text, isError, ++_nextId);
        Current = message;
        Changed?.Invoke();
        _ = HideLaterAsync(message.Id, durationMs);
    }

    public void Hide()
    {
        Current = null;
        Changed?.Invoke();
    }

    private async Task HideLaterAsync(int id, int durationMs)
    {
        await Task.Delay(durationMs);
        if (Current?.Id == id) Hide();
    }
}
