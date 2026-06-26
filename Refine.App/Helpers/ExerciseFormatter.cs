using System.Linq;
using Refine.App.Models.Entities;
using Refine.App.Models.UI;
using Refine.App.Models.Enums;

namespace Refine.App.Helpers;

public static class ExerciseFormatter
{
    public static string Format(Exercise? e, bool includeEquipment = true)
    {
        if (e == null) return "Bilinmeyen Hareket";

        var equipStr = includeEquipment && e.Equipment != EquipmentType.None && e.Equipment != EquipmentType.Bodyweight ? e.Equipment.ToString() + " " : "";
        return $"{equipStr}{e.Name}".TrimStart() +
               (e.VariationTags != null && e.VariationTags.Any() == true
                ? $" ({string.Join(", ", e.VariationTags)})"
                : "");
    }
}


