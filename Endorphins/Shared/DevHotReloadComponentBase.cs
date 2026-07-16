using Microsoft.AspNetCore.Components;

public abstract class DevHotReloadComponentBase : ComponentBase, IDisposable
{
#if DEBUG
    private Timer? _devTimer;
#endif

    protected override void OnAfterRender(bool firstRender)
    {
#if DEBUG
        if (firstRender)
        {
            _devTimer = new Timer(_ => InvokeAsync(StateHasChanged), null, 1000, 1000);
        }
#endif
        base.OnAfterRender(firstRender);
    }

    public virtual void Dispose()
    {
#if DEBUG
        _devTimer?.Dispose();
#endif
    }
}