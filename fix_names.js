const fs = require('fs');
let content = fs.readFileSync('c:/Library/Projects/Visual Studio/Refine/Refine.App/Services/LocalDbService.cs', 'utf8');

// First block fix
content = content.replace(/Name = "Chest Fly Machine"/, 'Name = "Chest Fly"');
content = content.replace(/Name = "Shoulder Press Machine"/, 'Name = "Shoulder Press"');
content = content.replace(/Name = "Smith Machine Low Incline Press"([\s\S]*?)CnsFatigueScore = 5\.5m/, 'Name = "Press", VariationTags = new List<string> { "Smith", "Incline" }$1CnsFatigueScore = 5.5m');

// Second block fixes
content = content.replace(/Name = "Cable Lat Pull Over"/, 'Name = "Lat Pull Over"');
content = content.replace(/Name = "Smith Incline Bench Press"/, 'Name = "Bench Press", VariationTags = new List<string> { "Smith", "Incline" }');
content = content.replace(/Name = "Fly Machine"/, 'Name = "Chest Fly"');
content = content.replace(/Name = "Shoulder Machine"/, 'Name = "Shoulder Press"');
content = content.replace(/Name = "Rear Delt Machine Fly"/, 'Name = "Rear Delt Fly"');
content = content.replace(/Name = "Seated Machine Row"/, 'Name = "Row", VariationTags = new List<string> { "Seated" }');
content = content.replace(/Name = "One Arm Cable Row"/, 'Name = "Row", VariationTags = new List<string> { "One Arm" }');
content = content.replace(/Name = "Cable Lateral Raise"/, 'Name = "Lateral Raise"');
content = content.replace(/Name = "Hip Abductor Machine"/, 'Name = "Hip Abductor"');

fs.writeFileSync('c:/Library/Projects/Visual Studio/Refine/Refine.App/Services/LocalDbService.cs', content);
console.log('Fixed names');
