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

    // XAML tarafının çağıracağı metot
    public void NavigateTo(string uri)
    {
        OnNavigate?.Invoke(uri);
    }
}