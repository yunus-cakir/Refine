using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Refine.App.Models
{
    public class AppSetting
    {
        [PrimaryKey]
        public string Key { get; set; } = ""; // Örn: "PreferredRIR", "Theme", "RestTimer"

        public string Value { get; set; } = ""; // Örn: "2", "Dark", "90"

        public string DataType { get; set; } = "string"; // "int", "bool", "string" (UI'da doğru input göstermek için)

        public string Category { get; set; } = "General"; // "General", "Workout", "Appearance"

        public string Description { get; set; } = ""; // Kullanıcıya ayarın ne olduğunu açıklamak için
    }
}
