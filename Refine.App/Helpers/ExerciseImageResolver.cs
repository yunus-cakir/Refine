using System.Text.RegularExpressions;
using Refine.App.Models;

namespace Refine.App.Helpers;

public static class ExerciseImageResolver
{
    /// <summary>
    /// Generates a CSS background-image string with multiple fallback layers.
    /// It first tries the specific exercise image, then falls back to the equipment type image,
    /// and finally uses a generic fallback if neither exists.
    /// This relies on standard CSS behavior where failed image URLs (404) are rendered transparent,
    /// allowing the next layer to show through.
    /// </summary>
    public static string GetBackgroundImageCss(Exercise? exercise, bool includeFallback = true)
    {
        string genericFallback = "url('/images/Fallback.png')";

        if (exercise == null)
        {
            return includeFallback ? genericFallback : "none";
        }

        // 1. Specific exercise images
        // We generate two levels of specificity:
        // A) Equipment + Name (e.g., "Machine Bench Press" -> "MachineBenchPress.png")
        // B) Just Name (e.g., "Bench Press" -> "BenchPress.png")
        string baseName = Regex.Replace(exercise.Name, @"[^a-zA-Z0-9]", "");
        string equipPrefix = exercise.Equipment != Exercise.EquipmentType.None && exercise.Equipment != Exercise.EquipmentType.Bodyweight ? exercise.Equipment.ToString() : "";
        string equippedName = Regex.Replace($"{equipPrefix}{exercise.Name}", @"[^a-zA-Z0-9]", "");

        string specificImages = $"url('/images/{equippedName}.png')";
        if (equippedName != baseName)
        {
            specificImages += $", url('/images/{baseName}.png')";
        }

        // 2. Equipment type fallback (e.g., EquipmentType.Cable -> "EquipmentType_Cable.png")
        string equipmentName = exercise.Equipment.ToString();
        if (exercise.Equipment == Exercise.EquipmentType.Bodyweight)
        {
            equipmentName = "BodyWeight";
        }
        string equipmentFallback = $"url('/images/EquipmentType_{equipmentName}.png')";

        // If the equipment is Barbell or Dumbbell, we can also add Rack.png as an intermediate fallback since it exists in the examples
        if (exercise.Equipment == Exercise.EquipmentType.Barbell || exercise.Equipment == Exercise.EquipmentType.Dumbbell)
        {
            string rackFallback = "url('/images/Rack.png')";
            return includeFallback
                ? $"{specificImages}, {equipmentFallback}, {rackFallback}, {genericFallback}"
                : $"{specificImages}, {equipmentFallback}, {rackFallback}";
        }

        return includeFallback
            ? $"{specificImages}, {equipmentFallback}, {genericFallback}"
            : $"{specificImages}, {equipmentFallback}";
    }
}
