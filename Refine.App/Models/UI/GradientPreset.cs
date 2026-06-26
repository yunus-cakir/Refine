using System;
using System.Collections.Generic;
using System.Linq;

namespace Refine.App.Models.UI;

public class GradientColor
{
    public string CurrentPrimaryColor { get; set; } = string.Empty;
    public string CurrentSecondaryColor { get; set; } = string.Empty;
}

public class GradientPreset
{
    public string Name { get; set; } = string.Empty;
    public bool IsReversed { get; set; } = false;
    public List<GradientStop> Stops { get; set; } = new List<GradientStop>();

    public GradientColor GetColor(decimal percentage)
    {
        if (IsReversed)
        {
            percentage = 100m - percentage;
        }

        if (Stops == null || Stops.Count == 0) return new GradientColor { CurrentPrimaryColor = "#000000", CurrentSecondaryColor = "#000000" };

        var orderedStops = Stops.OrderBy(s => s.Percentage).ToList();
            
        if (percentage <= orderedStops.First().Percentage)
            return new GradientColor { CurrentPrimaryColor = orderedStops.First().PrimaryColor, CurrentSecondaryColor = orderedStops.First().SecondaryColor };
            
        if (percentage >= orderedStops.Last().Percentage)
            return new GradientColor { CurrentPrimaryColor = orderedStops.Last().PrimaryColor, CurrentSecondaryColor = orderedStops.Last().SecondaryColor };

        for (int i = 0; i < orderedStops.Count - 1; i++)
        {
            var start = orderedStops[i];
            var end = orderedStops[i + 1];

            if (percentage >= start.Percentage && percentage <= end.Percentage)
            {
                decimal range = end.Percentage - start.Percentage;
                decimal t = (percentage - start.Percentage) / range;
                
                return new GradientColor
                {
                    CurrentPrimaryColor = InterpolateColor(start.PrimaryColor, end.PrimaryColor, t),
                    CurrentSecondaryColor = InterpolateColor(start.SecondaryColor, end.SecondaryColor, t)
                };
            }
        }

        return new GradientColor { CurrentPrimaryColor = orderedStops.Last().PrimaryColor, CurrentSecondaryColor = orderedStops.Last().SecondaryColor };
    }

    private string InterpolateColor(string startHex, string endHex, decimal t)
    {
        var startColor = ParseHex(startHex);
        var endColor = ParseHex(endHex);

        int r = (int)(startColor.R + t * (endColor.R - startColor.R));
        int g = (int)(startColor.G + t * (endColor.G - startColor.G));
        int b = (int)(startColor.B + t * (endColor.B - startColor.B));

        return $"rgb({r}, {g}, {b})";
    }

    private (int R, int G, int B) ParseHex(string hex)
    {
        if (string.IsNullOrEmpty(hex)) return (0, 0, 0);
        if (hex.StartsWith("#")) hex = hex.Substring(1);
        if (hex.Length == 6)
        {
            return (
                Convert.ToInt32(hex.Substring(0, 2), 16),
                Convert.ToInt32(hex.Substring(2, 2), 16),
                Convert.ToInt32(hex.Substring(4, 2), 16)
            );
        }
        return (0, 0, 0); // Default fallback
    }
        
    // Static Presets
    public static readonly GradientPreset DefaultCns = new GradientPreset
    {
        Name = "DefaultCNS",
        Stops = new List<GradientStop>
        {
            new GradientStop { Percentage = 0m, PrimaryColor = "#28a745", SecondaryColor = "#48c765" },
            new GradientStop { Percentage = 25m, PrimaryColor = "#28a745", SecondaryColor = "#48c765" },
            new GradientStop { Percentage = 50m, PrimaryColor = "#ffc107", SecondaryColor = "#ffe127" },
            new GradientStop { Percentage = 75m, PrimaryColor = "#dc3545", SecondaryColor = "#fc5565" },
            new GradientStop { Percentage = 100m, PrimaryColor = "#6f42c1", SecondaryColor = "#8f62e1" }
        }
    };

    public static readonly GradientPreset Tier = new GradientPreset
    {
        Name = "Tier",
        Stops = new List<GradientStop>
        {
            new GradientStop { Percentage = 10m, PrimaryColor = "#FFFFFF", SecondaryColor = "#E5E5E5" },
            new GradientStop { Percentage = 30m, PrimaryColor = "#319236", SecondaryColor = "#5ACF60" },
            new GradientStop { Percentage = 50m, PrimaryColor = "#4C51F7", SecondaryColor = "#8A8DFF" },
            new GradientStop { Percentage = 70m, PrimaryColor = "#9D4DBB", SecondaryColor = "#D58CFF" },
            new GradientStop { Percentage = 90m, PrimaryColor = "#F3AF19", SecondaryColor = "#FFD66B" }
        }
    };

    public static readonly GradientPreset MuscleImpact = new GradientPreset
    {
        Name = "MuscleImpact",
        Stops = new List<GradientStop>
        {
            new GradientStop { Percentage = 10m, PrimaryColor = "#FFFFFF", SecondaryColor = "#E5E5E5" },
            new GradientStop { Percentage = 30m, PrimaryColor = "#dc3545", SecondaryColor = "#fc5565" },
            new GradientStop { Percentage = 50m, PrimaryColor = "#ffc107", SecondaryColor = "#ffe127" },
            new GradientStop { Percentage = 70m, PrimaryColor = "#28a745", SecondaryColor = "#48c765" }
        }
    };
}

public class GradientStop
{
    public decimal Percentage { get; set; }
    public string PrimaryColor { get; set; } = string.Empty;
    public string SecondaryColor { get; set; } = string.Empty;
}
