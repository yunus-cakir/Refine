const fs = require('fs');
let content = fs.readFileSync('c:/Library/Projects/Visual Studio/Refine/Refine.App/Services/LocalDbService.cs', 'utf8');

// Step 1: Re-apply the dictionary replacement
let newBlock = fs.readFileSync('c:/Library/Projects/Visual Studio/Refine/new_block.txt', 'utf8');
const regex1 = /var exercises = new List<Exercise>\s*\{[\s\S]*?int GetExId\(string name\) => dbExercises\.FirstOrDefault\(e => e\.Name == name\)\?\.Id \?\? 0;/g;
content = content.replace(regex1, newBlock);

// Step 2: Fix names in both blocks
content = content.replace(/Name = "Chest Fly Machine"/, 'Name = "Chest Fly"');
content = content.replace(/Name = "Shoulder Press Machine"/, 'Name = "Shoulder Press"');
content = content.replace(/Name = "Smith Machine Low Incline Press"([\s\S]*?)CnsFatigueScore = 5\.5m/, 'Name = "Press", VariationTags = new List<string> { "Smith", "Incline" }$1CnsFatigueScore = 5.5m');

content = content.replace(/Name = "Cable Lat Pull Over"/, 'Name = "Lat Pull Over"');
content = content.replace(/Name = "Smith Incline Bench Press"/, 'Name = "Bench Press", VariationTags = new List<string> { "Smith", "Incline" }');
content = content.replace(/Name = "Fly Machine"/, 'Name = "Chest Fly"');
content = content.replace(/Name = "Shoulder Machine"/, 'Name = "Shoulder Press"');
content = content.replace(/Name = "Rear Delt Machine Fly"/, 'Name = "Rear Delt Fly"');
content = content.replace(/Name = "Seated Machine Row"/, 'Name = "Row", VariationTags = new List<string> { "Seated" }');
content = content.replace(/Name = "One Arm Cable Row"/, 'Name = "Row", VariationTags = new List<string> { "One Arm" }');
content = content.replace(/Name = "Cable Lateral Raise"/, 'Name = "Lateral Raise"');
content = content.replace(/Name = "Hip Abductor Machine"/, 'Name = "Hip Abductor"');

// Step 3: Replace string Equipment with EquipmentType enum
content = content.replace(/Equipment\s*=\s*"Barbell"/g, 'Equipment = Exercise.EquipmentType.Barbell');
content = content.replace(/Equipment\s*=\s*"Dumbbell"/g, 'Equipment = Exercise.EquipmentType.Dumbbell');
content = content.replace(/Equipment\s*=\s*"Machine"/g, 'Equipment = Exercise.EquipmentType.Machine');
content = content.replace(/Equipment\s*=\s*"Cable"/g, 'Equipment = Exercise.EquipmentType.Cable');
content = content.replace(/Equipment\s*=\s*"Bodyweight"/g, 'Equipment = Exercise.EquipmentType.Bodyweight');
content = content.replace(/Equipment\s*=\s*"Kettlebell"/g, 'Equipment = Exercise.EquipmentType.Kettlebell');

fs.writeFileSync('c:/Library/Projects/Visual Studio/Refine/Refine.App/Services/LocalDbService.cs', content);
console.log('Successfully ran all fixes');
