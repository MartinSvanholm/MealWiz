using MudBlazor;

namespace TheMealWorkshop.Shared.Features;

public abstract class StateContainerBase
{
    public event Action OnStateChanged;
    public void NotifyStateChanged() => OnStateChanged?.Invoke();
    public ISnackbar CurrentSnackbar { get; set; }
}
