using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Refine.App.Services;

public static class UiHelper
{
    // Güvenli Onay Kutusu (DisplayAlert)
    public static async Task<bool> ShowConfirm(string title, string message, string accept, string cancel)
    {
        if (Application.Current is not null && Application.Current.Windows.Count > 0)
        {
            var page = Application.Current.Windows[0].Page;
            if (page is not null)
            {
                return await page.DisplayAlert(title, message, accept, cancel);
            }
        }
        return false; // Pencere bulunamazsa işlem iptal sayılır
    }

    // Güvenli Bilgi Kutusu
    public static async Task ShowAlert(string title, string message, string cancel)
    {
        if (Application.Current is not null && Application.Current.Windows.Count > 0)
        {
            var page = Application.Current.Windows[0].Page;
            if (page is not null)
            {
                await page.DisplayAlert(title, message, cancel);
            }
        }
    }
}