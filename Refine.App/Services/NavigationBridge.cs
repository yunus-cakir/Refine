using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Refine.App.Services;

public class NavigationBridge
{
    // Blazor tarafının abone olacağı olay (Event)
    public event Action<string>? OnNavigate;
    public event Func<Task<bool>>? OnHardwareBackRequested;

    // XAML tarafının çağıracağı metot
    public void NavigateTo(string uri)
    {
        OnNavigate?.Invoke(uri);
    }

    public event Action<string>? OnLocationChanged;

    public void LocationChanged(string activeUrl)
    {
        OnLocationChanged?.Invoke(activeUrl);
    }

    public async Task<bool> RequestHardwareBackAsync()
    {
        if (OnHardwareBackRequested != null)
        {
            var handlers = OnHardwareBackRequested.GetInvocationList();
            foreach (Func<Task<bool>> handler in handlers)
            {
                if (await handler()) return true;
            }
        }
        return false;
    }
}