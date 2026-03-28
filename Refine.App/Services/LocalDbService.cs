using SQLite;
using Refine.App.Models;

namespace Refine.App.Services;

public class LocalDbService
{
    private SQLiteAsyncConnection? _connection;

    // Veritabanı bağlantısını başlat
    private async Task Init()
    {
        if (_connection is not null)
            return;

        _connection = new SQLiteAsyncConnection(DbConstants.DatabasePath, DbConstants.Flags);

        // Tabloları oluştur
        await _connection.CreateTableAsync<Exercise>();
        await _connection.CreateTableAsync<WorkoutProgram>();
        await _connection.CreateTableAsync<Workout>();
        await _connection.CreateTableAsync<WorkoutItem>();
        await _connection.CreateTableAsync<WorkoutLog>();
        await _connection.CreateTableAsync<User>();
        await _connection.CreateTableAsync<AppSetting>();

        // Eğer Egzersiz tablosu boşsa, örnek verileri yükle
        if (await _connection.Table<Exercise>().CountAsync() == 0)
        {
            await SeedDataAsync();
            await SeedUserSettingsAsync();
        }
    }

    // --------------------------------------------
    //                  USER CRUD
    // --------------------------------------------

    // Tek bir kullanıcı olduğu için (Local App) hep ilk kullanıcıyı çekeriz
    public async Task<User> GetUserAsync()
    {
        await Init();
        var user = await _connection!.Table<User>().FirstOrDefaultAsync();
        if (user == null)
        {
            user = new User
            {
                FirstName = "Sporcu",
                LastName = "",
                Height = 180,
                Weight = 80,
                Language = "Tr"
            };
            await _connection.InsertAsync(user);
        }
        return user;
    }

    public async Task UpdateUserAsync(User user)
    {
        await Init();
        await _connection!.UpdateAsync(user);
    }

    // --------------------------------------------
    //               SETTINGS CRUD
    // --------------------------------------------

    public async Task<List<AppSetting>> GetAllSettingsAsync()
    {
        await Init();
        return await _connection!.Table<AppSetting>().ToListAsync();
    }

