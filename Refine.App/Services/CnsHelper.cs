namespace Refine.App.Services;

public static class CnsHelper
{
    public static string GetCnsColor(decimal percentage)
    {
        // Clamp percentage between 0 and 100
        percentage = Math.Max(0, Math.Min(100, percentage));

        // Colors
        var green = new { R = 40, G = 167, B = 69 };   // #28a745
        var yellow = new { R = 255, G = 193, B = 7 };  // #ffc107
        var red = new { R = 220, G = 53, B = 69 };     // #dc3545
        var purple = new { R = 111, G = 66, B = 193 }; // #6f42c1

        int r, g, b;

        if (percentage <= 25)
        {
            // 0-30 arası tamamen yeşil
            r = green.R;
            g = green.G;
            b = green.B;
        }
        else if (percentage <= 50)
        {
            // 30-60 arası Yeşil'den Sarı'ya gradyan
            decimal t = (percentage - 25m) / 25m;
            r = (int)(green.R + t * (yellow.R - green.R));
            g = (int)(green.G + t * (yellow.G - green.G));
            b = (int)(green.B + t * (yellow.B - green.B));
        }
        else if (percentage <= 75)
        {
            // 60-90 arası Sarı'dan Kırmızı'ya gradyan
            decimal t = (percentage - 50m) / 25m;
            r = (int)(yellow.R + t * (red.R - yellow.R));
            g = (int)(yellow.G + t * (red.G - yellow.G));
            b = (int)(yellow.B + t * (red.B - yellow.B));
        }
        else
        {
            // 90-100 arası Kırmızı'dan Mor'a gradyan
            decimal t = (percentage - 75m) / 25m;
            r = (int)(red.R + t * (purple.R - red.R));
            g = (int)(red.G + t * (purple.G - red.G));
            b = (int)(red.B + t * (purple.B - red.B));
        }

        return $"rgb({r}, {g}, {b})";
    }
}
