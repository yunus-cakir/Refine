const fs = require('fs');
const path = "c:\\Library\\Projects\\Visual Studio\\Refine\\Refine.App\\Services\\LocalDbService.cs";
let content = fs.readFileSync(path, 'utf8');

// 1. Serialization loop
if (!content.includes('System.Text.Json.JsonSerializer.Serialize')) {
    content = content.replace(
        'await _connection!.InsertAllAsync(oldNameToExercise.Values);',
        'foreach (var ex in oldNameToExercise.Values)\n        {\n            if (ex.VariationTags != null && ex.VariationTags.Any())\n            {\n                ex.VariationTagsBlob = System.Text.Json.JsonSerializer.Serialize(ex.VariationTags);\n            }\n        }\n        await _connection!.InsertAllAsync(oldNameToExercise.Values);'
    );
    console.log("Added serialization loop.");
}

// 2. Fix SeedMockProgramAsync GetExId
if (content.includes('int GetExId(string name) => oldNameToExercise.ContainsKey(name) ? oldNameToExercise[name].Id : 0;\n\n        var mockMappings')) {
    content = content.replace(
        'int GetExId(string name) => oldNameToExercise.ContainsKey(name) ? oldNameToExercise[name].Id : 0;\n\n        var mockMappings',
        'int GetExId(string name, Exercise.EquipmentType? eq = null, string tag = "") => dbExercises.FirstOrDefault(e => e.Name == name && (eq == null || e.Equipment == eq) && (string.IsNullOrEmpty(tag) || (!string.IsNullOrEmpty(e.VariationTagsBlob) && e.VariationTagsBlob.Contains(tag))))?.Id ?? 0;\n\n        var mockMappings'
    );
    console.log("Fixed GetExId in SeedMockProgramAsync.");
}

// 3. Fix SeedAgirsaglam5x5ProgramAsync GetExId
if (content.includes('int GetExId(string name) => oldNameToExercise.ContainsKey(name) ? oldNameToExercise[name].Id : 0;\n\n        var program')) {
    content = content.replace(
        'int GetExId(string name) => oldNameToExercise.ContainsKey(name) ? oldNameToExercise[name].Id : 0;\n\n        var program',
        'int GetExId(string name) => dbExercises.FirstOrDefault(e => e.Name == name)?.Id ?? 0;\n\n        var program'
    );
    console.log("Fixed GetExId in SeedAgirsaglam5x5ProgramAsync.");
}

fs.writeFileSync(path, content, 'utf8');
console.log("Done");