    public async Task<T> GetSettingValueAsync<T>(string key, T defaultValue)
    {
        await Init();
        var setting = await _connection!.Table<AppSetting>().Where(s => s.Key == key).FirstOrDefaultAsync();

        if (setting == null) return defaultValue;

        try
        {
            return (T)Convert.ChangeType(setting.Value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }

    public async Task UpdateSettingAsync(AppSetting setting)
    {
        await Init();
        await _connection!.UpdateAsync(setting);
    }

    // Varsayılan Ayarları ve Kullanıcıyı Yükle
    private async Task SeedUserSettingsAsync()
    {
        // Kullanıcı yoksa oluştur
        var userCount = await _connection!.Table<User>().CountAsync();
        if (userCount == 0)
        {
            await _connection.InsertAsync(new User { FirstName = "New", LastName = "User", Height = 175, Weight = 75 });
        }

        // Varsayılan Ayarlar (Admin Panel mantığı burasıdır. Buraya eklediğin her şey ayarlar sayfasına düşer)
        var defaultSettings = new List<AppSetting>
        {
            new AppSetting { Key = "PreferredRIR", Value = "2", DataType = "int", Category = "Workout", Description = "Varsayılan Rezerv Tekrar (RIR)" },
            new AppSetting { Key = "DefaultRestTime", Value = "60", DataType = "int", Category = "Workout", Description = "Varsayılan Dinlenme Süresi (sn)" },
            new AppSetting { Key = "ShowTips", Value = "true", DataType = "bool", Category = "General", Description = "İpuçlarını Göster" },
            new AppSetting { Key = "WeightUnit", Value = "kg", DataType = "string", Category = "General", Description = "Ağırlık Birimi (kg/lbs)" }
        };

        foreach (var def in defaultSettings)
        {
            var existing = await _connection.Table<AppSetting>().Where(s => s.Key == def.Key).FirstOrDefaultAsync();
            if (existing == null)
            {
                await _connection.InsertAsync(def);
            }
        }
    }

    // --------------------------------------------
    //              EXERCISE CRUD
    // --------------------------------------------

    public async Task<Exercise?> GetExerciseByIdAsync(int id)
    {
        await Init();
        return await _connection!.Table<Exercise>().Where(e => e.Id == id).FirstOrDefaultAsync();
    }

    public async Task SaveExerciseAsync(Exercise exercise)
    {
        await Init();
        if (exercise.Id != 0)
            await _connection!.UpdateAsync(exercise);
        else
            await _connection!.InsertAsync(exercise);
    }

    public async Task DeleteExerciseAsync(Exercise exercise)
    {
        await Init();
        await _connection!.DeleteAsync(exercise);
    }

    public async Task<List<Exercise>> GetExercisesAsync()
    {
        await Init();
        return await _connection!.Table<Exercise>().ToListAsync();
    }

    // --------------------------------------------
    //              WORKOUT CRUD
    // --------------------------------------------

    public async Task SaveWorkoutWithItemsAsync(Workout workout)
    {
        await Init();

        if (workout.Id != 0)
            await _connection!.UpdateAsync(workout);
        else
            await _connection!.InsertAsync(workout);

        var oldItems = await _connection.Table<WorkoutItem>().Where(i => i.WorkoutId == workout.Id).ToListAsync();
        foreach (var old in oldItems)
        {
            await _connection.DeleteAsync(old);
        }

        if (workout.Items != null && workout.Items.Any())
        {
            foreach (var item in workout.Items)
            {
                item.Id = 0;
                item.WorkoutId = workout.Id;
                await _connection.InsertAsync(item);
            }
        }
    }

    public async Task DeleteWorkoutAsync(int workoutId)
    {
        await Init();

        var items = await _connection!.Table<WorkoutItem>().Where(i => i.WorkoutId == workoutId).ToListAsync();
        foreach (var item in items) await _connection.DeleteAsync(item);

        var workout = await _connection.Table<Workout>().Where(w => w.Id == workoutId).FirstOrDefaultAsync();
        if (workout != null) await _connection.DeleteAsync(workout);
    }

    // --------------------------------------------
    //      STANDALONE WORKOUT CRUD
    // --------------------------------------------

    public async Task<List<Workout>> GetStandaloneWorkoutsAsync()
    {
        await Init();

        var workouts = await _connection!.Table<Workout>()
                                        .Where(w => w.WorkoutProgramId == 0)
                                        .OrderByDescending(w => w.Id)
                                        .ToListAsync();

        foreach (var w in workouts)
        {
            var items = await _connection.Table<WorkoutItem>().Where(wi => wi.WorkoutId == w.Id).ToListAsync();
            w.Items = items;
        }

        return workouts;
    }

    // --------------------------------------------
    //                  WORKOUTLOG
    // --------------------------------------------

    public async Task SaveWorkoutLogsAsync(List<WorkoutLog> logs)
    {
        await Init();
        await _connection!.InsertAllAsync(logs);
    }

    public async Task<List<WorkoutLog>> GetLastLogForExerciseAsync(int exerciseId)
    {
        await Init();

        // 1. Bu harekete ait EN SON girilen kaydı (muhtemelen son set) bul
        var lastLog = await _connection!.Table<WorkoutLog>()
                                        .Where(l => l.ExerciseId == exerciseId)
                                        .OrderByDescending(l => l.Date)
                                        .FirstOrDefaultAsync();

        if (lastLog == null) return new List<WorkoutLog>();

        // 2. DÜZELTME: Birebir eşleşme (==) yerine toleranslı aralık kullanıyoruz.
        // Çünkü döngü içinde oluşturulan kayıtların milisaniyeleri farklı olabilir.
        // Son kaydın 1 dakika öncesi ve 1 dakika sonrası aralığındaki tüm kayıtları o seans kabul ediyoruz.

        var minDate = lastLog.Date.AddMinutes(-1);
        var maxDate = lastLog.Date.AddMinutes(1);

        return await _connection.Table<WorkoutLog>()
                                .Where(l => l.ExerciseId == exerciseId
                                         && l.Date >= minDate
                                         && l.Date <= maxDate)
                                .OrderBy(l => l.SetNumber)
                                .ToListAsync();
    }

    // LOG HISTORY

    // Tüm log kayıtlarını getir (Tarihe göre tersten sıralı)
    public async Task<List<WorkoutLog>> GetAllLogsAsync()
    {
        await Init();
        return await _connection!.Table<WorkoutLog>()
                                .OrderByDescending(l => l.Date)
                                .ToListAsync();
    }

    // Belirli bir tarih ve antrenman ID'sine göre logları getir (Detay sayfası için)
    // Not: Tarihi string veya Tick olarak karşılaştırmak daha güvenlidir.
    public async Task<List<WorkoutLog>> GetSessionLogsAsync(int workoutId, DateTime date)
    {
        await Init();

        // SQLite'ta tarih karşılaştırması bazen hassas olabilir, 
        // bu yüzden o günün başlangıcı ve bitişi aralığını alıyoruz.
        var startDate = date.Date;
        var endDate = date.Date.AddDays(1).AddTicks(-1);

        return await _connection!.Table<WorkoutLog>()
                                .Where(l => l.WorkoutId == workoutId && l.Date >= startDate && l.Date <= endDate)
                                .ToListAsync();
    }

    // --- KONTROL ---
    public async Task<bool> HasLogForTodayAsync(int workoutId)
    {
        await Init();
        var today = DateTime.Now.Date;
        var tomorrow = today.AddDays(1);

        // Bugünün tarih aralığında bu workoutId ile girilmiş herhangi bir log var mı?
        var count = await _connection!.Table<WorkoutLog>()
                                     .Where(l => l.WorkoutId == workoutId && l.Date >= today && l.Date < tomorrow)
                                     .CountAsync();
        return count > 0;
    }

    // --- DÜZENLEME KAYDI (Edit Mode İçin) ---
    // Hem var olanları günceller, hem yeni eklenenleri kaydeder.
    public async Task UpdateSessionLogsAsync(List<WorkoutLog> logs)
    {
        await Init();
        await _connection!.RunInTransactionAsync(tran =>
        {
            foreach (var log in logs)
            {
                // ID'si 0'dan büyükse veritabanında var demektir -> Güncelle
                if (log.Id != 0)
                    tran.Update(log);
                // ID'si 0 ise yeni eklenmiştir -> Ekle
                else
                    tran.Insert(log);
            }
        });
    }

    // --------------------------------------------
    //              WORKOUTPROGRAM CRUD
    // --------------------------------------------

    public async Task<int> SaveWorkoutProgramAsync(WorkoutProgram program)
    {
        await Init();
        if (program.Id != 0)
        {
            await _connection!.UpdateAsync(program);
            return program.Id;
        }
        else
        {
            await _connection!.InsertAsync(program);
            return program.Id;
        }
    }

    public async Task DuplicateWorkoutToProgramAsync(int sourceWorkoutId, int targetProgramId)
    {
        await Init();

        var sourceWorkout = await GetWorkoutByIdAsync(sourceWorkoutId);
        if (sourceWorkout == null) return;

        var existingCount = await _connection!.Table<Workout>().Where(w => w.WorkoutProgramId == targetProgramId).CountAsync();

        var newWorkout = new Workout
        {
            WorkoutProgramId = targetProgramId,
            Name = sourceWorkout.Name,
            Description = sourceWorkout.Description,
            Order = existingCount + 1
        };
        await _connection!.InsertAsync(newWorkout);

        if (sourceWorkout.Items != null)
        {
            foreach (var item in sourceWorkout.Items)
            {
                var newItem = new WorkoutItem
                {
                    WorkoutId = newWorkout.Id,
                    ExerciseId = item.ExerciseId,
                    Sets = item.Sets,
                    RepsRange = item.RepsRange,
                    Order = item.Order
                };
                await _connection!.InsertAsync(newItem);
            }
        }
    }

    public async Task UpdateWorkoutOrdersAsync(List<Workout> workouts)
    {
        await Init();
        await _connection!.RunInTransactionAsync(tran =>
        {
            foreach (var workout in workouts)
            {
                tran.Update(workout);
            }
        });
    }

    public async Task<List<WorkoutProgram>> GetAllProgramsAsync()
    {
        await Init();
        return await _connection!.Table<WorkoutProgram>().ToListAsync();
    }

    public async Task<WorkoutProgram?> GetProgramByIdAsync(int id)
    {
        await Init();

        var program = await _connection!.Table<WorkoutProgram>()
                                        .Where(p => p.Id == id)
                                        .FirstOrDefaultAsync();
        if (program == null) return null;

        program.Workouts = await _connection.Table<Workout>()
                                            .Where(w => w.WorkoutProgramId == id)
                                            .OrderBy(w => w.Order)
                                            .ToListAsync();

        foreach (var workout in program.Workouts)
        {
            workout.Items = await _connection.Table<WorkoutItem>()
                                             .Where(wi => wi.WorkoutId == workout.Id)
                                             .ToListAsync();
        }

        return program;
    }

    // DÜZELTME: Items listesi ve içindeki Exercise nesnesi burada dolduruluyor
    public async Task<Workout?> GetWorkoutByIdAsync(int workoutId)
    {
        await Init();

        var workout = await _connection!.Table<Workout>()
                                        .Where(w => w.Id == workoutId)
                                        .FirstOrDefaultAsync();
        if (workout == null) return null;

        var items = await _connection.Table<WorkoutItem>()
                                     .Where(wi => wi.WorkoutId == workoutId)
                                     .OrderBy(wi => wi.Order)
                                     .ToListAsync();

        foreach (var item in items)
        {
            item.Exercise = await _connection.Table<Exercise>()
                                             .Where(e => e.Id == item.ExerciseId)
                                             .FirstOrDefaultAsync();
        }

        workout.Items = items;
        return workout;
    }

    public async Task DeleteProgramAsync(int programId)
    {
        await Init();

        var workouts = await _connection!.Table<Workout>()
                                        .Where(w => w.WorkoutProgramId == programId)
                                        .ToListAsync();

        foreach (var workout in workouts)
        {
            await DeleteWorkoutAsync(workout.Id);
        }

        var program = await _connection.Table<WorkoutProgram>()
                                       .Where(p => p.Id == programId)
                                       .FirstOrDefaultAsync();

        if (program != null)
        {
            await _connection.DeleteAsync(program);
        }
    }

    private async Task SeedDataAsync()
    {
        // --- 1. EGZERSİZLERİ OLUŞTUR ---
        var exercises = new List<Exercise>
        {
            new Exercise { Name = "Bench Press", Difficulty = "Intermediate", Equipment = "Barbell", MuscleGroup = "Chest", ImageUrl = "bench_press.png" },
            new Exercise { Name = "Squat", Difficulty = "Intermediate", Equipment = "Barbell", MuscleGroup = "Legs", ImageUrl = "squat.png" },
            new Exercise { Name = "Deadlift", Difficulty = "Advanced", Equipment = "Barbell", MuscleGroup = "Back", ImageUrl = "deadlift.png" },
            new Exercise { Name = "Overhead Press", Difficulty = "Intermediate", Equipment = "Barbell", MuscleGroup = "Shoulders", ImageUrl = "ohp.png" },
            new Exercise { Name = "Pull Up", Difficulty = "Intermediate", Equipment = "Bodyweight", MuscleGroup = "Back", ImageUrl = "pullup.png" },
            
            // --- EKSİK OLAN HAREKET BURAYA EKLENDİ ---
            new Exercise { Name = "Barbell Row", Difficulty = "Intermediate", Equipment = "Barbell", MuscleGroup = "Back", ImageUrl = "barbell_row.png" },
            // -----------------------------------------

            new Exercise { Name = "Dumbbell Curl", Difficulty = "Beginner", Equipment = "Dumbbell", MuscleGroup = "Arms", ImageUrl = "curl.png" },
            new Exercise { Name = "Triceps Pushdown", Difficulty = "Beginner", Equipment = "Machine", MuscleGroup = "Arms", ImageUrl = "pushdown.png" },
            new Exercise { Name = "Lunges", Difficulty = "Beginner", Equipment = "Dumbbell", MuscleGroup = "Legs", ImageUrl = "lunges.png" },
            new Exercise { Name = "Plank", Difficulty = "Beginner", Equipment = "Bodyweight", MuscleGroup = "Core", ImageUrl = "plank.png" },
            new Exercise { Name = "Lateral Raise", Difficulty = "Beginner", Equipment = "Dumbbell", MuscleGroup = "Shoulders", ImageUrl = "lateral_raise.png" }
        };

        // Toplu ekle
        await _connection!.InsertAllAsync(exercises);

        // ID eşleşmesi için veritabanından geri çek
        var dbExercises = await _connection.Table<Exercise>().ToListAsync();

        // Helper fonksiyon
        int GetExId(string name) => dbExercises.FirstOrDefault(e => e.Name == name)?.Id ?? 0;


        // --- 2. PROGRAM 1: BAŞLANGIÇ ---
        var program1 = new WorkoutProgram
        {
            Name = "Start Strong",
            Level = "Beginner",
            Goal = "Temel Kuvvet",
            TargetMuscles = "Tüm Vücut",
            Environment = "Spor Salonu"
        };
        await _connection.InsertAsync(program1);

        // Program 1 - Gün A
        var p1_w1 = new Workout { WorkoutProgramId = program1.Id, Name = "Full Body A", Order = 1, Description = "Temel itiş ve bacak odaklı." };
        await _connection.InsertAsync(p1_w1);
        await _connection.InsertAllAsync(new[] {
            new WorkoutItem { WorkoutId = p1_w1.Id, ExerciseId = GetExId("Squat"), Sets = 3, RepsRange = "8-10", Order = 1 },
            new WorkoutItem { WorkoutId = p1_w1.Id, ExerciseId = GetExId("Bench Press"), Sets = 3, RepsRange = "8-12", Order = 2 },
            new WorkoutItem { WorkoutId = p1_w1.Id, ExerciseId = GetExId("Barbell Row"), Sets = 3, RepsRange = "10-12", Order = 3 }, // Artık ID'si 0 dönmeyecek!
             new WorkoutItem { WorkoutId = p1_w1.Id, ExerciseId = GetExId("Dumbbell Curl"), Sets = 3, RepsRange = "12-15", Order = 4 }
        });

        // Program 1 - Gün B
        var p1_w2 = new Workout { WorkoutProgramId = program1.Id, Name = "Full Body B", Order = 2, Description = "Çekiş ve omuz odaklı." };
        await _connection.InsertAsync(p1_w2);
        await _connection.InsertAllAsync(new[] {
            new WorkoutItem { WorkoutId = p1_w2.Id, ExerciseId = GetExId("Deadlift"), Sets = 3, RepsRange = "5", Order = 1 },
            new WorkoutItem { WorkoutId = p1_w2.Id, ExerciseId = GetExId("Overhead Press"), Sets = 3, RepsRange = "8-10", Order = 2 },
            new WorkoutItem { WorkoutId = p1_w2.Id, ExerciseId = GetExId("Pull Up"), Sets = 3, RepsRange = "Max", Order = 3 },
            new WorkoutItem { WorkoutId = p1_w2.Id, ExerciseId = GetExId("Plank"), Sets = 3, RepsRange = "45sn", Order = 4 }
        });


        // --- 3. PROGRAM 2: PPL ---
        var program2 = new WorkoutProgram
        {
            Name = "Classic PPL",
            Level = "Intermediate",
            Goal = "Hipertrofi (Kas Kütlesi)",
            TargetMuscles = "Bölgesel",
            Environment = "Spor Salonu"
        };
        await _connection.InsertAsync(program2);

        // Push Day
        var p2_push = new Workout { WorkoutProgramId = program2.Id, Name = "Push (İtiş)", Order = 1, Description = "Göğüs, Omuz, Arka Kol" };
        await _connection.InsertAsync(p2_push);
        await _connection.InsertAllAsync(new[] {
            new WorkoutItem { WorkoutId = p2_push.Id, ExerciseId = GetExId("Bench Press"), Sets = 4, RepsRange = "6-8", Order = 1 },
            new WorkoutItem { WorkoutId = p2_push.Id, ExerciseId = GetExId("Overhead Press"), Sets = 3, RepsRange = "8-10", Order = 2 },
            new WorkoutItem { WorkoutId = p2_push.Id, ExerciseId = GetExId("Lateral Raise"), Sets = 3, RepsRange = "12-15", Order = 3 },
            new WorkoutItem { WorkoutId = p2_push.Id, ExerciseId = GetExId("Triceps Pushdown"), Sets = 3, RepsRange = "12-15", Order = 4 }
        });

        // Pull Day
        var p2_pull = new Workout { WorkoutProgramId = program2.Id, Name = "Pull (Çekiş)", Order = 2, Description = "Sırt, Ön Kol" };
        await _connection.InsertAsync(p2_pull);
        await _connection.InsertAllAsync(new[] {
            new WorkoutItem { WorkoutId = p2_pull.Id, ExerciseId = GetExId("Deadlift"), Sets = 3, RepsRange = "5-8", Order = 1 },
            new WorkoutItem { WorkoutId = p2_pull.Id, ExerciseId = GetExId("Pull Up"), Sets = 3, RepsRange = "8-10", Order = 2 },
            new WorkoutItem { WorkoutId = p2_pull.Id, ExerciseId = GetExId("Dumbbell Curl"), Sets = 4, RepsRange = "10-12", Order = 3 }
        });

        // Legs Day
        var p2_legs = new Workout { WorkoutProgramId = program2.Id, Name = "Legs (Bacak)", Order = 3, Description = "Ön ve Arka Bacak, Kalça" };
        await _connection.InsertAsync(p2_legs);
        await _connection.InsertAllAsync(new[] {
            new WorkoutItem { WorkoutId = p2_legs.Id, ExerciseId = GetExId("Squat"), Sets = 4, RepsRange = "6-8", Order = 1 },
            new WorkoutItem { WorkoutId = p2_legs.Id, ExerciseId = GetExId("Lunges"), Sets = 3, RepsRange = "10-12", Order = 2 },
            new WorkoutItem { WorkoutId = p2_legs.Id, ExerciseId = GetExId("Plank"), Sets = 3, RepsRange = "60sn", Order = 3 }
        });
    }
}