using System;
using System.Threading.Tasks;
using Refine.App.Models.Entities;
using Refine.App.Models.UI;
using Refine.App.Models.Enums;

namespace Refine.App.Services;

public interface IPopupService
{
    event Action<PopupOptions>? OnShow;
    event Action? OnHide;

    Task<PopupResult> ShowAsync(PopupOptions options);
}

