const fs = require('fs');
let content = fs.readFileSync('c:/Library/Projects/Visual Studio/Refine/block.txt', 'utf8');

const regex = /new Exercise\s*\{([\s\S]*?)\}/g;
let matches = [...content.matchAll(regex)];

let entries = [];
for (let match of matches) {
    let props = match[1];
    let nameMatch = props.match(/Name\s*=\s*"([^"]+)"/);
    if (!nameMatch) continue;
    let oldName = nameMatch[1];
    let newName = oldName;
    let tags = [];

    if (oldName === 'Barbell Row') newName = 'Row';
    else if (oldName === 'Dumbbell Curl') newName = 'Curl';
    else if (oldName === 'Incline Dumbbell Press') { newName = 'Press'; tags.push('Incline'); }
    else if (oldName === 'Plate Loaded Chest Press') { newName = 'Chest Press'; tags.push('Plate Loaded'); }
    else if (oldName === 'Smith Machine Low Incline Press') { newName = 'Press'; tags.push('Smith Machine', 'Low Incline'); }
    else if (oldName === 'Overhead Rope Extension') { newName = 'Overhead Extension'; tags.push('Rope'); }
    else if (oldName === 'Plate Loaded Wide Grip Row') { newName = 'Row'; tags.push('Plate Loaded', 'Wide Grip'); }
    else if (oldName === 'Cable Row') newName = 'Row';
    else if (oldName === 'Cable Curl') newName = 'Curl';
    else if (oldName === 'Reverse Barbell Curl') { newName = 'Reverse Curl'; }
    else if (oldName === 'Smith Machine Squat') { newName = 'Squat'; tags.push('Smith Machine'); }
    else if (oldName === 'Seated Leg Curl') { newName = 'Leg Curl'; tags.push('Seated'); }
    else if (oldName === 'Close Grip Lat Pulldown') { newName = 'Lat Pulldown'; tags.push('Close Grip'); }
    else if (oldName === 'Front Squat') { newName = 'Squat'; tags.push('Front'); }
    else if (oldName === 'Bulgarian Split Squat') { newName = 'Split Squat'; tags.push('Bulgarian'); }
    else if (oldName === 'Incline Barbell Bench Press') { newName = 'Bench Press'; tags.push('Incline'); }
    else if (oldName === 'Close Grip Bench Press') { newName = 'Bench Press'; tags.push('Close Grip'); }
    else if (oldName === 'Weighted Pull Up') { newName = 'Pull Up'; tags.push('Weighted'); }
    else if (oldName === 'Seated Dumbbell Press') { newName = 'Press'; tags.push('Seated'); }
    else if (oldName === 'Romanian Deadlift') { newName = 'Deadlift'; tags.push('Romanian'); }
    else if (oldName === 'Sumo Deadlift') { newName = 'Deadlift'; tags.push('Sumo'); }
    else if (oldName === 'Pendlay Row') { newName = 'Row'; tags.push('Pendlay'); }
    else if (oldName === 'T-Bar Row') { newName = 'Row'; tags.push('T-Bar'); }
    else if (oldName === 'Rear Delt Machine Fly') { newName = 'Rear Delt Fly'; tags.push('Machine'); }

    props = props.replace(/Name\s*=\s*"([^"]+)"/, 'Name = "' + newName + '"');
    if (tags.length > 0) {
        props += ', VariationTags = new List<string> { ' + tags.map(t => '"' + t + '"').join(', ') + ' }';
    }

    entries.push('            { "' + oldName + '", new Exercise {' + props + '} }');
}

let newCode = 'var oldNameToExercise = new Dictionary<string, Exercise>\n        {\n' + entries.join(',\n') + '\n        };\n\n        // Toplu ekle\n        await _connection!.InsertAllAsync(oldNameToExercise.Values);\n\n        // ID eşleşmesi\n        var dbExercises = await _connection.Table<Exercise>().ToListAsync();\n\n        // Helper fonksiyon\n        int GetExId(string name) => oldNameToExercise.ContainsKey(name) ? oldNameToExercise[name].Id : 0;';

fs.writeFileSync('c:/Library/Projects/Visual Studio/Refine/new_block.txt', newCode);
console.log('Done!');
