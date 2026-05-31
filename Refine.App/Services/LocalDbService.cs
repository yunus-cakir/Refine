using SQLite;
using Refine.App.Models;
using SQLiteNetExtensionsAsync.Extensions;

namespace Refine.App.Services;

public class LocalDbService
{
    private SQLiteAsyncConnection? _connection;

    public event Action? OnDatabaseChanged;
    private void NotifyDatabaseChanged() => OnDatabaseChanged?.Invoke();

    // Veritabanı bağlantısını başlat
    private async Task Init()
    {
        if (_connection is not null)
            return;

        _connection = new SQLiteAsyncConnection(DbConstants.DatabasePath, DbConstants.Flags);

        await _connection.CreateTableAsync<MuscleGroup>();
        await _connection.CreateTableAsync<ExerciseMuscleMap>();
        await _connection.CreateTableAsync<Exercise>();
        await _connection.CreateTableAsync<WorkoutProgram>();
        await _connection.CreateTableAsync<Workout>();
        await _connection.CreateTableAsync<WorkoutItem>();
        await _connection.CreateTableAsync<WorkoutLog>();
        await _connection.CreateTableAsync<User>();
        await _connection.CreateTableAsync<BiometricLog>();

        // Varsayılan kullanıcı ve ayarları oluştur (Eğer yoksa)
        var userCount = await _connection.Table<User>().CountAsync();
        if (userCount == 0)
        {
            var newUser = new User
            {
                FirstName = "Sporcu",
                LastName = "",
                Height = 175,
                Weight = 75,
                Gender = "Erkek",
                TargetDailyCalories = 2500,
                MetabolismType = "normal",
                AppSettings = new AppSettings { Language = "tr", UnitSystem = "metric", Theme = "dark" },
                WorkoutSettings = new WorkoutSettings { PreferredReps = 10, PreferredRIR = 2, AutoCopyPreviousSetData = true }
            };
            await _connection.InsertWithChildrenAsync(newUser);
            
            var initialLog = new BiometricLog
            {
                UserId = newUser.Id,
                Date = DateTime.Now,
                Weight = 75,
                Neck = 38,
                Shoulder = 115,
                Waist = 85
            };
            await _connection.InsertAsync(initialLog);
        }

        // Eğer Egzersiz tablosu boşsa, örnek verileri yükle
        if (await _connection.Table<Exercise>().CountAsync() == 0)
        {
            await SeedDataAsync();
        }

        // Rename existing "Mock İdman Programı" to "W's Upper Lower" if it exists
        var existingMock = await _connection.Table<WorkoutProgram>().Where(p => p.Name == "Mock İdman Programı").FirstOrDefaultAsync();
        if (existingMock != null)
        {
            existingMock.Name = "W's Upper Lower";
            await _connection.UpdateAsync(existingMock);
        }

        var mockProgramExists = await _connection.Table<WorkoutProgram>().Where(p => p.Name == "W's Upper Lower").CountAsync();
        if (mockProgramExists == 0)
        {
            await SeedMockProgramAsync();
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
        if (user != null)
        {
            user = await _connection.GetWithChildrenAsync<User>(user.Id);

            // Auto-heal settings if they are null due to uninitialized blob columns on older DBs
            bool needsUpdate = false;
            if (user.AppSettings == null)
            {
                user.AppSettings = new AppSettings { Language = "tr", UnitSystem = "metric", Theme = "dark" };
                needsUpdate = true;
            }
            if (user.WorkoutSettings == null)
            {
                user.WorkoutSettings = new WorkoutSettings { PreferredReps = 10, PreferredRIR = 2, AutoCopyPreviousSetData = true };
                needsUpdate = true;
            }

            if (needsUpdate)
            {
                await _connection.UpdateWithChildrenAsync(user);
            }
        }
        return user;
    }

    public async Task UpdateUserAsync(User user)
    {
        await Init();
        await _connection!.UpdateWithChildrenAsync(user);
        NotifyDatabaseChanged();
    }

    // --------------------------------------------
    //               BIOMETRICS CRUD
    // --------------------------------------------

    public async Task AddBiometricLogAsync(BiometricLog log)
    {
        await Init();
        await _connection!.InsertAsync(log);
        
        var user = await GetUserAsync();
        if (user != null && log.Weight.HasValue)
        {
            user.Weight = log.Weight.Value;
            // Sadece flat property'yi güncelliyoruz, blob ve ilişkileri değil
            await _connection.UpdateAsync(user); 
        }
        NotifyDatabaseChanged();
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
            
        NotifyDatabaseChanged();
    }

    public async Task DeleteExerciseAsync(Exercise exercise)
    {
        await Init();
        await _connection!.DeleteAsync(exercise);
        NotifyDatabaseChanged();
    }

    public async Task<List<Exercise>> GetExercisesAsync()
    {
        await Init();
        var exercises = await _connection!.Table<Exercise>().ToListAsync();
        foreach (var ex in exercises)
        {
            await _connection.GetChildrenAsync(ex, recursive: true);
        }
        return exercises;
    }

    // --------------------------------------------
    //              WORKOUT CRUD
    // --------------------------------------------

    public async Task SaveWorkoutWithItemsAsync(Workout workout)
    {
        await Init();

        if (workout.Id != 0)
            await _connection!.UpdateWithChildrenAsync(workout);
        else
            await _connection!.InsertWithChildrenAsync(workout, recursive: true);
            
        NotifyDatabaseChanged();
    }

    public async Task DeleteWorkoutAsync(int workoutId)
    {
        await Init();
        var workout = await GetWorkoutByIdAsync(workoutId);
        if (workout != null)
            await _connection!.DeleteAsync(workout, recursive: true);
            
        NotifyDatabaseChanged();
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
            await _connection.GetChildrenAsync(w, recursive: true);
        }

        return workouts;
    }

