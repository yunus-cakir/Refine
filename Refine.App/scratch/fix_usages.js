const fs = require('fs');

const path = "c:\\Library\\Projects\\Visual Studio\\Refine\\Refine.App\\Services\\LocalDbService.cs";
let content = fs.readFileSync(path, 'utf8');

// Replace usages in SeedMockProgramAsync
content = content.replace(/GetExId\("Cable Lat Pull Over"\)/g, 'GetExId("Lat Pull Over", Exercise.EquipmentType.Cable)');
content = content.replace(/GetExId\("Smith Incline Bench Press"\)/g, 'GetExId("Bench Press", Exercise.EquipmentType.Machine, "Smith")');
content = content.replace(/GetExId\("Fly Machine"\)/g, 'GetExId("Chest Fly", Exercise.EquipmentType.Machine)');
content = content.replace(/GetExId\("Rear Delt Machine Fly"\)/g, 'GetExId("Rear Delt Fly", Exercise.EquipmentType.Machine)');

// Also fix some others that were mapped to base names
content = content.replace(/GetExId\("Seated Machine Row"\)/g, 'GetExId("Row", Exercise.EquipmentType.Machine, "Seated")');
content = content.replace(/GetExId\("One Arm Cable Row"\)/g, 'GetExId("Row", Exercise.EquipmentType.Cable, "One Arm")');
content = content.replace(/GetExId\("Cable Lateral Raise"\)/g, 'GetExId("Lateral Raise", Exercise.EquipmentType.Cable)');
content = content.replace(/GetExId\("Shoulder Machine"\)/g, 'GetExId("Shoulder Press", Exercise.EquipmentType.Machine)');

fs.writeFileSync(path, content, 'utf8');
console.log("Done");
