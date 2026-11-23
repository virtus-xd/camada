using CamAda.Core.Safety;
using ManagedShell.AppBar;

namespace CamAda.Safety;

/// <summary>
/// Windows görev çubuğunu gizler ve geri getirir. Gizlemeden önce durumu diske yazar ki
/// çökme ya da süreç öldürülmesi durumunda bekçi süreç görev çubuğunu geri getirebilsin.
/// </summary>
public sealed class TaskbarGuard
{
    private readonly ExplorerHelper _explorer;
    private readonly TaskbarSafetyStore _store;
    private int _originalState = TaskbarSafetyStore.DefaultTaskbarState;

    public TaskbarGuard(ExplorerHelper explorer, TaskbarSafetyStore store)
    {
        _explorer = explorer;
        _store = store;
    }

    public bool IsHidden => _explorer.HideExplorerTaskbar;

    public void Hide()
    {
        if (_explorer.HideExplorerTaskbar)
        {
            return;
        }

        _originalState = (int)_explorer.GetTaskbarState();
        _store.Save(new TaskbarSafetyState(HiddenByUs: true, CleanShutdown: false, _originalState));
        _explorer.HideExplorerTaskbar = true;
    }

    public void ShowAndMarkClean()
    {
        if (_explorer.HideExplorerTaskbar)
        {
            _explorer.HideExplorerTaskbar = false;
        }

        _store.Save(new TaskbarSafetyState(HiddenByUs: false, CleanShutdown: true, _originalState));
    }
}