    // --------------------------------------------
    //                  WORKOUTLOG
    // --------------------------------------------

    public async Task SaveWorkoutLogsAsync(List<WorkoutLog> logs)
    {
        await Init();
        var logsToSave = logs.Where(l => l.Weight.HasValue && l.Reps.HasValue).ToList();
        if (logsToSave.Any())
        {
            await _connection!.InsertAllAsync(logsToSave);
        }
        NotifyDatabaseChanged();
    }

    public async Task<List<WorkoutLog>> GetLogsForExerciseAsync(int exerciseId)
    {
        await Init();
        return await _connection!.Table<WorkoutLog>()
                                .Where(l => l.ExerciseId == exerciseId && l.IsSaved)
                                .OrderByDescending(l => l.Date)
                                .ToListAsync();
    }

    public async Task<List<WorkoutLog>> GetLastLogForExerciseAsync(int exerciseId)
    {
        await Init();

        // 1. Bu harekete ait EN SON girilen kaydı (muhtemelen son set) bul
        var lastLog = await _connection!.Table<WorkoutLog>()
                                        .Where(l => l.ExerciseId == exerciseId && l.IsSaved)
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
                                .Where(l => l.IsSaved)
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
    public async Task<bool> HasLogForTodayAsync(int workoutId, int? cycle = null)
    {
        await Init();
        var today = DateTime.Now.Date;
        var maxDate = today.AddDays(1).AddTicks(-1);

        var query = _connection!.Table<WorkoutLog>()
                                .Where(l => l.WorkoutId == workoutId && l.Date >= today && l.Date <= maxDate);
        
        if (cycle.HasValue)
        {
            query = query.Where(l => l.Cycle == cycle.Value);
        }

        var count = await query.CountAsync();
        return count > 0;
    }

    public async Task<bool> HasLogWithinDaysAsync(int workoutId, int days)
    {
        await Init();
        var limitDate = DateTime.Now.Date.AddDays(-days);
        var count = await _connection!.Table<WorkoutLog>()
                                     .Where(l => l.WorkoutId == workoutId && l.Date >= limitDate && l.IsCompleted)
                                     .CountAsync();
        return count > 0;
    }

    public async Task<bool> HasLogForWeekAsync(int workoutId, bool previousWeek = false)
    {
        await Init();
        var today = DateTime.Now.Date;
        int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
        var startOfWeek = today.AddDays(-diff);
        
        if (previousWeek)
        {
            startOfWeek = startOfWeek.AddDays(-7);
        }
        var endOfWeek = startOfWeek.AddDays(7);

        var count = await _connection!.Table<WorkoutLog>()
                                     .Where(l => l.WorkoutId == workoutId && l.Date >= startOfWeek && l.Date < endOfWeek && l.IsCompleted)
                                     .CountAsync();
        return count > 0;
    }

    public async Task<bool> HasLogForCycleAsync(int workoutId, int cycle)
    {
        await Init();
        var count = await _connection!.Table<WorkoutLog>()
                                     .Where(l => l.WorkoutId == workoutId && l.Cycle == cycle && l.IsCompleted)
                                     .CountAsync();
        return count > 0;
    }

    public async Task<bool> HasSavedLogForCycleAsync(int workoutId, int cycle)
    {
        await Init();
        var count = await _connection!.Table<WorkoutLog>()
                                     .Where(l => l.WorkoutId == workoutId && l.Cycle == cycle && l.IsSaved && !l.IsCompleted)
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
                bool isEmpty = !log.Weight.HasValue || !log.Reps.HasValue;
                if (log.Id != 0)
                {
                    if (isEmpty)
                    {
                        tran.Delete(log);
                    }
                    else
                    {
                        tran.Update(log);
                    }
                }
                else
                {
                    if (!isEmpty)
                    {
                        tran.Insert(log);
                    }
                }
            }
        });
        NotifyDatabaseChanged();
    }

    // --------------------------------------------
    //              WORKOUTPROGRAM CRUD
    // --------------------------------------------

    public async Task<WorkoutProgram?> GetSelectedWorkoutProgramAsync()
    {
        var user = await GetUserAsync();
        if (user.SelectedWorkoutProgramId.HasValue && user.SelectedWorkoutProgramId.Value > 0)
        {
            var program = await GetProgramByIdAsync(user.SelectedWorkoutProgramId.Value);
            if (program != null && user.WorkoutSettings?.CycleLength == "Weekly")
            {
                var now = DateTime.Now;
                var lastUpdate = program.LastCycleUpdateDate;
                var weekStart = user.AppSettings?.WeekStartDay ?? DayOfWeek.Monday;

                int diff = (7 + (now.DayOfWeek - weekStart)) % 7;
                DateTime currentWeekStart = now.Date.AddDays(-diff);

                int lastDiff = (7 + (lastUpdate.DayOfWeek - weekStart)) % 7;
                DateTime lastWeekStart = lastUpdate.Date.AddDays(-lastDiff);

                int weeksPassed = (int)Math.Round((currentWeekStart - lastWeekStart).TotalDays / 7.0);
                if (weeksPassed > 0)
                {
                    program.Cycle += weeksPassed;
                    program.LastCycleUpdateDate = now;
                    await Init();
                    await _connection!.UpdateAsync(program);
                }
            }
            return program;
        }
        return null;
    }

    public async Task SetSelectedWorkoutProgramAsync(int programId)
    {
        var user = await GetUserAsync();
        user.SelectedWorkoutProgramId = programId;
        if (programId > 0)
        {
            user.SelectedWorkoutProgram = await GetProgramByIdAsync(programId);
        }
        else
        {
            user.SelectedWorkoutProgram = null;
        }
        await UpdateUserAsync(user);
        // Note: UpdateUserAsync already calls NotifyDatabaseChanged, but calling it again won't hurt, 
        // however we will just rely on UpdateUserAsync's notification.
    }

    public async Task<int> SaveWorkoutProgramAsync(WorkoutProgram program)
    {
        await Init();
        if (program.Id != 0)
        {
            await _connection!.UpdateWithChildrenAsync(program);
            NotifyDatabaseChanged();
            return program.Id;
        }
        else
        {
            await _connection!.InsertWithChildrenAsync(program, recursive: true);
            NotifyDatabaseChanged();
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
            Order = existingCount + 1,
            Items = new List<WorkoutItem>()
        };

        if (sourceWorkout.Items != null)
        {
            foreach (var item in sourceWorkout.Items)
            {
                newWorkout.Items.Add(new WorkoutItem
                {
                    ExerciseId = item.ExerciseId,
                    Sets = item.Sets,
                    RepsRange = item.RepsRange,
                    Order = item.Order
                });
            }
        }
        await _connection!.InsertWithChildrenAsync(newWorkout, recursive: true);
        NotifyDatabaseChanged();
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
        NotifyDatabaseChanged();
    }

    public async Task<List<WorkoutProgram>> GetAllProgramsAsync()
    {
        await Init();
        return await _connection!.Table<WorkoutProgram>().ToListAsync();
    }

    public async Task<WorkoutProgram?> GetProgramByIdAsync(int id)
    {
        await Init();
        try
        {
            return await _connection!.GetWithChildrenAsync<WorkoutProgram>(id, recursive: true);
        }
        catch
        {
            return null;
        }
    }

    public async Task<Workout?> GetWorkoutByIdAsync(int workoutId)
    {
        await Init();
        try
        {
            return await _connection!.GetWithChildrenAsync<Workout>(workoutId, recursive: true);
        }
        catch
        {
            return null;
        }
    }

    public async Task DeleteProgramAsync(int programId)
    {
        await Init();
        var program = await GetProgramByIdAsync(programId);
        if (program != null)
        {
            await _connection!.DeleteAsync(program, recursive: true);
        }
        NotifyDatabaseChanged();
    }

    private async Task SeedDataAsync()
    {
        // --- 1. KAS GRUPLARINI OLUŞTUR ---
        var muscles = new List<MuscleGroup>
        {
            new MuscleGroup { Category = "Chest", Name = "Pectoralis Major" },
            new MuscleGroup { Category = "Chest", Name = "Upper Chest" },
            new MuscleGroup { Category = "Back", Name = "Lats" },
            new MuscleGroup { Category = "Back", Name = "Rhomboids" },
            new MuscleGroup { Category = "Shoulders", Name = "Front Delt" },
            new MuscleGroup { Category = "Shoulders", Name = "Side Delt" },
            new MuscleGroup { Category = "Shoulders", Name = "Rear Delt" },
            new MuscleGroup { Category = "Arms", Name = "Biceps" },
            new MuscleGroup { Category = "Arms", Name = "Triceps" },
            new MuscleGroup { Category = "Legs", Name = "Quads" },
            new MuscleGroup { Category = "Legs", Name = "Hamstrings" },
            new MuscleGroup { Category = "Legs", Name = "Glutes" },
            new MuscleGroup { Category = "Core", Name = "Abs" }
        };
        await _connection!.InsertAllAsync(muscles);
        var dbMuscles = await _connection.Table<MuscleGroup>().ToListAsync();
        int GetMusId(string name) => dbMuscles.FirstOrDefault(m => m.Name == name)?.Id ?? 0;

        // --- 2. EGZERSİZLERİ OLUŞTUR ---
        var exercises = new List<Exercise>
        {
            new Exercise { Name = "Bench Press", Difficulty = "Intermediate", Equipment = "Barbell", ImageUrl = "bench_press.png", CnsFatigueScore = 6.5m },
            new Exercise { Name = "Squat", Difficulty = "Intermediate", Equipment = "Barbell", ImageUrl = "squat.png", CnsFatigueScore = 8.5m },
            new Exercise { Name = "Deadlift", Difficulty = "Advanced", Equipment = "Barbell", ImageUrl = "deadlift.png", CnsFatigueScore = 9.5m },
            new Exercise { Name = "Overhead Press", Difficulty = "Intermediate", Equipment = "Barbell", ImageUrl = "ohp.png", CnsFatigueScore = 7.0m },
            new Exercise { Name = "Pull Up", Difficulty = "Intermediate", Equipment = "Bodyweight", ImageUrl = "pullup.png", CnsFatigueScore = 6.0m },
            new Exercise { Name = "Barbell Row", Difficulty = "Intermediate", Equipment = "Barbell", ImageUrl = "barbell_row.png", CnsFatigueScore = 7.5m },
            new Exercise { Name = "Dumbbell Curl", Difficulty = "Beginner", Equipment = "Dumbbell", ImageUrl = "curl.png", CnsFatigueScore = 3.0m },
            new Exercise { Name = "Triceps Pushdown", Difficulty = "Beginner", Equipment = "Machine", ImageUrl = "pushdown.png", CnsFatigueScore = 3.0m },
            new Exercise { Name = "Lunges", Difficulty = "Beginner", Equipment = "Dumbbell", ImageUrl = "lunges.png", CnsFatigueScore = 6.0m },
            new Exercise { Name = "Plank", Difficulty = "Beginner", Equipment = "Bodyweight", ImageUrl = "plank.png", CnsFatigueScore = 4.0m },
            new Exercise { Name = "Lateral Raise", Difficulty = "Beginner", Equipment = "Dumbbell", ImageUrl = "lateral_raise.png", CnsFatigueScore = 3.5m }
        };

        // Toplu ekle
        await _connection!.InsertAllAsync(exercises);

        // ID eşleşmesi için veritabanından geri çek
        var dbExercises = await _connection.Table<Exercise>().ToListAsync();

        // Helper fonksiyon
        int GetExId(string name) => dbExercises.FirstOrDefault(e => e.Name == name)?.Id ?? 0;

        // --- 3. MAPPING (ÇOKA ÇOK) OLUŞTUR ---
        var mappings = new List<ExerciseMuscleMap>
        {
            new ExerciseMuscleMap { ExerciseId = GetExId("Bench Press"), MuscleGroupId = GetMusId("Pectoralis Major"), ImpactMultiplier = 1.0 },
            new ExerciseMuscleMap { ExerciseId = GetExId("Bench Press"), MuscleGroupId = GetMusId("Front Delt"), ImpactMultiplier = 0.5 },
            new ExerciseMuscleMap { ExerciseId = GetExId("Bench Press"), MuscleGroupId = GetMusId("Triceps"), ImpactMultiplier = 0.5 },

            new ExerciseMuscleMap { ExerciseId = GetExId("Squat"), MuscleGroupId = GetMusId("Quads"), ImpactMultiplier = 1.0 },
            new ExerciseMuscleMap { ExerciseId = GetExId("Squat"), MuscleGroupId = GetMusId("Glutes"), ImpactMultiplier = 0.7 },
            new ExerciseMuscleMap { ExerciseId = GetExId("Squat"), MuscleGroupId = GetMusId("Hamstrings"), ImpactMultiplier = 0.4 },

            new ExerciseMuscleMap { ExerciseId = GetExId("Deadlift"), MuscleGroupId = GetMusId("Hamstrings"), ImpactMultiplier = 1.0 },
            new ExerciseMuscleMap { ExerciseId = GetExId("Deadlift"), MuscleGroupId = GetMusId("Glutes"), ImpactMultiplier = 0.8 },
            new ExerciseMuscleMap { ExerciseId = GetExId("Deadlift"), MuscleGroupId = GetMusId("Lats"), ImpactMultiplier = 0.6 },
            
            new ExerciseMuscleMap { ExerciseId = GetExId("Overhead Press"), MuscleGroupId = GetMusId("Front Delt"), ImpactMultiplier = 1.0 },
            new ExerciseMuscleMap { ExerciseId = GetExId("Overhead Press"), MuscleGroupId = GetMusId("Triceps"), ImpactMultiplier = 0.6 },

            new ExerciseMuscleMap { ExerciseId = GetExId("Pull Up"), MuscleGroupId = GetMusId("Lats"), ImpactMultiplier = 1.0 },
            new ExerciseMuscleMap { ExerciseId = GetExId("Pull Up"), MuscleGroupId = GetMusId("Biceps"), ImpactMultiplier = 0.5 },

            new ExerciseMuscleMap { ExerciseId = GetExId("Barbell Row"), MuscleGroupId = GetMusId("Rhomboids"), ImpactMultiplier = 1.0 },
            new ExerciseMuscleMap { ExerciseId = GetExId("Barbell Row"), MuscleGroupId = GetMusId("Lats"), ImpactMultiplier = 0.8 },
            new ExerciseMuscleMap { ExerciseId = GetExId("Barbell Row"), MuscleGroupId = GetMusId("Biceps"), ImpactMultiplier = 0.5 },

            new ExerciseMuscleMap { ExerciseId = GetExId("Dumbbell Curl"), MuscleGroupId = GetMusId("Biceps"), ImpactMultiplier = 1.0 },
            
            new ExerciseMuscleMap { ExerciseId = GetExId("Triceps Pushdown"), MuscleGroupId = GetMusId("Triceps"), ImpactMultiplier = 1.0 },

            new ExerciseMuscleMap { ExerciseId = GetExId("Lunges"), MuscleGroupId = GetMusId("Quads"), ImpactMultiplier = 1.0 },
            new ExerciseMuscleMap { ExerciseId = GetExId("Lunges"), MuscleGroupId = GetMusId("Glutes"), ImpactMultiplier = 0.6 },
            
            new ExerciseMuscleMap { ExerciseId = GetExId("Plank"), MuscleGroupId = GetMusId("Abs"), ImpactMultiplier = 1.0 },

            new ExerciseMuscleMap { ExerciseId = GetExId("Lateral Raise"), MuscleGroupId = GetMusId("Side Delt"), ImpactMultiplier = 1.0 }
        };
        await _connection.InsertAllAsync(mappings);


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

    private async Task SeedMockProgramAsync()
    {
        var dbMuscles = await _connection!.Table<MuscleGroup>().ToListAsync();
        int GetMusId(string name) => dbMuscles.FirstOrDefault(m => m.Name == name)?.Id ?? 0;

        // Ensure "Calves" muscle group exists
        if (GetMusId("Calves") == 0)
        {
            var calves = new MuscleGroup { Category = "Legs", Name = "Calves" };
            await _connection.InsertAsync(calves);
            dbMuscles.Add(calves);
        }

        var exercisesToSeed = new List<Exercise>
        {
            new Exercise { Name = "Cable Lat Pull Over", Difficulty = "Intermediate", Equipment = "Cable", CnsFatigueScore = 5.0m },
            new Exercise { Name = "Smith Incline Bench Press", Difficulty = "Intermediate", Equipment = "Machine", CnsFatigueScore = 6.0m },
            new Exercise { Name = "Fly Machine", Difficulty = "Beginner", Equipment = "Machine", CnsFatigueScore = 4.0m },
            new Exercise { Name = "Shoulder Machine", Difficulty = "Beginner", Equipment = "Machine", CnsFatigueScore = 4.5m },
            new Exercise { Name = "Triceps Kickback", Difficulty = "Beginner", Equipment = "Dumbbell", CnsFatigueScore = 3.0m },
            new Exercise { Name = "Ab Crunch", Difficulty = "Beginner", Equipment = "Bodyweight", CnsFatigueScore = 3.5m },
            new Exercise { Name = "Leg Raise", Difficulty = "Beginner", Equipment = "Bodyweight", CnsFatigueScore = 4.0m },
            new Exercise { Name = "Leg Press", Difficulty = "Intermediate", Equipment = "Machine", CnsFatigueScore = 7.0m },
            new Exercise { Name = "Leg Extension", Difficulty = "Beginner", Equipment = "Machine", CnsFatigueScore = 5.0m },
            new Exercise { Name = "Leg Curl", Difficulty = "Beginner", Equipment = "Machine", CnsFatigueScore = 5.0m },
            new Exercise { Name = "Rear Delt Machine Fly", Difficulty = "Beginner", Equipment = "Machine", CnsFatigueScore = 4.0m },
            new Exercise { Name = "Hammer Curl", Difficulty = "Beginner", Equipment = "Dumbbell", CnsFatigueScore = 3.0m },
            new Exercise { Name = "Barbell Curl", Difficulty = "Intermediate", Equipment = "Barbell", CnsFatigueScore = 4.0m },
            new Exercise { Name = "Calf Raise", Difficulty = "Beginner", Equipment = "Machine", CnsFatigueScore = 3.5m },
            new Exercise { Name = "Vertical Row", Difficulty = "Intermediate", Equipment = "Machine", CnsFatigueScore = 6.0m },
            new Exercise { Name = "One Arm Cable Row", Difficulty = "Intermediate", Equipment = "Cable", CnsFatigueScore = 5.0m },
            new Exercise { Name = "Cable Lateral Raise", Difficulty = "Beginner", Equipment = "Cable", CnsFatigueScore = 3.5m },
            new Exercise { Name = "Pushdown", Difficulty = "Beginner", Equipment = "Cable", CnsFatigueScore = 3.0m }
        };

        var dbExercises = await _connection.Table<Exercise>().ToListAsync();
        
        foreach(var ex in exercisesToSeed)
        {
            if (!dbExercises.Any(e => e.Name == ex.Name))
            {
                await _connection.InsertAsync(ex);
                dbExercises.Add(ex);
            }
        }

        int GetExId(string name) => dbExercises.FirstOrDefault(e => e.Name == name)?.Id ?? 0;

        var program = new WorkoutProgram
        {
            Name = "W's Upper Lower",
            Level = "Advanced",
            Goal = "Hipertrofi",
            TargetMuscles = "Tüm Vücut",
            Environment = "Spor Salonu",
            Cycle = 14,
            LastCycleUpdateDate = DateTime.Now
        };
        await _connection.InsertAsync(program);

        // Idman 1
        var w1 = new Workout { WorkoutProgramId = program.Id, Name = "İdman 1", Order = 1 };
        await _connection.InsertAsync(w1);
        await _connection.InsertAllAsync(new[] {
            new WorkoutItem { WorkoutId = w1.Id, ExerciseId = GetExId("Pull Up"), Sets = 2, RepsRange = "Tükeniş", Order = 1 },
            new WorkoutItem { WorkoutId = w1.Id, ExerciseId = GetExId("Cable Lat Pull Over"), Sets = 2, RepsRange = "Tükeniş", Order = 2 },
            new WorkoutItem { WorkoutId = w1.Id, ExerciseId = GetExId("Smith Incline Bench Press"), Sets = 2, RepsRange = "Tükeniş", Order = 3 },
            new WorkoutItem { WorkoutId = w1.Id, ExerciseId = GetExId("Fly Machine"), Sets = 2, RepsRange = "Tükeniş", Order = 4 },
            new WorkoutItem { WorkoutId = w1.Id, ExerciseId = GetExId("Shoulder Machine"), Sets = 2, RepsRange = "Tükeniş", Order = 5 },
            new WorkoutItem { WorkoutId = w1.Id, ExerciseId = GetExId("Lateral Raise"), Sets = 2, RepsRange = "Tükeniş", Order = 6 },
            new WorkoutItem { WorkoutId = w1.Id, ExerciseId = GetExId("Triceps Pushdown"), Sets = 2, RepsRange = "Tükeniş", Order = 7 },
            new WorkoutItem { WorkoutId = w1.Id, ExerciseId = GetExId("Triceps Kickback"), Sets = 2, RepsRange = "Tükeniş", Order = 8 },
            new WorkoutItem { WorkoutId = w1.Id, ExerciseId = GetExId("Ab Crunch"), Sets = 2, RepsRange = "Tükeniş", Order = 9 },
            new WorkoutItem { WorkoutId = w1.Id, ExerciseId = GetExId("Leg Raise"), Sets = 2, RepsRange = "Tükeniş", Order = 10 }
        });

        // Idman 2
        var w2 = new Workout { WorkoutProgramId = program.Id, Name = "İdman 2", Order = 2 };
        await _connection.InsertAsync(w2);
        await _connection.InsertAllAsync(new[] {
            new WorkoutItem { WorkoutId = w2.Id, ExerciseId = GetExId("Leg Press"), Sets = 2, RepsRange = "Tükeniş", Order = 1 },
            new WorkoutItem { WorkoutId = w2.Id, ExerciseId = GetExId("Leg Extension"), Sets = 2, RepsRange = "Tükeniş", Order = 2 },
            new WorkoutItem { WorkoutId = w2.Id, ExerciseId = GetExId("Leg Curl"), Sets = 2, RepsRange = "Tükeniş", Order = 3 },
            new WorkoutItem { WorkoutId = w2.Id, ExerciseId = GetExId("Lateral Raise"), Sets = 2, RepsRange = "Tükeniş", Order = 4 },
            new WorkoutItem { WorkoutId = w2.Id, ExerciseId = GetExId("Rear Delt Machine Fly"), Sets = 2, RepsRange = "Tükeniş", Order = 5 },
            new WorkoutItem { WorkoutId = w2.Id, ExerciseId = GetExId("Hammer Curl"), Sets = 2, RepsRange = "Tükeniş", Order = 6 },
            new WorkoutItem { WorkoutId = w2.Id, ExerciseId = GetExId("Barbell Curl"), Sets = 2, RepsRange = "Tükeniş", Order = 7 },
            new WorkoutItem { WorkoutId = w2.Id, ExerciseId = GetExId("Calf Raise"), Sets = 2, RepsRange = "Tükeniş", Order = 8 },
            new WorkoutItem { WorkoutId = w2.Id, ExerciseId = GetExId("Ab Crunch"), Sets = 2, RepsRange = "Tükeniş", Order = 9 },
            new WorkoutItem { WorkoutId = w2.Id, ExerciseId = GetExId("Leg Raise"), Sets = 2, RepsRange = "Tükeniş", Order = 10 }
        });

        // Idman 3
        var w3 = new Workout { WorkoutProgramId = program.Id, Name = "İdman 3", Order = 3 };
        await _connection.InsertAsync(w3);
        await _connection.InsertAllAsync(new[] {
            new WorkoutItem { WorkoutId = w3.Id, ExerciseId = GetExId("Smith Incline Bench Press"), Sets = 2, RepsRange = "Tükeniş", Order = 1 },
            new WorkoutItem { WorkoutId = w3.Id, ExerciseId = GetExId("Fly Machine"), Sets = 2, RepsRange = "Tükeniş", Order = 2 },
            new WorkoutItem { WorkoutId = w3.Id, ExerciseId = GetExId("Pull Up"), Sets = 2, RepsRange = "Tükeniş", Order = 3 },
            new WorkoutItem { WorkoutId = w3.Id, ExerciseId = GetExId("Vertical Row"), Sets = 2, RepsRange = "Tükeniş", Order = 4 },
            new WorkoutItem { WorkoutId = w3.Id, ExerciseId = GetExId("One Arm Cable Row"), Sets = 2, RepsRange = "Tükeniş", Order = 5 },
            new WorkoutItem { WorkoutId = w3.Id, ExerciseId = GetExId("Lateral Raise"), Sets = 2, RepsRange = "Tükeniş", Order = 6 },
            new WorkoutItem { WorkoutId = w3.Id, ExerciseId = GetExId("Cable Lateral Raise"), Sets = 2, RepsRange = "Tükeniş", Order = 7 },
            new WorkoutItem { WorkoutId = w3.Id, ExerciseId = GetExId("Pushdown"), Sets = 2, RepsRange = "Tükeniş", Order = 8 },
            new WorkoutItem { WorkoutId = w3.Id, ExerciseId = GetExId("Ab Crunch"), Sets = 2, RepsRange = "Tükeniş", Order = 9 },
            new WorkoutItem { WorkoutId = w3.Id, ExerciseId = GetExId("Leg Raise"), Sets = 2, RepsRange = "Tükeniş", Order = 10 }
        });

        // Generate 3 months of mock logs
        var logs = new List<WorkoutLog>();
        var r = new Random();
        var startDate = DateTime.Now.Date.AddDays(-90);
        int dayIndex = 0;
        
        var workouts = new[] { w1, w2, w3 };
        var workoutItems = new List<List<WorkoutItem>>();
        workoutItems.Add(await _connection.Table<WorkoutItem>().Where(i => i.WorkoutId == w1.Id).ToListAsync());
        workoutItems.Add(await _connection.Table<WorkoutItem>().Where(i => i.WorkoutId == w2.Id).ToListAsync());
        workoutItems.Add(await _connection.Table<WorkoutItem>().Where(i => i.WorkoutId == w3.Id).ToListAsync());

        double GetProgressiveWeight(int dayIdx, double startWeight)
        {
            double weeksPassed = dayIdx / 7.0;
            return Math.Round(startWeight + (weeksPassed * 1.5) + (r.NextDouble() * 2 - 1), 1);
        }
        
        int GetProgressiveReps(int dayIdx, int baseReps)
        {
             return baseReps + (r.Next(0, 3));
        }

        while (dayIndex <= 90)
        {
            int weekDay = dayIndex % 7;
            int? workoutIdx = null;
            if (weekDay == 0) workoutIdx = 0;
            else if (weekDay == 2) workoutIdx = 1;
            else if (weekDay == 4) workoutIdx = 2;

            if (workoutIdx.HasValue)
            {
                var wDate = startDate.AddDays(dayIndex).AddHours(18); // 18:00
                var currentWorkout = workouts[workoutIdx.Value];
                var currentItems = workoutItems[workoutIdx.Value];

                foreach(var item in currentItems)
                {
                    var exName = dbExercises.FirstOrDefault(e => e.Id == item.ExerciseId)?.Name ?? "";
                    
                    double baseWeight = 50;
                    if (exName.Contains("Press")) baseWeight = 60;
                    if (exName.Contains("Fly")) baseWeight = 40;
                    if (exName.Contains("Curl") || exName.Contains("Raise") || exName.Contains("Pushdown") || exName.Contains("Kickback")) baseWeight = 15;
                    if (exName.Contains("Leg Press")) baseWeight = 120;
                    if (exName.Contains("Leg Extension") || exName.Contains("Leg Curl")) baseWeight = 45;
                    if (exName.Contains("Pull Up") || exName.Contains("Ab Crunch") || exName.Contains("Leg Raise") || exName.Contains("Plank")) baseWeight = 0;

                    for(int s = 1; s <= item.Sets; s++)
                    {
                        logs.Add(new WorkoutLog
                        {
                            WorkoutId = currentWorkout.Id,
                            ExerciseId = item.ExerciseId,
                            ExerciseNameSnapshot = exName,
                            Date = wDate.AddMinutes(item.Order * 5 + s * 2), // staggered times
                            SetNumber = s,
                            Weight = baseWeight > 0 ? GetProgressiveWeight(dayIndex, baseWeight) : 0,
                            Reps = GetProgressiveReps(dayIndex, r.Next(8, 12)),
                            RIR = 0, // Tükeniş
                            FormRating = r.Next(3, 6),
                            Note = "",
                            IsCompleted = true,
                            IsSaved = true,
                            Cycle = (dayIndex / 7) + 1
                        });
                    }
                }
            }
            dayIndex++;
        }

        await _connection.InsertAllAsync(logs);
    }
}