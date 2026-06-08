using System;
using System.Threading.Tasks;
using Refine.App.Models;

namespace Refine.App.Services;

public class PopupService : IPopupService
{
    public event Action<PopupOptions>? OnShow;
    public event Action? OnHide;

    private TaskCompletionSource<PopupResult>? _tcs;

    public Task<PopupResult> ShowAsync(PopupOptions options)
    {
        // Cancel the previous popup if one is already open, to avoid deadlocks
        _tcs?.TrySetResult(PopupResult.Dismissed());

        _tcs = new TaskCompletionSource<PopupResult>();
        OnShow?.Invoke(options);
        
        return _tcs.Task;
    }

    internal void Resolve(PopupResult result)
    {
        if (_tcs != null)
        {
            _tcs.TrySetResult(result);
            _tcs = null;
        }
        OnHide?.Invoke();
    }
}
