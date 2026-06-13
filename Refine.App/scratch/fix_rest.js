const fs = require('fs');

const path = "c:\\Library\\Projects\\Visual Studio\\Refine\\Refine.App\\Services\\LocalDbService.cs";
let content = fs.readFileSync(path, 'utf8');

// Fix GetExId in SeedMockProgramAsync
content = content.replace(
    '        int GetExId(string name, Exercise.EquipmentType? eq = null, string tag = "") => oldNameToExercise.ContainsKey(name) ? oldNameToExercise[name].Id : 0;\n\n        var mockMappings = new List<ExerciseMuscleMap>',
    '        int GetExId(string name, Exercise.EquipmentType? eq = null, string tag = "") => dbExercises.FirstOrDefault(e => e.Name == name && (eq == null || e.Equipment == eq) && (string.IsNullOrEmpty(tag) || (!string.IsNullOrEmpty(e.VariationTagsBlob) && e.VariationTagsBlob.Contains(tag))))?.Id ?? 0;\n\n        var mockMappings = new List<ExerciseMuscleMap>'
);

// Wait, fix_all3.js replaced it with `oldNameToExercise.ContainsKey(name) ? oldNameToExercise[name].Id : 0;`
// and fix_mock_program.js replaced `int GetExId(string name) => dbExercises.FirstOrDefault...` which didn't match anymore!
// Let's just manually search and replace what fix_all3.js left!

// In SeedMockProgramAsync:
content = content.replace(
    '        int GetExId(string name) => oldNameToExercise.ContainsKey(name) ? oldNameToExercise[name].Id : 0;\n\n        var mockMappings = new List<ExerciseMuscleMap>',
    '        int GetExId(string name, Exercise.EquipmentType? eq = null, string tag = "") => dbExercises.FirstOrDefault(e => e.Name == name && (eq == null || e.Equipment == eq) && (string.IsNullOrEmpty(tag) || (!string.IsNullOrEmpty(e.VariationTagsBlob) && e.VariationTagsBlob.Contains(tag))))?.Id ?? 0;\n\n        var mockMappings = new List<ExerciseMuscleMap>'
);

// In SeedAgirsaglam5x5ProgramAsync:
content = content.replace(
    '        int GetExId(string name) => oldNameToExercise.ContainsKey(name) ? oldNameToExercise[name].Id : 0;\n\n        var program = new WorkoutProgram',
    '        int GetExId(string name) => dbExercises.FirstOrDefault(e => e.Name == name)?.Id ?? 0;\n\n        var program = new WorkoutProgram'
);

// Add the serialization loop before InsertAllAsync:
content = content.replace(
    '        await _connection!.InsertAllAsync(oldNameToExercise.Values);\n\n        // ID e',
    '        foreach (var ex in oldNameToExercise.Values)\n        {\n            if (ex.VariationTags != null && ex.VariationTags.Any())\n            {\n                ex.VariationTagsBlob = System.Text.Json.JsonSerializer.Serialize(ex.VariationTags);\n            }\n        }\n        await _connection!.InsertAllAsync(oldNameToExercise.Values);\n\n        // ID e'
);

fs.writeFileSync(path, content, 'utf8');
console.log("Done");
