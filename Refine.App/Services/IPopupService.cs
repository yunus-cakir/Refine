using System;
using System.Threading.Tasks;
using Refine.App.Models;

namespace Refine.App.Services;

public interface IPopupService
{
    event Action<PopupOptions>? OnShow;
    event Action? OnHide;

    Task<PopupResult> ShowAsync(PopupOptions options);
}
