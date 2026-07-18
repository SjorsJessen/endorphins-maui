using Microsoft.AspNetCore.Components;

#if DEBUG
[assembly: System.Reflection.Metadata.MetadataUpdateHandler(typeof(DevHotReload))]
#endif

/// <summary>
/// Hot-reload plumbing: the runtime calls <see cref="UpdateApplication"/> after an
/// edit is applied, and subscribed components re-render. Replaces the old 1-second
/// polling timer, which forced every component to re-render continuously and made
/// the whole app feel slow.
/// </summary>
public static class DevHotReload
{
    public static event Action? Applied;

    public static void UpdateApplication(Type[]? _) => Applied?.Invoke();
    public static void ClearCache(Type[]? _) { }
}

public abstract class DevHotReloadComponentBase : ComponentBase, IDisposable
{
    protected override void OnAfterRender(bool firstRender)
    {
#if DEBUG
        if (firstRender)
        {
            DevHotReload.Applied += OnHotReloadApplied;
        }
#endif
        base.OnAfterRender(firstRender);
    }

#if DEBUG
    private void OnHotReloadApplied() => _ = InvokeAsync(StateHasChanged);
#endif

    public virtual void Dispose()
    {
#if DEBUG
        DevHotReload.Applied -= OnHotReloadApplied;
#endif
    }
}
