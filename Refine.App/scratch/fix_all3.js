const fs = require('fs');

const path = "c:\\Library\\Projects\\Visual Studio\\Refine\\Refine.App\\Services\\LocalDbService.cs";
let content = fs.readFileSync(path, 'utf8');

// Replace Equipment = "..." with Equipment = Exercise.EquipmentType....
content = content.replace(/Equipment\s*=\s*"([^"]+)"/g, (match, p1) => {
    return `Equipment = Exercise.EquipmentType.${p1.replace(/\s/g, '')}`;
});

const startIdx = content.indexOf('var exercises = new List<Exercise>', content.indexOf('EGZERSİZLERİ OLUŞTUR'));

if (startIdx === -1) {
    console.error("Could not find start index");
    process.exit(1);
}

const endMarker = 'await _connection!.InsertAllAsync(exercises);';
const endIdx = content.indexOf(endMarker, startIdx) + endMarker.length;

if (endIdx < startIdx) {
    console.error("Could not find end index");
    process.exit(1);
}

const replacement = `var oldNameToExercise = new Dictionary<string, Exercise>
        {
            { "Bench Press", new Exercise {
                Name = "Bench Press", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Barbell, ImageUrl = "bench_press.png",
                CnsFatigueScore = 6.5m
            } },
            { "Squat", new Exercise {
                Name = "Squat", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Barbell, ImageUrl = "squat.png",
                CnsFatigueScore = 8.5m
            } },
            { "Deadlift", new Exercise {
                Name = "Deadlift", Difficulty = "Advanced", Equipment = Exercise.EquipmentType.Barbell, ImageUrl = "deadlift.png",
                CnsFatigueScore = 9.5m
            } },
            { "Overhead Press", new Exercise {
                Name = "Overhead Press", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Barbell, ImageUrl = "ohp.png",
                CnsFatigueScore = 7.0m
            } },
            { "Pull Up", new Exercise {
                Name = "Pull Up", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Bodyweight, ImageUrl = "pullup.png",
                CnsFatigueScore = 6.0m
            } },
            { "Barbell Row", new Exercise {
                Name = "Row", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Barbell, ImageUrl = "barbell_row.png",
                CnsFatigueScore = 7.5m
            } },
            { "Dumbbell Curl", new Exercise {
                Name = "Curl", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Dumbbell, ImageUrl = "curl.png",
                CnsFatigueScore = 3.0m
            } },
            { "Triceps Pushdown", new Exercise {
                Name = "Triceps Pushdown", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Machine, ImageUrl = "pushdown.png",
                CnsFatigueScore = 3.0m
            } },
            { "Lunges", new Exercise {
                Name = "Lunges", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Dumbbell, ImageUrl = "lunges.png",
                CnsFatigueScore = 6.0m
            } },
            { "Plank", new Exercise {
                Name = "Plank", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Bodyweight, ImageUrl = "plank.png",
                CnsFatigueScore = 4.0m
            } },
            { "Lateral Raise", new Exercise {
                Name = "Lateral Raise", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Dumbbell, ImageUrl = "lateral_raise.png",
                CnsFatigueScore = 3.5m
            } },
            { "Incline Dumbbell Press", new Exercise {
                Name = "Press", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Dumbbell,
                ImageUrl = "incline_press.png",
                CnsFatigueScore = 5.5m
            , VariationTags = new List<string> { "Incline" }} },
            { "Face Pull", new Exercise {
                Name = "Face Pull", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Cable, ImageUrl = "face_pull.png",
                CnsFatigueScore = 3.5m
            } },
            { "Hyperextension", new Exercise {
                Name = "Hyperextension", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Bodyweight,
                ImageUrl = "hyperextension.png",
                CnsFatigueScore = 4.0m
            } },
            { "Plate Loaded Chest Press", new Exercise {
                Name = "Chest Press", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Machine,
                ImageUrl = "chest_press.png",
                CnsFatigueScore = 5.0m
            , VariationTags = new List<string> { "Plate Loaded" }} },
            { "Smith Machine Low Incline Press", new Exercise {
                Name = "Press", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Machine,
                ImageUrl = "smith_incline_press.png",
                CnsFatigueScore = 5.0m
            , VariationTags = new List<string> { "Smith Machine", "Low Incline" }} },
            { "Chest Fly Machine", new Exercise {
                Name = "Chest Fly", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Machine,
                ImageUrl = "chest_fly_machine.png",
                CnsFatigueScore = 3.5m
            } },
            { "Shoulder Press Machine", new Exercise {
                Name = "Shoulder Press", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Machine,
                ImageUrl = "shoulder_press_machine.png",
                CnsFatigueScore = 4.0m
            } },
            { "Overhead Rope Extension", new Exercise {
                Name = "Overhead Extension", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Cable,
                ImageUrl = "overhead_rope_extension.png",
                CnsFatigueScore = 3.0m
            , VariationTags = new List<string> { "Rope" }} },
            { "Lat Pulldown", new Exercise {
                Name = "Lat Pulldown", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Machine, ImageUrl = "lat_pulldown.png",
                CnsFatigueScore = 4.5m
            } },
            { "Plate Loaded Wide Grip Row", new Exercise {
                Name = "Row", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Machine,
                ImageUrl = "wide_grip_row.png",
                CnsFatigueScore = 6.0m
            , VariationTags = new List<string> { "Plate Loaded", "Wide Grip" }} },
            { "Cable Row", new Exercise {
                Name = "Row", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Cable, ImageUrl = "cable_row.png",
                CnsFatigueScore = 4.5m
            } },
            { "Cable Curl", new Exercise {
                Name = "Curl", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Cable, ImageUrl = "cable_curl.png",
                CnsFatigueScore = 3.0m
            } },
            { "Hammer Curl", new Exercise {
                Name = "Hammer Curl", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Dumbbell, ImageUrl = "hammer_curl.png",
                CnsFatigueScore = 3.5m
            } },
            { "Reverse Barbell Curl", new Exercise {
                Name = "Reverse Curl", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Barbell,
                ImageUrl = "reverse_barbell_curl.png",
                CnsFatigueScore = 4.0m
            } },
            { "Leg Press", new Exercise {
                Name = "Leg Press", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Machine, ImageUrl = "leg_press.png",
                CnsFatigueScore = 6.5m
            } },
            { "Smith Machine Squat", new Exercise {
                Name = "Squat", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Machine,
                ImageUrl = "smith_squat.png",
                CnsFatigueScore = 6.5m
            , VariationTags = new List<string> { "Smith Machine" }} },
            { "Leg Extension", new Exercise {
                Name = "Leg Extension", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Machine, ImageUrl = "leg_extension.png",
                CnsFatigueScore = 4.0m
            } },
            { "Seated Leg Curl", new Exercise {
                Name = "Leg Curl", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Machine,
                ImageUrl = "seated_leg_curl.png",
                CnsFatigueScore = 4.0m
            , VariationTags = new List<string> { "Seated" }} },
            { "Cable Rear Delt Fly", new Exercise {
                Name = "Cable Rear Delt Fly", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Cable,
                ImageUrl = "cable_rear_delt_fly.png",
                CnsFatigueScore = 3.0m
            } },
            { "Close Grip Lat Pulldown", new Exercise {
                Name = "Lat Pulldown", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Machine,
                ImageUrl = "close_grip_lat_pulldown.png",
                CnsFatigueScore = 4.5m
            , VariationTags = new List<string> { "Close Grip" }} },
            { "Dips", new Exercise {
                Name = "Dips", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Bodyweight, ImageUrl = "dips.png",
                CnsFatigueScore = 5.0m
            } },
            { "Front Squat", new Exercise {
                Name = "Squat", Difficulty = "Advanced", Equipment = Exercise.EquipmentType.Barbell, ImageUrl = "front_squat.png",
                CnsFatigueScore = 8.0m
            , VariationTags = new List<string> { "Front" }} },
            { "Bulgarian Split Squat", new Exercise {
                Name = "Split Squat", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Dumbbell,
                ImageUrl = "bulgarian_split_squat.png",
                CnsFatigueScore = 6.5m
            , VariationTags = new List<string> { "Bulgarian" }} },
            { "Incline Barbell Bench Press", new Exercise {
                Name = "Bench Press", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Barbell,
                ImageUrl = "incline_barbell_bench.png",
                CnsFatigueScore = 6.0m
            , VariationTags = new List<string> { "Incline" }} },
            { "Close Grip Bench Press", new Exercise {
                Name = "Bench Press", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Barbell,
                ImageUrl = "close_grip_bench.png",
                CnsFatigueScore = 5.5m
            , VariationTags = new List<string> { "Close Grip" }} },
            { "Chin Up", new Exercise {
                Name = "Chin Up", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Bodyweight, ImageUrl = "chinup.png",
                CnsFatigueScore = 5.5m
            } },
            { "Weighted Pull Up", new Exercise {
                Name = "Pull Up", Difficulty = "Advanced", Equipment = Exercise.EquipmentType.Bodyweight,
                ImageUrl = "weighted_pullup.png",
                CnsFatigueScore = 7.0m
            , VariationTags = new List<string> { "Weighted" }} },
            { "Seated Dumbbell Press", new Exercise {
                Name = "Press", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Dumbbell,
                ImageUrl = "seated_dumbbell_press.png",
                CnsFatigueScore = 5.5m
            , VariationTags = new List<string> { "Seated" }} },
            { "Push Press", new Exercise {
                Name = "Push Press", Difficulty = "Advanced", Equipment = Exercise.EquipmentType.Barbell, ImageUrl = "push_press.png",
                CnsFatigueScore = 8.0m
            } },
            { "Romanian Deadlift", new Exercise {
                Name = "Deadlift", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Barbell, ImageUrl = "rdl.png",
                CnsFatigueScore = 7.5m
            , VariationTags = new List<string> { "Romanian" }} },
            { "Sumo Deadlift", new Exercise {
                Name = "Deadlift", Difficulty = "Advanced", Equipment = Exercise.EquipmentType.Barbell, ImageUrl = "sumo_deadlift.png",
                CnsFatigueScore = 9.0m
            , VariationTags = new List<string> { "Sumo" }} },
            { "Pendlay Row", new Exercise {
                Name = "Row", Difficulty = "Advanced", Equipment = Exercise.EquipmentType.Barbell, ImageUrl = "pendlay_row.png",
                CnsFatigueScore = 7.0m
            , VariationTags = new List<string> { "Pendlay" }} },
            { "T-Bar Row", new Exercise {
                Name = "Row", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Machine, ImageUrl = "tbar_row.png",
                CnsFatigueScore = 6.5m
            , VariationTags = new List<string> { "T-Bar" }} },
            { "Cable Lat Pull Over", new Exercise {
                Name = "Lat Pull Over", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Cable, ImageUrl = "lat_pullover.png",
                CnsFatigueScore = 5.0m
            } },
            { "Smith Incline Bench Press", new Exercise {
                Name = "Bench Press", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Machine,
                ImageUrl = "smith_incline_press.png", CnsFatigueScore = 6.0m
            , VariationTags = new List<string> { "Smith", "Incline" }} },
            { "Fly Machine", new Exercise {
                Name = "Chest Fly", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Machine, ImageUrl = "fly_machine.png",
                CnsFatigueScore = 4.0m
            } },
            { "Rear Delt Machine Fly", new Exercise {
                Name = "Rear Delt Fly", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Machine,
                ImageUrl = "rear_delt_machine_fly.png", CnsFatigueScore = 4.0m
            } },
            { "Seated Machine Row", new Exercise {
                Name = "Row", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Machine,
                ImageUrl = "seated_machine_row.png", CnsFatigueScore = 5.0m
            , VariationTags = new List<string> { "Seated" }} },
            { "One Arm Cable Row", new Exercise {
                Name = "Row", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Cable,
                ImageUrl = "one_arm_cable_row.png", CnsFatigueScore = 5.0m
            , VariationTags = new List<string> { "One Arm" }} },
            { "Cable Lateral Raise", new Exercise {
                Name = "Lateral Raise", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Cable,
                ImageUrl = "cable_lateral_raise.png", CnsFatigueScore = 4.0m
            } },
            { "Leg Raise", new Exercise {
                Name = "Leg Raise", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Bodyweight,
                ImageUrl = "leg_raise.png", CnsFatigueScore = 4.0m
            } },
            { "Calf Raise", new Exercise {
                Name = "Calf Raise", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Machine,
                ImageUrl = "calf_raise.png", CnsFatigueScore = 4.0m
            } },
            { "Shoulder Machine", new Exercise {
                Name = "Shoulder Press", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Machine,
                ImageUrl = "shoulder_machine.png", CnsFatigueScore = 5.0m
            } },
            { "Triceps Kickback", new Exercise {
                Name = "Triceps Kickback", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Dumbbell,
                ImageUrl = "triceps_kickback.png", CnsFatigueScore = 3.0m
            } },
            { "Ab Crunch", new Exercise {
                Name = "Ab Crunch", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Bodyweight,
                ImageUrl = "ab_crunch.png", CnsFatigueScore = 3.0m
            } }
        };

        // Toplu ekle
        await _connection!.InsertAllAsync(oldNameToExercise.Values);`;

content = content.substring(0, startIdx) + replacement + content.substring(endIdx);

// Change `int GetExId(string name) => dbExercises.FirstOrDefault(e => e.Name == name)?.Id ?? 0;`
// to `int GetExId(string name) => oldNameToExercise.ContainsKey(name) ? oldNameToExercise[name].Id : 0;`
content = content.replace(/int GetExId\(string name\) => dbExercises.FirstOrDefault\(e => e.Name == name\)\?.Id \?\? 0;/g,
    'int GetExId(string name) => oldNameToExercise.ContainsKey(name) ? oldNameToExercise[name].Id : 0;');

fs.writeFileSync(path, content, 'utf8');
console.log("Done");
