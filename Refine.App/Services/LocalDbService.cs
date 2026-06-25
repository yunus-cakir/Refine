using SQLite;
using Refine.App.Models;
using SQLiteNetExtensionsAsync.Extensions;

namespace Refine.App.Services;

public class LocalDbService
{
    private SQLiteAsyncConnection? _connection;
    private readonly SemaphoreSlim _initLock = new SemaphoreSlim(1, 1);
    private bool _isInitialized = false;

    public event Action? OnDatabaseChanged;
    private void NotifyDatabaseChanged() => OnDatabaseChanged?.Invoke();

    // Veritabanı bağlantısını başlat
    private async Task Init()
    {
        if (_isInitialized && _connection is not null)
            return;

        await _initLock.WaitAsync();
        try
        {
            if (_isInitialized && _connection is not null)
                return;

            if (_connection == null)
            {
                _connection = new SQLiteAsyncConnection(DbConstants.DatabasePath, DbConstants.Flags);
            }

            try
            {
                await _connection.CreateTableAsync<MuscleGroup>();
                await _connection.CreateTableAsync<ExerciseMuscleMap>();
                await _connection.CreateTableAsync<Exercise>();
                await _connection.CreateTableAsync<WorkoutProgram>();
                await _connection.CreateTableAsync<Workout>();
                await _connection.CreateTableAsync<WorkoutItem>();
                await _connection.CreateTableAsync<WorkoutLog>();
                await _connection.CreateTableAsync<User>();
                await _connection.CreateTableAsync<BiometricLog>();

                try
                {
                    await _connection.ExecuteAsync(
                        "UPDATE WorkoutProgram SET Week = Cycle, LastWeekUpdateDate = LastCycleUpdateDate");
                }
                catch
                {
                }

                try
                {
                    await _connection.ExecuteAsync("UPDATE WorkoutLog SET Week = Cycle");
                }
                catch
                {
                }

                // Varsayılan kullanıcı ve ayarları oluştur (Eğer yoksa)
                var userCount = await _connection.Table<User>().CountAsync();
                if (userCount == 0)
                {
                    var newUser = new User
                    {
                        FirstName = "John",
                        LastName = "",
                        Height = 175,
                        Weight = 75,
                        Gender = Gender.Male,
                        TargetDailyCalories = 2500,
                        MetabolismType = "normal",
                        AppSettings = new AppSettings { Language = "en", UnitSystem = "metric", Theme = "dark" },
                        WorkoutSettings = new WorkoutSettings
                        {
                            PreferredMinReps = 8, PreferredMaxReps = 12, PreferredRepRange = "8-12",
                            PreferredRIR = 2.0m, AutoCopyWeight = true, AutoCopyReps = true, AutoCopyRIR = true,
                            AutoCopyFormRating = true
                        }
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
                var existingMock = await _connection.Table<WorkoutProgram>().Where(p => p.Name == "Mock İdman Programı")
                    .FirstOrDefaultAsync();
                if (existingMock != null)
                {
                    existingMock.Name = "W's Upper Lower";
                    await _connection.UpdateAsync(existingMock);
                }

                var mockProgramExists =
                    await _connection.Table<WorkoutProgram>().Where(p => p.Name == "W's Upper Lower").CountAsync();
                if (mockProgramExists == 0)
                {
                    await SeedMockProgramAsync();
                }

                var agirsaglamExists =
                    await _connection.Table<WorkoutProgram>().Where(p => p.Name == "Ağırsağlam's 5x5").CountAsync();
                if (agirsaglamExists == 0)
                {
                    await SeedAgirsaglam5x5ProgramAsync();
                }

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LocalDbService] Initialization Error: {ex.Message}");
                // Not throwing here allows the app to stay alive, 
                // but subsequent operations might fail or retry.
            }
        }
        finally
        {
            _initLock.Release();
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
                user.AppSettings = new AppSettings { Language = "en", UnitSystem = "metric", Theme = "dark" };
                needsUpdate = true;
            }

            if (user.WorkoutSettings == null)
            {
                user.WorkoutSettings = new WorkoutSettings
                {
                    PreferredMinReps = 8, PreferredMaxReps = 12, PreferredRepRange = "8-12", PreferredRIR = 2.0m,
                    AutoCopyWeight = true, AutoCopyReps = true, AutoCopyRIR = true, AutoCopyFormRating = true
                };
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

    private async Task PopulateExerciseMuscleMapsAsync(IEnumerable<Exercise> exercises)
    {
        var exerciseList = exercises.ToList();
        if (!exerciseList.Any()) return;

        var exerciseIds = exerciseList.Select(e => e.Id).ToList();
        var mappings = await _connection!.Table<ExerciseMuscleMap>().Where(m => exerciseIds.Contains(m.ExerciseId))
            .ToListAsync();

        var muscleGroupIds = mappings.Select(m => m.MuscleGroupId).Distinct().ToList();
        var muscleGroups = new List<MuscleGroup>();
        if (muscleGroupIds.Any())
        {
            muscleGroups = await _connection.Table<MuscleGroup>().Where(mg => muscleGroupIds.Contains(mg.Id))
                .ToListAsync();
        }

        var mgDict = muscleGroups.ToDictionary(mg => mg.Id);
        var mapDict = mappings.GroupBy(m => m.ExerciseId).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var ex in exerciseList)
        {
            if (mapDict.TryGetValue(ex.Id, out var exMaps))
            {
                foreach (var map in exMaps)
                {
                    if (mgDict.TryGetValue(map.MuscleGroupId, out var mg))
                    {
                        map.MuscleGroup = mg;
                    }
                }

                ex.MuscleMaps = exMaps;
            }
            else
            {
                ex.MuscleMaps = new List<ExerciseMuscleMap>();
            }
        }
    }

    public async Task<List<Exercise>> GetExercisesAsync()
    {
        await Init();
        var exercises = await _connection!.Table<Exercise>().ToListAsync();
        await PopulateExerciseMuscleMapsAsync(exercises);

        return exercises;
    }

    // --------------------------------------------
    //              WORKOUT CRUD
    // --------------------------------------------

    public async Task SaveWorkoutWithItemsAsync(Workout workout)
    {
        await Init();

        if (workout.WorkoutProgramId != 0)
        {
            var otherWorkouts = await _connection!.Table<Workout>()
                .Where(w => w.WorkoutProgramId == workout.WorkoutProgramId && w.Id != workout.Id)
                .ToListAsync();

            if (workout.Id == 0 && workout.Order == 0)
            {
                var maxOrder = otherWorkouts.Any() ? otherWorkouts.Max(w => w.Order) : 0;
                workout.Order = maxOrder + 1;
            }

            bool hasConflict = otherWorkouts.Any(w => w.Order == workout.Order);
            if (hasConflict)
            {
                var workoutsToUpdate = otherWorkouts.Where(w => w.Order >= workout.Order).ToList();
                foreach (var w in workoutsToUpdate)
                {
                    w.Order += 1;
                }

                if (workoutsToUpdate.Any())
                {
                    await _connection!.RunInTransactionAsync(tran =>
                    {
                        foreach (var w in workoutsToUpdate)
                        {
                            tran.Update(w);
                        }
                    });
                }
            }
        }

        if (workout.Id != 0)
        {
            await _connection!.UpdateAsync(workout);

            if (workout.Items != null)
            {
                var newItems = workout.Items.Where(i => i.Id == 0).ToList();
                if (newItems.Any())
                {
                    foreach (var item in newItems)
                    {
                        item.WorkoutId = workout.Id;
                    }

                    await _connection.InsertAllAsync(newItems);
                }

                var existingItems = workout.Items.Where(i => i.Id != 0).ToList();
                if (existingItems.Any())
                {
                    await _connection.UpdateAllAsync(existingItems);
                }

                var dbItems = await _connection.Table<WorkoutItem>().Where(i => i.WorkoutId == workout.Id)
                    .ToListAsync();
                var currentItemIds = existingItems.Select(i => i.Id).ToList();
                var itemsToDelete = dbItems.Where(i => !currentItemIds.Contains(i.Id)).ToList();
                if (itemsToDelete.Any())
                {
                    foreach (var delItem in itemsToDelete)
                    {
                        await _connection.DeleteAsync(delItem);
                    }
                }
            }
        }
        else
        {
            await _connection!.InsertAsync(workout);
            if (workout.Items != null && workout.Items.Any())
            {
                foreach (var item in workout.Items)
                {
                    item.WorkoutId = workout.Id;
                }

                await _connection.InsertAllAsync(workout.Items);
            }
        }

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

        var workoutIds = workouts.Select(w => w.Id).ToList();

        var items = new List<WorkoutItem>();
        if (workoutIds.Any())
        {
            items = await _connection.Table<WorkoutItem>().Where(i => workoutIds.Contains(i.WorkoutId)).ToListAsync();
        }

        var exerciseIds = items.Select(i => i.ExerciseId).Distinct().ToList();
        var exercises = new List<Exercise>();
        if (exerciseIds.Any())
        {
            exercises = await _connection.Table<Exercise>().Where(e => exerciseIds.Contains(e.Id)).ToListAsync();
            await PopulateExerciseMuscleMapsAsync(exercises);
        }

        var exDict = exercises.ToDictionary(e => e.Id);

        var itemsByWorkout = items.GroupBy(i => i.WorkoutId).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var w in workouts)
        {
            if (itemsByWorkout.TryGetValue(w.Id, out var wItems))
            {
                foreach (var item in wItems)
                {
                    if (exDict.TryGetValue(item.ExerciseId, out var ex))
                    {
                        item.Exercise = ex;
                    }
                }

                w.Items = wItems.OrderBy(i => i.Order).ToList();
            }
            else
            {
                w.Items = new List<WorkoutItem>();
            }
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
        // Bütün gün boyunca girilen o egzersiz kayıtlarını aynı seans kabul ediyoruz.

        var minDate = lastLog.Date.Date;
        var maxDate = minDate.AddDays(1).AddTicks(-1);

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
    public async Task<bool> HasLogForTodayAsync(int workoutId, int? week = null, int? cycle = null)
    {
        await Init();
        var today = DateTime.Now.Date;
        var maxDate = today.AddDays(1).AddTicks(-1);

        var query = _connection!.Table<WorkoutLog>()
            .Where(l => l.WorkoutId == workoutId && l.Date >= today && l.Date <= maxDate);

        if (week.HasValue)
        {
            query = query.Where(l => l.Week == week.Value);
        }

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

    public async Task<bool> HasLogForWeekAsync(int workoutId, int week, int cycle)
    {
        await Init();
        var count = await _connection!.Table<WorkoutLog>()
            .Where(l => l.WorkoutId == workoutId && l.Week == week && l.Cycle == cycle && l.IsCompleted)
            .CountAsync();
        return count > 0;
    }

    public async Task<bool> HasSavedLogForWeekAsync(int workoutId, int week, int cycle)
    {
        await Init();
        var count = await _connection!.Table<WorkoutLog>()
            .Where(l => l.WorkoutId == workoutId && l.Week == week && l.Cycle == cycle && l.IsSaved && !l.IsCompleted)
            .CountAsync();
        return count > 0;
    }

    public async Task DeleteWorkoutLogAsync(int workoutId, int week, int cycle)
    {
        await Init();
        var logs = await _connection!.Table<WorkoutLog>()
            .Where(l => l.WorkoutId == workoutId && l.Week == week && l.Cycle == cycle)
            .ToListAsync();
        if (logs.Any())
        {
            foreach (var log in logs)
            {
                await _connection.DeleteAsync(log);
            }

            NotifyDatabaseChanged();
        }
    }

    public async Task ClearAllWorkoutLogsAsync()
    {
        await Init();
        await _connection!.ExecuteAsync("DELETE FROM WorkoutLog");
        NotifyDatabaseChanged();
    }

    public async Task<int> GetWorkoutLogsCountAsync()
    {
        await Init();
        return await _connection!.Table<WorkoutLog>().CountAsync();
    }

    public async Task<DateTime?> GetWorkoutLogDateAsync(int workoutId, int week, int cycle)
    {
        await Init();
        var log = await _connection!.Table<WorkoutLog>()
            .Where(l => l.WorkoutId == workoutId && l.Week == week && l.Cycle == cycle)
            .OrderByDescending(l => l.Date)
            .FirstOrDefaultAsync();
        return log?.Date;
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
            if (program != null && user.WorkoutSettings?.WeekLength == "Weekly")
            {
                var now = DateTime.Now;
                var lastUpdate = program.LastWeekUpdateDate;
                var weekStart = user.AppSettings?.WeekStartDay ?? DayOfWeek.Monday;

                int diff = (7 + (now.DayOfWeek - weekStart)) % 7;
                DateTime currentWeekStart = now.Date.AddDays(-diff);

                int lastDiff = (7 + (lastUpdate.DayOfWeek - weekStart)) % 7;
                DateTime lastWeekStart = lastUpdate.Date.AddDays(-lastDiff);

                int weeksPassed = (int)Math.Round((currentWeekStart - lastWeekStart).TotalDays / 7.0);
                if (weeksPassed > 0)
                {
                    program.Week += weeksPassed;
                    program.LastWeekUpdateDate = now;
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

        var existingWorkouts = await _connection!.Table<Workout>()
            .Where(w => w.WorkoutProgramId == targetProgramId)
            .ToListAsync();
        var maxOrder = existingWorkouts.Any() ? existingWorkouts.Max(w => w.Order) : 0;

        var newWorkout = new Workout
        {
            WorkoutProgramId = targetProgramId,
            Name = sourceWorkout.Name,
            Description = sourceWorkout.Description,
            Order = maxOrder + 1,
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

    public async Task<List<Workout>> GetAllWorkoutsWithProgramAsync()
    {
        await Init();
        var workouts = await _connection!.Table<Workout>().ToListAsync();

        var programIds = workouts.Where(w => w.WorkoutProgramId != 0).Select(w => w.WorkoutProgramId).Distinct()
            .ToList();
        var programs = new List<WorkoutProgram>();
        if (programIds.Any())
        {
            programs = await _connection.Table<WorkoutProgram>().Where(p => programIds.Contains(p.Id)).ToListAsync();
        }

        var programDict = programs.ToDictionary(p => p.Id);

        var items = await _connection.Table<WorkoutItem>().ToListAsync();
        var exerciseIds = items.Select(i => i.ExerciseId).Distinct().ToList();

        var exercises = new List<Exercise>();
        if (exerciseIds.Any())
        {
            exercises = await _connection.Table<Exercise>().Where(e => exerciseIds.Contains(e.Id)).ToListAsync();
            await PopulateExerciseMuscleMapsAsync(exercises);
        }

        var exDict = exercises.ToDictionary(e => e.Id);

        var itemsByWorkout = items.GroupBy(i => i.WorkoutId).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var workout in workouts)
        {
            if (workout.WorkoutProgramId != 0 && programDict.TryGetValue(workout.WorkoutProgramId, out var program))
            {
                workout.WorkoutProgram = program;
            }

            if (itemsByWorkout.TryGetValue(workout.Id, out var wItems))
            {
                foreach (var item in wItems)
                {
                    if (exDict.TryGetValue(item.ExerciseId, out var ex))
                    {
                        item.Exercise = ex;
                    }
                }

                workout.Items = wItems.OrderBy(i => i.Order).ToList();
            }
            else
            {
                workout.Items = new List<WorkoutItem>();
            }
        }

        return workouts;
    }

    public async Task<WorkoutProgram?> GetProgramByIdAsync(int id)
    {
        await Init();
        try
        {
            var program = await _connection!.Table<WorkoutProgram>().Where(p => p.Id == id).FirstOrDefaultAsync();
            if (program != null)
            {
                var workouts = await _connection.Table<Workout>().Where(w => w.WorkoutProgramId == id).ToListAsync();
                var workoutIds = workouts.Select(w => w.Id).ToList();

                var items = new List<WorkoutItem>();
                if (workoutIds.Any())
                {
                    items = await _connection.Table<WorkoutItem>().Where(i => workoutIds.Contains(i.WorkoutId))
                        .ToListAsync();
                }

                var exerciseIds = items.Select(i => i.ExerciseId).Distinct().ToList();
                var exercises = new List<Exercise>();
                if (exerciseIds.Any())
                {
                    exercises = await _connection.Table<Exercise>().Where(e => exerciseIds.Contains(e.Id))
                        .ToListAsync();
                    await PopulateExerciseMuscleMapsAsync(exercises);
                }

                var exDict = exercises.ToDictionary(e => e.Id);

                var itemsByWorkout = items.GroupBy(i => i.WorkoutId).ToDictionary(g => g.Key, g => g.ToList());

                foreach (var w in workouts)
                {
                    if (itemsByWorkout.TryGetValue(w.Id, out var wItems))
                    {
                        foreach (var item in wItems)
                        {
                            if (exDict.TryGetValue(item.ExerciseId, out var ex))
                            {
                                item.Exercise = ex;
                            }
                        }

                        w.Items = wItems.OrderBy(i => i.Order).ToList();
                    }
                    else
                    {
                        w.Items = new List<WorkoutItem>();
                    }
                }

                program.Workouts = workouts.OrderBy(w => w.Order).ToList();
            }

            return program;
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
            var workout = await _connection!.Table<Workout>().Where(w => w.Id == workoutId).FirstOrDefaultAsync();
            if (workout != null)
            {
                var items = await _connection.Table<WorkoutItem>().Where(i => i.WorkoutId == workoutId).ToListAsync();

                var exerciseIds = items.Select(i => i.ExerciseId).Distinct().ToList();
                var exercises = new List<Exercise>();
                if (exerciseIds.Any())
                {
                    exercises = await _connection.Table<Exercise>().Where(e => exerciseIds.Contains(e.Id))
                        .ToListAsync();
                    await PopulateExerciseMuscleMapsAsync(exercises);
                }

                var exDict = exercises.ToDictionary(e => e.Id);

                foreach (var item in items)
                {
                    if (exDict.TryGetValue(item.ExerciseId, out var ex))
                    {
                        item.Exercise = ex;
                    }
                }

                workout.Items = items.OrderBy(i => i.Order).ToList();
            }

            return workout;
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
            new MuscleGroup { Category = "Chest", Name = "Upper Chest" },
            new MuscleGroup { Category = "Chest", Name = "Mid/Lower Chest" },
            new MuscleGroup { Category = "Back", Name = "Lats" },
            new MuscleGroup { Category = "Back", Name = "Upper Traps" },
            new MuscleGroup { Category = "Back", Name = "Upper Back" },
            new MuscleGroup { Category = "Back", Name = "Lower Back" },
            new MuscleGroup { Category = "Shoulders", Name = "Front Delts" },
            new MuscleGroup { Category = "Shoulders", Name = "Side Delts" },
            new MuscleGroup { Category = "Shoulders", Name = "Rear Delts" },
            new MuscleGroup { Category = "Shoulders", Name = "Rotator Cuff" },
            new MuscleGroup { Category = "Arms", Name = "Biceps" },
            new MuscleGroup { Category = "Arms", Name = "Brachialis" },
            new MuscleGroup { Category = "Arms", Name = "Triceps Long Head" },
            new MuscleGroup { Category = "Arms", Name = "Triceps Short Heads" },
            new MuscleGroup { Category = "Arms", Name = "Forearms" },
            new MuscleGroup { Category = "Core", Name = "Upper Abs" },
            new MuscleGroup { Category = "Core", Name = "Lower Abs" },
            new MuscleGroup { Category = "Core", Name = "Obliques" },
            new MuscleGroup { Category = "Legs", Name = "Quads" },
            new MuscleGroup { Category = "Legs", Name = "Hamstrings" },
            new MuscleGroup { Category = "Legs", Name = "Glutes" },
            new MuscleGroup { Category = "Legs", Name = "Inner Thigh" },
            new MuscleGroup { Category = "Legs", Name = "Outer Thigh" },
            new MuscleGroup { Category = "Legs", Name = "Calves" }
        };
        await _connection!.InsertAllAsync(muscles);
        var dbMuscles = await _connection.Table<MuscleGroup>().ToListAsync();
        int GetMusId(string name) => dbMuscles.FirstOrDefault(m => m.Name == name)?.Id ?? 0;

        // --- 2. EGZERSİZLERİ OLUŞTUR ---
        var oldNameToExercise = new Dictionary<string, Exercise>
        {
            {
                "Bench Press", new Exercise
                {
                    Name = "Bench Press", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Barbell,
                    ImageUrl = "bench_press.png",
                    CnsFatigueScore = 6.5m,
                    Laterality = Exercise.LateralityType.Bilateral
                }
            },
            {
                "Squat", new Exercise
                {
                    Name = "Squat", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Barbell,
                    ImageUrl = "squat.png",
                    CnsFatigueScore = 8.5m,
                    Laterality = Exercise.LateralityType.Bilateral
                }
            },
            {
                "Deadlift", new Exercise
                {
                    Name = "Deadlift", Difficulty = "Advanced", Equipment = Exercise.EquipmentType.Barbell,
                    ImageUrl = "deadlift.png",
                    CnsFatigueScore = 9.5m,
                    Laterality = Exercise.LateralityType.Bilateral
                }
            },
            {
                "Overhead Press", new Exercise
                {
                    Name = "Overhead Press", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Barbell,
                    ImageUrl = "ohp.png",
                    CnsFatigueScore = 7.0m,
                    Laterality = Exercise.LateralityType.Bilateral
                }
            },
            {
                "Pull Up", new Exercise
                {
                    Name = "Pull Up", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Bodyweight,
                    ImageUrl = "pullup.png",
                    CnsFatigueScore = 6.0m,
                    Laterality = Exercise.LateralityType.Bilateral
                }
            },
            {
                "Barbell Row", new Exercise
                {
                    Name = "Row", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Barbell,
                    ImageUrl = "barbell_row.png",
                    CnsFatigueScore = 7.5m,
                    Laterality = Exercise.LateralityType.Bilateral
                }
            },
            {
                "Dumbbell Curl", new Exercise
                {
                    Name = "Curl", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Dumbbell,
                    ImageUrl = "curl.png",
                    CnsFatigueScore = 3.0m,
                    Laterality = Exercise.LateralityType.UnilateralAlternating
                }
            },
            {
                "Triceps Pushdown", new Exercise
                {
                    Name = "Triceps Pushdown", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Machine,
                    ImageUrl = "pushdown.png",
                    CnsFatigueScore = 3.0m,
                    Laterality = Exercise.LateralityType.Bilateral
                }
            },
            {
                "Lunges", new Exercise
                {
                    Name = "Lunges", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Dumbbell,
                    ImageUrl = "lunges.png",
                    CnsFatigueScore = 6.0m,
                    Laterality = Exercise.LateralityType.UnilateralAlternating
                }
            },
            {
                "Plank", new Exercise
                {
                    Name = "Plank", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Bodyweight,
                    ImageUrl = "plank.png",
                    CnsFatigueScore = 4.0m,
                    Laterality = Exercise.LateralityType.Bilateral
                }
            },
            {
                "Lateral Raise", new Exercise
                {
                    Name = "Lateral Raise", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Dumbbell,
                    ImageUrl = "lateral_raise.png",
                    CnsFatigueScore = 3.5m,
                    Laterality = Exercise.LateralityType.Bilateral
                }
            },
            {
                "Incline Dumbbell Press", new Exercise
                {
                    Name = "Press", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Dumbbell,
                    ImageUrl = "incline_press.png",
                    CnsFatigueScore = 5.5m,
                    Laterality = Exercise.LateralityType.Bilateral, VariationTags = new List<string> { "Incline" }
                }
            },
            {
                "Face Pull", new Exercise
                {
                    Name = "Face Pull", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Cable,
                    ImageUrl = "face_pull.png",
                    CnsFatigueScore = 3.5m,
                    Laterality = Exercise.LateralityType.Bilateral
                }
            },
            {
                "Hyperextension", new Exercise
                {
                    Name = "Hyperextension", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Bodyweight,
                    ImageUrl = "hyperextension.png",
                    CnsFatigueScore = 4.0m,
                    Laterality = Exercise.LateralityType.Bilateral
                }
            },
            {
                "Plate Loaded Chest Press", new Exercise
                {
                    Name = "Chest Press", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Machine,
                    ImageUrl = "chest_press.png",
                    CnsFatigueScore = 5.0m,
                    Laterality = Exercise.LateralityType.Bilateral, VariationTags = new List<string> { "Plate Loaded" }
                }
            },
            {
                "Smith Machine Low Incline Press", new Exercise
                {
                    Name = "Press", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Machine,
                    ImageUrl = "smith_incline_press.png",
                    CnsFatigueScore = 5.0m,
                    Laterality = Exercise.LateralityType.Bilateral,
                    VariationTags = new List<string> { "Smith Machine", "Low Incline" }
                }
            },
            {
                "Chest Fly Machine", new Exercise
                {
                    Name = "Chest Fly", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Machine,
                    ImageUrl = "chest_fly_machine.png",
                    CnsFatigueScore = 3.5m,
                    Laterality = Exercise.LateralityType.Bilateral
                }
            },
            {
                "Shoulder Press Machine", new Exercise
                {
                    Name = "Shoulder Press", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Machine,
                    ImageUrl = "shoulder_press_machine.png",
                    CnsFatigueScore = 4.0m,
                    Laterality = Exercise.LateralityType.Bilateral
                }
            },
            {
                "Overhead Rope Extension", new Exercise
                {
                    Name = "Overhead Extension", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Cable,
                    ImageUrl = "overhead_rope_extension.png",
                    CnsFatigueScore = 3.0m,
                    Laterality = Exercise.LateralityType.Bilateral, VariationTags = new List<string> { "Rope" }
                }
            },
            {
                "Lat Pulldown", new Exercise
                {
                    Name = "Lat Pulldown", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Machine,
                    ImageUrl = "lat_pulldown.png",
                    CnsFatigueScore = 4.5m,
                    Laterality = Exercise.LateralityType.Bilateral
                }
            },
            {
                "Plate Loaded Wide Grip Row", new Exercise
                {
                    Name = "Row", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Machine,
                    ImageUrl = "wide_grip_row.png",
                    CnsFatigueScore = 6.0m,
                    Laterality = Exercise.LateralityType.Bilateral,
                    VariationTags = new List<string> { "Plate Loaded", "Wide Grip" }
                }
            },
            {
                "Cable Row", new Exercise
                {
                    Name = "Row", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Cable,
                    ImageUrl = "cable_row.png",
                    CnsFatigueScore = 4.5m,
                    Laterality = Exercise.LateralityType.Bilateral
                }
            },
            {
                "Cable Curl", new Exercise
                {
                    Name = "Curl", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Cable,
                    ImageUrl = "cable_curl.png",
                    CnsFatigueScore = 3.0m,
                    Laterality = Exercise.LateralityType.Bilateral
                }
            },
            {
                "Hammer Curl", new Exercise
                {
                    Name = "Hammer Curl", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Dumbbell,
                    ImageUrl = "hammer_curl.png",
                    CnsFatigueScore = 3.5m,
                    Laterality = Exercise.LateralityType.UnilateralAlternating
                }
            },
            {
                "Reverse Barbell Curl", new Exercise
                {
                    Name = "Reverse Curl", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Barbell,
                    ImageUrl = "reverse_barbell_curl.png",
                    CnsFatigueScore = 4.0m,
                    Laterality = Exercise.LateralityType.Bilateral
                }
            },
            {
                "Leg Press", new Exercise
                {
                    Name = "Leg Press", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Machine,
                    ImageUrl = "leg_press.png",
                    CnsFatigueScore = 6.5m,
                    Laterality = Exercise.LateralityType.Bilateral
                }
            },
            {
                "Smith Machine Squat", new Exercise
                {
                    Name = "Squat", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Machine,
                    ImageUrl = "smith_squat.png",
                    CnsFatigueScore = 6.5m,
                    Laterality = Exercise.LateralityType.Bilateral, VariationTags = new List<string> { "Smith Machine" }
                }
            },
            {
                "Leg Extension", new Exercise
                {
                    Name = "Leg Extension", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Machine,
                    ImageUrl = "leg_extension.png",
                    CnsFatigueScore = 4.0m,
                    Laterality = Exercise.LateralityType.Bilateral
                }
            },
            {
                "Seated Leg Curl", new Exercise
                {
                    Name = "Leg Curl", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Machine,
                    ImageUrl = "seated_leg_curl.png",
                    CnsFatigueScore = 4.0m,
                    Laterality = Exercise.LateralityType.Bilateral, VariationTags = new List<string> { "Seated" }
                }
            },
            {
                "Cable Rear Delt Fly", new Exercise
                {
                    Name = "Cable Rear Delt Fly", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Cable,
                    ImageUrl = "cable_rear_delt_fly.png",
                    CnsFatigueScore = 3.0m,
                    Laterality = Exercise.LateralityType.Bilateral
                }
            },
            {
                "Close Grip Lat Pulldown", new Exercise
                {
                    Name = "Lat Pulldown", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Machine,
                    ImageUrl = "close_grip_lat_pulldown.png",
                    CnsFatigueScore = 4.5m,
                    Laterality = Exercise.LateralityType.Bilateral, VariationTags = new List<string> { "Close Grip" }
                }
            },
            {
                "Dips", new Exercise
                {
                    Name = "Dips", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Bodyweight,
                    ImageUrl = "dips.png",
                    CnsFatigueScore = 5.0m,
                    Laterality = Exercise.LateralityType.Bilateral
                }
            },
            {
                "Front Squat", new Exercise
                {
                    Name = "Squat", Difficulty = "Advanced", Equipment = Exercise.EquipmentType.Barbell,
                    ImageUrl = "front_squat.png",
                    CnsFatigueScore = 8.0m,
                    Laterality = Exercise.LateralityType.Bilateral, VariationTags = new List<string> { "Front" }
                }
            },
            {
                "Bulgarian Split Squat", new Exercise
                {
                    Name = "Split Squat", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Dumbbell,
                    ImageUrl = "bulgarian_split_squat.png",
                    CnsFatigueScore = 6.5m,
                    Laterality = Exercise.LateralityType.UnilateralIsolated,
                    VariationTags = new List<string> { "Bulgarian" }
                }
            },
            {
                "Incline Barbell Bench Press", new Exercise
                {
                    Name = "Bench Press", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Barbell,
                    ImageUrl = "incline_barbell_bench.png",
                    CnsFatigueScore = 6.0m,
                    Laterality = Exercise.LateralityType.Bilateral, VariationTags = new List<string> { "Incline" }
                }
            },
            {
                "Close Grip Bench Press", new Exercise
                {
                    Name = "Bench Press", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Barbell,
                    ImageUrl = "close_grip_bench.png",
                    CnsFatigueScore = 5.5m,
                    Laterality = Exercise.LateralityType.Bilateral, VariationTags = new List<string> { "Close Grip" }
                }
            },
            {
                "Chin Up", new Exercise
                {
                    Name = "Chin Up", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Bodyweight,
                    ImageUrl = "chinup.png",
                    CnsFatigueScore = 5.5m,
                    Laterality = Exercise.LateralityType.Bilateral
                }
            },
            {
                "Weighted Pull Up", new Exercise
                {
                    Name = "Pull Up", Difficulty = "Advanced", Equipment = Exercise.EquipmentType.Bodyweight,
                    ImageUrl = "weighted_pullup.png",
                    CnsFatigueScore = 7.0m,
                    Laterality = Exercise.LateralityType.Bilateral, VariationTags = new List<string> { "Weighted" }
                }
            },
            {
                "Seated Dumbbell Press", new Exercise
                {
                    Name = "Press", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Dumbbell,
                    ImageUrl = "seated_dumbbell_press.png",
                    CnsFatigueScore = 5.5m,
                    Laterality = Exercise.LateralityType.Bilateral, VariationTags = new List<string> { "Seated" }
                }
            },
            {
                "Push Press", new Exercise
                {
                    Name = "Push Press", Difficulty = "Advanced", Equipment = Exercise.EquipmentType.Barbell,
                    ImageUrl = "push_press.png",
                    CnsFatigueScore = 8.0m,
                    Laterality = Exercise.LateralityType.Bilateral
                }
            },
            {
                "Romanian Deadlift", new Exercise
                {
                    Name = "Deadlift", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Barbell,
                    ImageUrl = "rdl.png",
                    CnsFatigueScore = 7.5m,
                    Laterality = Exercise.LateralityType.Bilateral, VariationTags = new List<string> { "Romanian" }
                }
            },
            {
                "Sumo Deadlift", new Exercise
                {
                    Name = "Deadlift", Difficulty = "Advanced", Equipment = Exercise.EquipmentType.Barbell,
                    ImageUrl = "sumo_deadlift.png",
                    CnsFatigueScore = 9.0m,
                    Laterality = Exercise.LateralityType.Bilateral, VariationTags = new List<string> { "Sumo" }
                }
            },
            {
                "Pendlay Row", new Exercise
                {
                    Name = "Row", Difficulty = "Advanced", Equipment = Exercise.EquipmentType.Barbell,
                    ImageUrl = "pendlay_row.png",
                    CnsFatigueScore = 7.0m,
                    Laterality = Exercise.LateralityType.Bilateral, VariationTags = new List<string> { "Pendlay" }
                }
            },
            {
                "T-Bar Row", new Exercise
                {
                    Name = "Row", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Machine,
                    ImageUrl = "tbar_row.png",
                    CnsFatigueScore = 6.5m,
                    Laterality = Exercise.LateralityType.Bilateral, VariationTags = new List<string> { "T-Bar" }
                }
            },
            {
                "Cable Lat Pull Over", new Exercise
                {
                    Name = "Lat Pull Over", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Cable,
                    ImageUrl = "lat_pullover.png",
                    CnsFatigueScore = 5.0m,
                    Laterality = Exercise.LateralityType.Bilateral
                }
            },
            {
                "Smith Incline Bench Press", new Exercise
                {
                    Name = "Bench Press", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Machine,
                    ImageUrl = "smith_incline_press.png", CnsFatigueScore = 6.0m,
                    Laterality = Exercise.LateralityType.Bilateral,
                    VariationTags = new List<string> { "Smith", "Incline" }
                }
            },
            {
                "Fly Machine", new Exercise
                {
                    Name = "Chest Fly", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Machine,
                    ImageUrl = "fly_machine.png",
                    CnsFatigueScore = 4.0m,
                    Laterality = Exercise.LateralityType.Bilateral
                }
            },
            {
                "Rear Delt Machine Fly", new Exercise
                {
                    Name = "Rear Delt Fly", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Machine,
                    ImageUrl = "rear_delt_machine_fly.png", CnsFatigueScore = 4.0m,
                    Laterality = Exercise.LateralityType.Bilateral
                }
            },
            {
                "Seated Machine Row", new Exercise
                {
                    Name = "Row", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Machine,
                    ImageUrl = "seated_machine_row.png", CnsFatigueScore = 5.0m,
                    Laterality = Exercise.LateralityType.Bilateral, VariationTags = new List<string> { "Seated" }
                }
            },
            {
                "One Arm Cable Row", new Exercise
                {
                    Name = "Row", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Cable,
                    ImageUrl = "one_arm_cable_row.png", CnsFatigueScore = 5.0m,
                    Laterality = Exercise.LateralityType.UnilateralIsolated,
                    VariationTags = new List<string> { "One Arm" }
                }
            },
            {
                "Cable Lateral Raise", new Exercise
                {
                    Name = "Lateral Raise", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Cable,
                    ImageUrl = "cable_lateral_raise.png", CnsFatigueScore = 4.0m,
                    Laterality = Exercise.LateralityType.UnilateralIsolated
                }
            },
            {
                "Leg Raise", new Exercise
                {
                    Name = "Leg Raise", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Bodyweight,
                    ImageUrl = "leg_raise.png", CnsFatigueScore = 4.0m,
                    Laterality = Exercise.LateralityType.Bilateral
                }
            },
            {
                "Calf Raise", new Exercise
                {
                    Name = "Calf Raise", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Machine,
                    ImageUrl = "calf_raise.png", CnsFatigueScore = 4.0m,
                    Laterality = Exercise.LateralityType.Bilateral
                }
            },
            {
                "Shoulder Machine", new Exercise
                {
                    Name = "Shoulder Press", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Machine,
                    ImageUrl = "shoulder_machine.png", CnsFatigueScore = 5.0m,
                    Laterality = Exercise.LateralityType.Bilateral
                }
            },
            {
                "Triceps Kickback", new Exercise
                {
                    Name = "Triceps Kickback", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Dumbbell,
                    ImageUrl = "triceps_kickback.png", CnsFatigueScore = 3.0m,
                    Laterality = Exercise.LateralityType.UnilateralIsolated
                }
            },
            {
                "Ab Crunch", new Exercise
                {
                    Name = "Ab Crunch", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Bodyweight,
                    ImageUrl = "ab_crunch.png", CnsFatigueScore = 3.0m,
                    Laterality = Exercise.LateralityType.Bilateral
                }
            }
        };

        // Toplu ekle
        foreach (var ex in oldNameToExercise.Values)
        {
            if (ex.VariationTags != null && ex.VariationTags.Any())
            {
                ex.VariationTagsBlob = System.Text.Json.JsonSerializer.Serialize(ex.VariationTags);
            }
        }

        await _connection!.InsertAllAsync(oldNameToExercise.Values);

        // ID eşleşmesi için veritabanından geri çek
        var dbExercises = await _connection.Table<Exercise>().ToListAsync();

        // Helper fonksiyon
        int GetExId(string name) => oldNameToExercise.ContainsKey(name) ? oldNameToExercise[name].Id : 0;

        // --- 3. MAPPING (ÇOKA ÇOK) OLUŞTUR ---
        var mappings = new List<ExerciseMuscleMap>
        {
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Bench Press"), MuscleGroupId = GetMusId("Mid/Lower Chest"),
                ImpactMultiplier = 1.0, ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Bench Press"), MuscleGroupId = GetMusId("Upper Chest"), ImpactMultiplier = 0.5,
                ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Bench Press"), MuscleGroupId = GetMusId("Front Delts"), ImpactMultiplier = 0.5,
                ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Bench Press"), MuscleGroupId = GetMusId("Triceps Long Head"),
                ImpactMultiplier = 0.15, ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Bench Press"), MuscleGroupId = GetMusId("Triceps Short Heads"),
                ImpactMultiplier = 0.35, ActivationType = ActivationType.Secondary
            },

            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Squat"), MuscleGroupId = GetMusId("Quads"), ImpactMultiplier = 1.0,
                ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Squat"), MuscleGroupId = GetMusId("Glutes"), ImpactMultiplier = 0.7,
                ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Squat"), MuscleGroupId = GetMusId("Hamstrings"), ImpactMultiplier = 0.2,
                ActivationType = ActivationType.Synergist
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Squat"), MuscleGroupId = GetMusId("Lower Back"), ImpactMultiplier = 0.5,
                ActivationType = ActivationType.Stabilizer
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Squat"), MuscleGroupId = GetMusId("Inner Thigh"), ImpactMultiplier = 0.3,
                ActivationType = ActivationType.Synergist
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Squat"), MuscleGroupId = GetMusId("Upper Abs"), ImpactMultiplier = 0.3,
                ActivationType = ActivationType.Stabilizer
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Squat"), MuscleGroupId = GetMusId("Lower Abs"), ImpactMultiplier = 0.3,
                ActivationType = ActivationType.Stabilizer
            },

            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Deadlift"), MuscleGroupId = GetMusId("Hamstrings"), ImpactMultiplier = 1.0,
                ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Deadlift"), MuscleGroupId = GetMusId("Glutes"), ImpactMultiplier = 0.8,
                ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Deadlift"), MuscleGroupId = GetMusId("Lower Back"), ImpactMultiplier = 1.0,
                ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Deadlift"), MuscleGroupId = GetMusId("Lats"), ImpactMultiplier = 0.4,
                ActivationType = ActivationType.Synergist
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Deadlift"), MuscleGroupId = GetMusId("Quads"), ImpactMultiplier = 0.4,
                ActivationType = ActivationType.Synergist
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Deadlift"), MuscleGroupId = GetMusId("Upper Back"), ImpactMultiplier = 0.5,
                ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Deadlift"), MuscleGroupId = GetMusId("Upper Traps"), ImpactMultiplier = 0.5,
                ActivationType = ActivationType.Synergist
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Deadlift"), MuscleGroupId = GetMusId("Forearms"), ImpactMultiplier = 0.5,
                ActivationType = ActivationType.Synergist
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Deadlift"), MuscleGroupId = GetMusId("Upper Abs"), ImpactMultiplier = 0.3,
                ActivationType = ActivationType.Stabilizer
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Deadlift"), MuscleGroupId = GetMusId("Lower Abs"), ImpactMultiplier = 0.3,
                ActivationType = ActivationType.Stabilizer
            },

            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Overhead Press"), MuscleGroupId = GetMusId("Front Delts"), ImpactMultiplier = 1.0,
                ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Overhead Press"), MuscleGroupId = GetMusId("Side Delts"), ImpactMultiplier = 0.5,
                ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Overhead Press"), MuscleGroupId = GetMusId("Upper Chest"), ImpactMultiplier = 0.3,
                ActivationType = ActivationType.Synergist
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Overhead Press"), MuscleGroupId = GetMusId("Triceps Long Head"),
                ImpactMultiplier = 0.3, ActivationType = ActivationType.Synergist
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Overhead Press"), MuscleGroupId = GetMusId("Triceps Short Heads"),
                ImpactMultiplier = 0.7, ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Overhead Press"), MuscleGroupId = GetMusId("Upper Traps"), ImpactMultiplier = 0.7,
                ActivationType = ActivationType.Secondary
            },

            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Pull Up"), MuscleGroupId = GetMusId("Lats"), ImpactMultiplier = 1.0,
                ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Pull Up"), MuscleGroupId = GetMusId("Biceps"), ImpactMultiplier = 0.5,
                ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Pull Up"), MuscleGroupId = GetMusId("Upper Back"), ImpactMultiplier = 0.5,
                ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Pull Up"), MuscleGroupId = GetMusId("Rear Delts"), ImpactMultiplier = 0.4,
                ActivationType = ActivationType.Synergist
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Pull Up"), MuscleGroupId = GetMusId("Forearms"), ImpactMultiplier = 0.5,
                ActivationType = ActivationType.Synergist
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Pull Up"), MuscleGroupId = GetMusId("Brachialis"), ImpactMultiplier = 0.15,
                ActivationType = ActivationType.Synergist
            },

            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Barbell Row"), MuscleGroupId = GetMusId("Upper Back"), ImpactMultiplier = 1.0,
                ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Barbell Row"), MuscleGroupId = GetMusId("Lats"), ImpactMultiplier = 0.8,
                ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Barbell Row"), MuscleGroupId = GetMusId("Biceps"), ImpactMultiplier = 0.5,
                ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Barbell Row"), MuscleGroupId = GetMusId("Lower Back"), ImpactMultiplier = 0.5,
                ActivationType = ActivationType.Stabilizer
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Barbell Row"), MuscleGroupId = GetMusId("Rear Delts"), ImpactMultiplier = 0.5,
                ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Barbell Row"), MuscleGroupId = GetMusId("Forearms"), ImpactMultiplier = 0.5,
                ActivationType = ActivationType.Synergist
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Barbell Row"), MuscleGroupId = GetMusId("Brachialis"), ImpactMultiplier = 0.15,
                ActivationType = ActivationType.Synergist
            },

            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Dumbbell Curl"), MuscleGroupId = GetMusId("Biceps"), ImpactMultiplier = 1.0,
                ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Dumbbell Curl"), MuscleGroupId = GetMusId("Brachialis"), ImpactMultiplier = 0.2,
                ActivationType = ActivationType.Synergist
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Dumbbell Curl"), MuscleGroupId = GetMusId("Forearms"), ImpactMultiplier = 0.2,
                ActivationType = ActivationType.Synergist
            },

            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Triceps Pushdown"), MuscleGroupId = GetMusId("Triceps Long Head"),
                ImpactMultiplier = 0.25, ActivationType = ActivationType.Synergist
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Triceps Pushdown"), MuscleGroupId = GetMusId("Triceps Short Heads"),
                ImpactMultiplier = 0.75, ActivationType = ActivationType.Primary
            },

            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Lunges"), MuscleGroupId = GetMusId("Quads"), ImpactMultiplier = 1.0,
                ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Lunges"), MuscleGroupId = GetMusId("Glutes"), ImpactMultiplier = 0.6,
                ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Lunges"), MuscleGroupId = GetMusId("Hamstrings"), ImpactMultiplier = 0.2,
                ActivationType = ActivationType.Synergist
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Lunges"), MuscleGroupId = GetMusId("Inner Thigh"), ImpactMultiplier = 0.3,
                ActivationType = ActivationType.Synergist
            },

            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Plank"), MuscleGroupId = GetMusId("Upper Abs"), ImpactMultiplier = 1.0,
                ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Plank"), MuscleGroupId = GetMusId("Lower Abs"), ImpactMultiplier = 1.0,
                ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Plank"), MuscleGroupId = GetMusId("Obliques"), ImpactMultiplier = 0.4,
                ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Plank"), MuscleGroupId = GetMusId("Lower Back"), ImpactMultiplier = 0.2,
                ActivationType = ActivationType.Stabilizer
            },

            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Lateral Raise"), MuscleGroupId = GetMusId("Side Delts"), ImpactMultiplier = 1.0,
                ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Lateral Raise"), MuscleGroupId = GetMusId("Upper Traps"), ImpactMultiplier = 0.5,
                ActivationType = ActivationType.Secondary
            },

            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Incline Dumbbell Press"), MuscleGroupId = GetMusId("Upper Chest"),
                ImpactMultiplier = 1.0, ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Incline Dumbbell Press"), MuscleGroupId = GetMusId("Mid/Lower Chest"),
                ImpactMultiplier = 0.3, ActivationType = ActivationType.Synergist
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Incline Dumbbell Press"), MuscleGroupId = GetMusId("Front Delts"),
                ImpactMultiplier = 0.6, ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Incline Dumbbell Press"), MuscleGroupId = GetMusId("Triceps Long Head"),
                ImpactMultiplier = 0.15, ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Incline Dumbbell Press"), MuscleGroupId = GetMusId("Triceps Short Heads"),
                ImpactMultiplier = 0.35, ActivationType = ActivationType.Secondary
            },

            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Face Pull"), MuscleGroupId = GetMusId("Rear Delts"), ImpactMultiplier = 1.0,
                ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Face Pull"), MuscleGroupId = GetMusId("Upper Back"), ImpactMultiplier = 0.6,
                ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Face Pull"), MuscleGroupId = GetMusId("Upper Traps"), ImpactMultiplier = 0.5,
                ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Face Pull"), MuscleGroupId = GetMusId("Rotator Cuff"), ImpactMultiplier = 1.0,
                ActivationType = ActivationType.Secondary
            },

            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Hyperextension"), MuscleGroupId = GetMusId("Lower Back"), ImpactMultiplier = 1.0,
                ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Hyperextension"), MuscleGroupId = GetMusId("Hamstrings"), ImpactMultiplier = 0.8,
                ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Hyperextension"), MuscleGroupId = GetMusId("Glutes"), ImpactMultiplier = 0.8,
                ActivationType = ActivationType.Secondary
            },

            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Plate Loaded Chest Press"), MuscleGroupId = GetMusId("Mid/Lower Chest"),
                ImpactMultiplier = 1.0, ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Plate Loaded Chest Press"), MuscleGroupId = GetMusId("Upper Chest"),
                ImpactMultiplier = 0.5, ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Plate Loaded Chest Press"), MuscleGroupId = GetMusId("Front Delts"),
                ImpactMultiplier = 0.5, ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Plate Loaded Chest Press"), MuscleGroupId = GetMusId("Triceps Short Heads"),
                ImpactMultiplier = 0.4, ActivationType = ActivationType.Secondary
            },

            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Smith Machine Low Incline Press"), MuscleGroupId = GetMusId("Upper Chest"),
                ImpactMultiplier = 1.0, ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Smith Machine Low Incline Press"), MuscleGroupId = GetMusId("Mid/Lower Chest"),
                ImpactMultiplier = 0.6, ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Smith Machine Low Incline Press"), MuscleGroupId = GetMusId("Front Delts"),
                ImpactMultiplier = 0.6, ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Smith Machine Low Incline Press"),
                MuscleGroupId = GetMusId("Triceps Short Heads"), ImpactMultiplier = 0.4,
                ActivationType = ActivationType.Secondary
            },

            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Chest Fly Machine"), MuscleGroupId = GetMusId("Mid/Lower Chest"),
                ImpactMultiplier = 1.0, ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Chest Fly Machine"), MuscleGroupId = GetMusId("Upper Chest"),
                ImpactMultiplier = 0.5, ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Chest Fly Machine"), MuscleGroupId = GetMusId("Front Delts"),
                ImpactMultiplier = 0.2, ActivationType = ActivationType.Synergist
            },

            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Shoulder Press Machine"), MuscleGroupId = GetMusId("Front Delts"),
                ImpactMultiplier = 1.0, ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Shoulder Press Machine"), MuscleGroupId = GetMusId("Side Delts"),
                ImpactMultiplier = 0.4, ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Shoulder Press Machine"), MuscleGroupId = GetMusId("Triceps Short Heads"),
                ImpactMultiplier = 0.5, ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Shoulder Press Machine"), MuscleGroupId = GetMusId("Upper Chest"),
                ImpactMultiplier = 0.3, ActivationType = ActivationType.Synergist
            },

            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Overhead Rope Extension"), MuscleGroupId = GetMusId("Triceps Long Head"),
                ImpactMultiplier = 1.0, ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Overhead Rope Extension"), MuscleGroupId = GetMusId("Triceps Short Heads"),
                ImpactMultiplier = 0.4, ActivationType = ActivationType.Secondary
            },

            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Lat Pulldown"), MuscleGroupId = GetMusId("Lats"), ImpactMultiplier = 1.0,
                ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Lat Pulldown"), MuscleGroupId = GetMusId("Biceps"), ImpactMultiplier = 0.5,
                ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Lat Pulldown"), MuscleGroupId = GetMusId("Upper Back"), ImpactMultiplier = 0.4,
                ActivationType = ActivationType.Synergist
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Lat Pulldown"), MuscleGroupId = GetMusId("Rear Delts"), ImpactMultiplier = 0.3,
                ActivationType = ActivationType.Synergist
            },

            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Plate Loaded Wide Grip Row"), MuscleGroupId = GetMusId("Upper Back"),
                ImpactMultiplier = 1.0, ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Plate Loaded Wide Grip Row"), MuscleGroupId = GetMusId("Lats"),
                ImpactMultiplier = 0.6, ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Plate Loaded Wide Grip Row"), MuscleGroupId = GetMusId("Rear Delts"),
                ImpactMultiplier = 0.5, ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Plate Loaded Wide Grip Row"), MuscleGroupId = GetMusId("Biceps"),
                ImpactMultiplier = 0.4, ActivationType = ActivationType.Synergist
            },

            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Cable Row"), MuscleGroupId = GetMusId("Lats"), ImpactMultiplier = 1.0,
                ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Cable Row"), MuscleGroupId = GetMusId("Upper Back"), ImpactMultiplier = 0.8,
                ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Cable Row"), MuscleGroupId = GetMusId("Biceps"), ImpactMultiplier = 0.5,
                ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Cable Row"), MuscleGroupId = GetMusId("Lower Back"), ImpactMultiplier = 0.3,
                ActivationType = ActivationType.Stabilizer
            },

            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Cable Curl"), MuscleGroupId = GetMusId("Biceps"), ImpactMultiplier = 1.0,
                ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Cable Curl"), MuscleGroupId = GetMusId("Brachialis"), ImpactMultiplier = 0.2,
                ActivationType = ActivationType.Synergist
            },

            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Hammer Curl"), MuscleGroupId = GetMusId("Brachialis"), ImpactMultiplier = 1.0,
                ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Hammer Curl"), MuscleGroupId = GetMusId("Biceps"), ImpactMultiplier = 0.6,
                ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Hammer Curl"), MuscleGroupId = GetMusId("Forearms"), ImpactMultiplier = 0.8,
                ActivationType = ActivationType.Secondary
            },

            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Reverse Barbell Curl"), MuscleGroupId = GetMusId("Forearms"),
                ImpactMultiplier = 1.0, ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Reverse Barbell Curl"), MuscleGroupId = GetMusId("Brachialis"),
                ImpactMultiplier = 0.8, ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Reverse Barbell Curl"), MuscleGroupId = GetMusId("Biceps"),
                ImpactMultiplier = 0.4, ActivationType = ActivationType.Synergist
            },

            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Leg Press"), MuscleGroupId = GetMusId("Quads"), ImpactMultiplier = 1.0,
                ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Leg Press"), MuscleGroupId = GetMusId("Glutes"), ImpactMultiplier = 0.6,
                ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Leg Press"), MuscleGroupId = GetMusId("Hamstrings"), ImpactMultiplier = 0.2,
                ActivationType = ActivationType.Synergist
            },

            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Smith Machine Squat"), MuscleGroupId = GetMusId("Quads"), ImpactMultiplier = 1.0,
                ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Smith Machine Squat"), MuscleGroupId = GetMusId("Glutes"), ImpactMultiplier = 0.7,
                ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Smith Machine Squat"), MuscleGroupId = GetMusId("Hamstrings"),
                ImpactMultiplier = 0.2, ActivationType = ActivationType.Synergist
            },

            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Leg Extension"), MuscleGroupId = GetMusId("Quads"), ImpactMultiplier = 1.0,
                ActivationType = ActivationType.Primary
            },

            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Seated Leg Curl"), MuscleGroupId = GetMusId("Hamstrings"), ImpactMultiplier = 1.0,
                ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Seated Leg Curl"), MuscleGroupId = GetMusId("Calves"), ImpactMultiplier = 0.2,
                ActivationType = ActivationType.Synergist
            },

            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Cable Rear Delt Fly"), MuscleGroupId = GetMusId("Rear Delts"),
                ImpactMultiplier = 1.0, ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Cable Rear Delt Fly"), MuscleGroupId = GetMusId("Upper Back"),
                ImpactMultiplier = 0.4, ActivationType = ActivationType.Secondary
            },

            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Close Grip Lat Pulldown"), MuscleGroupId = GetMusId("Lats"),
                ImpactMultiplier = 1.0, ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Close Grip Lat Pulldown"), MuscleGroupId = GetMusId("Biceps"),
                ImpactMultiplier = 0.6, ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Close Grip Lat Pulldown"), MuscleGroupId = GetMusId("Upper Back"),
                ImpactMultiplier = 0.3, ActivationType = ActivationType.Synergist
            },

            // Dips
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Dips"), MuscleGroupId = GetMusId("Mid/Lower Chest"), ImpactMultiplier = 1.0,
                ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Dips"), MuscleGroupId = GetMusId("Triceps Short Heads"), ImpactMultiplier = 0.8,
                ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Dips"), MuscleGroupId = GetMusId("Front Delts"), ImpactMultiplier = 0.6,
                ActivationType = ActivationType.Secondary
            },

            // Front Squat
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Front Squat"), MuscleGroupId = GetMusId("Quads"), ImpactMultiplier = 1.0,
                ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Front Squat"), MuscleGroupId = GetMusId("Glutes"), ImpactMultiplier = 0.6,
                ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Front Squat"), MuscleGroupId = GetMusId("Upper Back"), ImpactMultiplier = 0.4,
                ActivationType = ActivationType.Stabilizer
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Front Squat"), MuscleGroupId = GetMusId("Upper Abs"), ImpactMultiplier = 0.5,
                ActivationType = ActivationType.Stabilizer
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Front Squat"), MuscleGroupId = GetMusId("Lower Abs"), ImpactMultiplier = 0.5,
                ActivationType = ActivationType.Stabilizer
            },

            // Bulgarian Split Squat
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Bulgarian Split Squat"), MuscleGroupId = GetMusId("Quads"),
                ImpactMultiplier = 1.0, ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Bulgarian Split Squat"), MuscleGroupId = GetMusId("Glutes"),
                ImpactMultiplier = 0.8, ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Bulgarian Split Squat"), MuscleGroupId = GetMusId("Hamstrings"),
                ImpactMultiplier = 0.3, ActivationType = ActivationType.Synergist
            },

            // Incline Barbell Bench Press
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Incline Barbell Bench Press"), MuscleGroupId = GetMusId("Upper Chest"),
                ImpactMultiplier = 1.0, ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Incline Barbell Bench Press"), MuscleGroupId = GetMusId("Mid/Lower Chest"),
                ImpactMultiplier = 0.5, ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Incline Barbell Bench Press"), MuscleGroupId = GetMusId("Front Delts"),
                ImpactMultiplier = 0.6, ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Incline Barbell Bench Press"), MuscleGroupId = GetMusId("Triceps Short Heads"),
                ImpactMultiplier = 0.4, ActivationType = ActivationType.Secondary
            },

            // Close Grip Bench Press
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Close Grip Bench Press"), MuscleGroupId = GetMusId("Triceps Short Heads"),
                ImpactMultiplier = 1.0, ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Close Grip Bench Press"), MuscleGroupId = GetMusId("Mid/Lower Chest"),
                ImpactMultiplier = 0.6, ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Close Grip Bench Press"), MuscleGroupId = GetMusId("Front Delts"),
                ImpactMultiplier = 0.4, ActivationType = ActivationType.Synergist
            },

            // Chin Up
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Chin Up"), MuscleGroupId = GetMusId("Lats"), ImpactMultiplier = 1.0,
                ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Chin Up"), MuscleGroupId = GetMusId("Biceps"), ImpactMultiplier = 0.8,
                ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Chin Up"), MuscleGroupId = GetMusId("Upper Back"), ImpactMultiplier = 0.4,
                ActivationType = ActivationType.Synergist
            },

            // Weighted Pull Up
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Weighted Pull Up"), MuscleGroupId = GetMusId("Lats"), ImpactMultiplier = 1.0,
                ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Weighted Pull Up"), MuscleGroupId = GetMusId("Upper Back"),
                ImpactMultiplier = 0.6, ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Weighted Pull Up"), MuscleGroupId = GetMusId("Biceps"), ImpactMultiplier = 0.5,
                ActivationType = ActivationType.Secondary
            },

            // Seated Dumbbell Press
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Seated Dumbbell Press"), MuscleGroupId = GetMusId("Front Delts"),
                ImpactMultiplier = 1.0, ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Seated Dumbbell Press"), MuscleGroupId = GetMusId("Side Delts"),
                ImpactMultiplier = 0.6, ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Seated Dumbbell Press"), MuscleGroupId = GetMusId("Triceps Short Heads"),
                ImpactMultiplier = 0.5, ActivationType = ActivationType.Secondary
            },

            // Push Press
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Push Press"), MuscleGroupId = GetMusId("Front Delts"), ImpactMultiplier = 1.0,
                ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Push Press"), MuscleGroupId = GetMusId("Triceps Short Heads"),
                ImpactMultiplier = 0.6, ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Push Press"), MuscleGroupId = GetMusId("Quads"), ImpactMultiplier = 0.4,
                ActivationType = ActivationType.Synergist
            },

            // Romanian Deadlift
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Romanian Deadlift"), MuscleGroupId = GetMusId("Hamstrings"),
                ImpactMultiplier = 1.0, ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Romanian Deadlift"), MuscleGroupId = GetMusId("Glutes"), ImpactMultiplier = 0.8,
                ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Romanian Deadlift"), MuscleGroupId = GetMusId("Lower Back"),
                ImpactMultiplier = 0.6, ActivationType = ActivationType.Secondary
            },

            // Sumo Deadlift
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Sumo Deadlift"), MuscleGroupId = GetMusId("Glutes"), ImpactMultiplier = 1.0,
                ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Sumo Deadlift"), MuscleGroupId = GetMusId("Quads"), ImpactMultiplier = 0.8,
                ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Sumo Deadlift"), MuscleGroupId = GetMusId("Hamstrings"), ImpactMultiplier = 0.6,
                ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Sumo Deadlift"), MuscleGroupId = GetMusId("Lower Back"), ImpactMultiplier = 0.5,
                ActivationType = ActivationType.Stabilizer
            },

            // Pendlay Row
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Pendlay Row"), MuscleGroupId = GetMusId("Upper Back"), ImpactMultiplier = 1.0,
                ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Pendlay Row"), MuscleGroupId = GetMusId("Lats"), ImpactMultiplier = 0.8,
                ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Pendlay Row"), MuscleGroupId = GetMusId("Lower Back"), ImpactMultiplier = 0.4,
                ActivationType = ActivationType.Stabilizer
            },

            // T-Bar Row
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("T-Bar Row"), MuscleGroupId = GetMusId("Upper Back"), ImpactMultiplier = 1.0,
                ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("T-Bar Row"), MuscleGroupId = GetMusId("Lats"), ImpactMultiplier = 0.8,
                ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("T-Bar Row"), MuscleGroupId = GetMusId("Biceps"), ImpactMultiplier = 0.4,
                ActivationType = ActivationType.Synergist
            }
        };
        await _connection.InsertAllAsync(mappings);


        // --- 2. PROGRAM 1: START STRONG ---
        var program1 = new WorkoutProgram
        {
            Name = "Start Strong",
            Level = "Beginner",
            Goal = "Strength",
            TargetMuscles = "Full Body",
            Environment = "Gym"
        };
        await _connection.InsertAsync(program1);

        // Program 1 - Day A
        var p1_w1 = new Workout
        {
            WorkoutProgramId = program1.Id, Name = "Full Body A", Order = 1, Description = "Basic push and leg focused."
        };
        await _connection.InsertAsync(p1_w1);
        await _connection.InsertAllAsync(new[]
        {
            new WorkoutItem
                { WorkoutId = p1_w1.Id, ExerciseId = GetExId("Squat"), Sets = 3, RepsRange = "8-10", Order = 1 },
            new WorkoutItem
                { WorkoutId = p1_w1.Id, ExerciseId = GetExId("Bench Press"), Sets = 3, RepsRange = "8-12", Order = 2 },
            new WorkoutItem
            {
                WorkoutId = p1_w1.Id, ExerciseId = GetExId("Barbell Row"), Sets = 3, RepsRange = "10-12", Order = 3
            }, // Artık ID'si 0 dönmeyecek!
            new WorkoutItem
            {
                WorkoutId = p1_w1.Id, ExerciseId = GetExId("Dumbbell Curl"), Sets = 3, RepsRange = "12-15", Order = 4
            }
        });

        // Program 1 - Day B
        var p1_w2 = new Workout
        {
            WorkoutProgramId = program1.Id, Name = "Full Body B", Order = 2, Description = "Pull and shoulder focused."
        };
        await _connection.InsertAsync(p1_w2);
        await _connection.InsertAllAsync(new[]
        {
            new WorkoutItem
                { WorkoutId = p1_w2.Id, ExerciseId = GetExId("Deadlift"), Sets = 3, RepsRange = "5", Order = 1 },
            new WorkoutItem
            {
                WorkoutId = p1_w2.Id, ExerciseId = GetExId("Overhead Press"), Sets = 3, RepsRange = "8-10", Order = 2
            },
            new WorkoutItem
                { WorkoutId = p1_w2.Id, ExerciseId = GetExId("Pull Up"), Sets = 3, RepsRange = "Max", Order = 3 },
            new WorkoutItem
                { WorkoutId = p1_w2.Id, ExerciseId = GetExId("Plank"), Sets = 3, RepsRange = "45s", Order = 4 }
        });


        // --- 3. PROGRAM 2: PPL ---
        var program2 = new WorkoutProgram
        {
            Name = "Classic PPL",
            Level = "Intermediate",
            Goal = "Hypertrophy",
            TargetMuscles = "Split",
            Environment = "Gym"
        };
        await _connection.InsertAsync(program2);

        // Push Day
        var p2_push = new Workout
            { WorkoutProgramId = program2.Id, Name = "Push", Order = 1, Description = "Chest, Shoulders, Triceps" };
        await _connection.InsertAsync(p2_push);
        await _connection.InsertAllAsync(new[]
        {
            new WorkoutItem
                { WorkoutId = p2_push.Id, ExerciseId = GetExId("Bench Press"), Sets = 4, RepsRange = "6-8", Order = 1 },
            new WorkoutItem
            {
                WorkoutId = p2_push.Id, ExerciseId = GetExId("Overhead Press"), Sets = 3, RepsRange = "8-10", Order = 2
            },
            new WorkoutItem
            {
                WorkoutId = p2_push.Id, ExerciseId = GetExId("Lateral Raise"), Sets = 3, RepsRange = "12-15", Order = 3
            },
            new WorkoutItem
            {
                WorkoutId = p2_push.Id, ExerciseId = GetExId("Triceps Pushdown"), Sets = 3, RepsRange = "12-15",
                Order = 4
            }
        });

        // Pull Day
        var p2_pull = new Workout
            { WorkoutProgramId = program2.Id, Name = "Pull", Order = 2, Description = "Back, Biceps" };
        await _connection.InsertAsync(p2_pull);
        await _connection.InsertAllAsync(new[]
        {
            new WorkoutItem
                { WorkoutId = p2_pull.Id, ExerciseId = GetExId("Deadlift"), Sets = 3, RepsRange = "5-8", Order = 1 },
            new WorkoutItem
                { WorkoutId = p2_pull.Id, ExerciseId = GetExId("Pull Up"), Sets = 3, RepsRange = "8-10", Order = 2 },
            new WorkoutItem
            {
                WorkoutId = p2_pull.Id, ExerciseId = GetExId("Dumbbell Curl"), Sets = 4, RepsRange = "10-12", Order = 3
            }
        });

        // Legs Day
        var p2_legs = new Workout
        {
            WorkoutProgramId = program2.Id, Name = "Legs", Order = 3, Description = "Quads, Hamstrings, Glutes"
        };
        await _connection.InsertAsync(p2_legs);
        await _connection.InsertAllAsync(new[]
        {
            new WorkoutItem
                { WorkoutId = p2_legs.Id, ExerciseId = GetExId("Squat"), Sets = 4, RepsRange = "6-8", Order = 1 },
            new WorkoutItem
                { WorkoutId = p2_legs.Id, ExerciseId = GetExId("Lunges"), Sets = 3, RepsRange = "10-12", Order = 2 },
            new WorkoutItem
                { WorkoutId = p2_legs.Id, ExerciseId = GetExId("Plank"), Sets = 3, RepsRange = "60s", Order = 3 }
        });

        // --- 4. PROGRAM 3: Güray's Hypertrophy Max ---
        var program3 = new WorkoutProgram
        {
            Name = "Güray's Hypertrophy Max",
            Level = "Advanced",
            Goal = "Hypertrophy",
            TargetMuscles = "Split",
            Environment = "Gym"
        };
        await _connection.InsertAsync(program3);

        // Day 1: Push / Chest & Shoulders
        var p3_day1 = new Workout
            { WorkoutProgramId = program3.Id, Name = "Day 1", Order = 1, Description = "Chest, Shoulders, Triceps" };
        await _connection.InsertAsync(p3_day1);
        await _connection.InsertAllAsync(new[]
        {
            new WorkoutItem
            {
                WorkoutId = p3_day1.Id, ExerciseId = GetExId("Plate Loaded Chest Press"), Sets = 2, RepsRange = "8-12",
                Order = 1
            },
            new WorkoutItem
            {
                WorkoutId = p3_day1.Id, ExerciseId = GetExId("Smith Machine Low Incline Press"), Sets = 2,
                RepsRange = "8-12", Order = 2
            },
            new WorkoutItem
            {
                WorkoutId = p3_day1.Id, ExerciseId = GetExId("Chest Fly Machine"), Sets = 1, RepsRange = "10-15",
                Order = 3
            },
            new WorkoutItem
            {
                WorkoutId = p3_day1.Id, ExerciseId = GetExId("Shoulder Press Machine"), Sets = 2, RepsRange = "8-12",
                Order = 4
            },
            new WorkoutItem
            {
                WorkoutId = p3_day1.Id, ExerciseId = GetExId("Lateral Raise"), Sets = 3, RepsRange = "12-15", Order = 5
            },
            new WorkoutItem
            {
                WorkoutId = p3_day1.Id, ExerciseId = GetExId("Triceps Pushdown"), Sets = 2, RepsRange = "10-15",
                Order = 6
            },
            new WorkoutItem
            {
                WorkoutId = p3_day1.Id, ExerciseId = GetExId("Overhead Rope Extension"), Sets = 2, RepsRange = "10-15",
                Order = 7
            }
        });

        // Day 2: Pull / Back & Biceps
        var p3_day2 = new Workout
            { WorkoutProgramId = program3.Id, Name = "Day 2", Order = 2, Description = "Back, Biceps" };
        await _connection.InsertAsync(p3_day2);
        await _connection.InsertAllAsync(new[]
        {
            new WorkoutItem
            {
                WorkoutId = p3_day2.Id, ExerciseId = GetExId("Lat Pulldown"), Sets = 2, RepsRange = "8-12", Order = 1
            },
            new WorkoutItem
            {
                WorkoutId = p3_day2.Id, ExerciseId = GetExId("Plate Loaded Wide Grip Row"), Sets = 3,
                RepsRange = "8-12", Order = 2
            },
            new WorkoutItem
                { WorkoutId = p3_day2.Id, ExerciseId = GetExId("Cable Row"), Sets = 1, RepsRange = "10-15", Order = 3 },
            new WorkoutItem
            {
                WorkoutId = p3_day2.Id, ExerciseId = GetExId("Dumbbell Curl"), Sets = 2, RepsRange = "10-15", Order = 4
            },
            new WorkoutItem
            {
                WorkoutId = p3_day2.Id, ExerciseId = GetExId("Cable Curl"), Sets = 2, RepsRange = "10-15", Order = 5
            },
            new WorkoutItem
            {
                WorkoutId = p3_day2.Id, ExerciseId = GetExId("Hammer Curl"), Sets = 2, RepsRange = "10-15", Order = 6
            },
            new WorkoutItem
            {
                WorkoutId = p3_day2.Id, ExerciseId = GetExId("Reverse Barbell Curl"), Sets = 2, RepsRange = "10-15",
                Order = 7
            }
        });

        // Day 3: Legs
        var p3_day3 = new Workout
            { WorkoutProgramId = program3.Id, Name = "Day 3", Order = 3, Description = "Quads, Hamstrings, Glutes" };
        await _connection.InsertAsync(p3_day3);
        await _connection.InsertAllAsync(new[]
        {
            new WorkoutItem
                { WorkoutId = p3_day3.Id, ExerciseId = GetExId("Leg Press"), Sets = 2, RepsRange = "8-12", Order = 1 },
            new WorkoutItem
            {
                WorkoutId = p3_day3.Id, ExerciseId = GetExId("Smith Machine Squat"), Sets = 2, RepsRange = "8-12",
                Order = 2
            },
            new WorkoutItem
            {
                WorkoutId = p3_day3.Id, ExerciseId = GetExId("Leg Extension"), Sets = 2, RepsRange = "12-15", Order = 3
            },
            new WorkoutItem
            {
                WorkoutId = p3_day3.Id, ExerciseId = GetExId("Seated Leg Curl"), Sets = 3, RepsRange = "10-15",
                Order = 4
            }
        });

        // Day 4: Shoulders & Chest & Triceps
        var p3_day4 = new Workout
            { WorkoutProgramId = program3.Id, Name = "Day 4", Order = 4, Description = "Shoulders, Chest, Triceps" };
        await _connection.InsertAsync(p3_day4);
        await _connection.InsertAllAsync(new[]
        {
            new WorkoutItem
            {
                WorkoutId = p3_day4.Id, ExerciseId = GetExId("Shoulder Press Machine"), Sets = 2, RepsRange = "8-12",
                Order = 1
            },
            new WorkoutItem
            {
                WorkoutId = p3_day4.Id, ExerciseId = GetExId("Lateral Raise"), Sets = 3, RepsRange = "12-15", Order = 2
            },
            new WorkoutItem
            {
                WorkoutId = p3_day4.Id, ExerciseId = GetExId("Smith Machine Low Incline Press"), Sets = 2,
                RepsRange = "8-12", Order = 3
            },
            new WorkoutItem
            {
                WorkoutId = p3_day4.Id, ExerciseId = GetExId("Chest Fly Machine"), Sets = 2, RepsRange = "10-15",
                Order = 4
            },
            new WorkoutItem
            {
                WorkoutId = p3_day4.Id, ExerciseId = GetExId("Cable Rear Delt Fly"), Sets = 2, RepsRange = "12-15",
                Order = 5
            },
            new WorkoutItem
            {
                WorkoutId = p3_day4.Id, ExerciseId = GetExId("Triceps Pushdown"), Sets = 2, RepsRange = "10-15",
                Order = 6
            },
            new WorkoutItem
            {
                WorkoutId = p3_day4.Id, ExerciseId = GetExId("Overhead Rope Extension"), Sets = 2, RepsRange = "10-15",
                Order = 7
            }
        });

        // Day 5: Back & Biceps & Legs
        var p3_day5 = new Workout
            { WorkoutProgramId = program3.Id, Name = "Day 5", Order = 5, Description = "Back, Biceps, Legs" };
        await _connection.InsertAsync(p3_day5);
        await _connection.InsertAllAsync(new[]
        {
            new WorkoutItem
            {
                WorkoutId = p3_day5.Id, ExerciseId = GetExId("Plate Loaded Wide Grip Row"), Sets = 3,
                RepsRange = "8-12", Order = 1
            },
            new WorkoutItem
            {
                WorkoutId = p3_day5.Id, ExerciseId = GetExId("Close Grip Lat Pulldown"), Sets = 3, RepsRange = "8-12",
                Order = 2
            },
            new WorkoutItem
            {
                WorkoutId = p3_day5.Id, ExerciseId = GetExId("Cable Curl"), Sets = 2, RepsRange = "10-15", Order = 3
            },
            new WorkoutItem
            {
                WorkoutId = p3_day5.Id, ExerciseId = GetExId("Hammer Curl"), Sets = 2, RepsRange = "10-15", Order = 4
            },
            new WorkoutItem
            {
                WorkoutId = p3_day5.Id, ExerciseId = GetExId("Reverse Barbell Curl"), Sets = 2, RepsRange = "10-15",
                Order = 5
            },
            new WorkoutItem
                { WorkoutId = p3_day5.Id, ExerciseId = GetExId("Leg Press"), Sets = 2, RepsRange = "8-12", Order = 6 },
            new WorkoutItem
            {
                WorkoutId = p3_day5.Id, ExerciseId = GetExId("Leg Extension"), Sets = 2, RepsRange = "12-15", Order = 7
            },
            new WorkoutItem
            {
                WorkoutId = p3_day5.Id, ExerciseId = GetExId("Seated Leg Curl"), Sets = 1, RepsRange = "10-15",
                Order = 8
            }
        });
    }

    private async Task SeedMockProgramAsync()
    {
        var dbMuscles = await _connection!.Table<MuscleGroup>().ToListAsync();
        int GetMusId(string name) => dbMuscles.FirstOrDefault(m => m.Name == name)?.Id ?? 0;


        var exercisesToSeed = new List<Exercise>
        {
            new Exercise
            {
                Name = "Cable Lat Pull Over", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Cable,
                CnsFatigueScore = 5.0m
            },
            new Exercise
            {
                Name = "Smith Incline Bench Press", Difficulty = "Intermediate",
                Equipment = Exercise.EquipmentType.Machine,
                CnsFatigueScore = 6.0m
            },
            new Exercise
            {
                Name = "Fly Machine", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Machine,
                CnsFatigueScore = 4.0m
            },
            new Exercise
            {
                Name = "Shoulder Machine", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Machine,
                CnsFatigueScore = 4.5m
            },
            new Exercise
            {
                Name = "Triceps Kickback", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Dumbbell,
                CnsFatigueScore = 3.0m
            },
            new Exercise
            {
                Name = "Ab Crunch", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Bodyweight,
                CnsFatigueScore = 3.5m
            },
            new Exercise
            {
                Name = "Leg Raise", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Bodyweight,
                CnsFatigueScore = 4.0m
            },
            new Exercise
            {
                Name = "Leg Press", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Machine,
                CnsFatigueScore = 7.0m
            },
            new Exercise
            {
                Name = "Leg Extension", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Machine,
                CnsFatigueScore = 5.0m
            },
            new Exercise
            {
                Name = "Leg Curl", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Machine,
                CnsFatigueScore = 5.0m
            },
            new Exercise
            {
                Name = "Rear Delt Machine Fly", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Machine,
                CnsFatigueScore = 4.0m
            },
            new Exercise
            {
                Name = "Hammer Curl", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Dumbbell,
                CnsFatigueScore = 3.0m
            },
            new Exercise
            {
                Name = "Barbell Curl", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Barbell,
                CnsFatigueScore = 4.0m
            },
            new Exercise
            {
                Name = "Calf Raise", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Machine,
                CnsFatigueScore = 3.5m
            },
            new Exercise
            {
                Name = "Seated Machine Row", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Machine,
                CnsFatigueScore = 6.0m
            },
            new Exercise
            {
                Name = "One Arm Cable Row", Difficulty = "Intermediate", Equipment = Exercise.EquipmentType.Cable,
                CnsFatigueScore = 5.0m
            },
            new Exercise
            {
                Name = "Cable Lateral Raise", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Cable,
                CnsFatigueScore = 3.5m
            },
            new Exercise
            {
                Name = "Russian Twist", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Bodyweight,
                CnsFatigueScore = 3.0m
            },
            new Exercise
            {
                Name = "Hip Abductor Machine", Difficulty = "Beginner", Equipment = Exercise.EquipmentType.Machine,
                CnsFatigueScore = 3.0m
            }
        };

        var dbExercises = await _connection.Table<Exercise>().ToListAsync();

        foreach (var ex in exercisesToSeed)
        {
            if (!dbExercises.Any(e => e.Name == ex.Name))
            {
                await _connection.InsertAsync(ex);
                dbExercises.Add(ex);
            }
        }

        int GetExId(string name, Exercise.EquipmentType? eq = null, string tag = "") => dbExercises.FirstOrDefault(e =>
            e.Name == name && (eq == null || e.Equipment == eq) && (string.IsNullOrEmpty(tag) ||
                                                                    (!string.IsNullOrEmpty(e.VariationTagsBlob) &&
                                                                     e.VariationTagsBlob.Contains(tag))))?.Id ?? 0;

        var mockMappings = new List<ExerciseMuscleMap>
        {
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Lat Pull Over", Exercise.EquipmentType.Cable), MuscleGroupId = GetMusId("Lats"),
                ImpactMultiplier = 1.0,
                ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Lat Pull Over", Exercise.EquipmentType.Cable),
                MuscleGroupId = GetMusId("Triceps Long Head"),
                ImpactMultiplier = 0.5, ActivationType = ActivationType.Synergist
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Bench Press", Exercise.EquipmentType.Machine, "Smith"),
                MuscleGroupId = GetMusId("Upper Chest"),
                ImpactMultiplier = 1.0, ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Bench Press", Exercise.EquipmentType.Machine, "Smith"),
                MuscleGroupId = GetMusId("Mid/Lower Chest"),
                ImpactMultiplier = 0.3, ActivationType = ActivationType.Synergist
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Bench Press", Exercise.EquipmentType.Machine, "Smith"),
                MuscleGroupId = GetMusId("Front Delts"),
                ImpactMultiplier = 0.6, ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Bench Press", Exercise.EquipmentType.Machine, "Smith"),
                MuscleGroupId = GetMusId("Triceps Long Head"),
                ImpactMultiplier = 0.15, ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Bench Press", Exercise.EquipmentType.Machine, "Smith"),
                MuscleGroupId = GetMusId("Triceps Short Heads"),
                ImpactMultiplier = 0.35, ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Chest Fly", Exercise.EquipmentType.Machine),
                MuscleGroupId = GetMusId("Mid/Lower Chest"),
                ImpactMultiplier = 1.0, ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Chest Fly", Exercise.EquipmentType.Machine),
                MuscleGroupId = GetMusId("Front Delts"), ImpactMultiplier = 0.3,
                ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Chest Fly", Exercise.EquipmentType.Machine),
                MuscleGroupId = GetMusId("Upper Chest"), ImpactMultiplier = 0.2,
                ActivationType = ActivationType.Synergist
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Shoulder Press", Exercise.EquipmentType.Machine),
                MuscleGroupId = GetMusId("Front Delts"),
                ImpactMultiplier = 1.0, ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Shoulder Press", Exercise.EquipmentType.Machine),
                MuscleGroupId = GetMusId("Side Delts"),
                ImpactMultiplier = 0.5, ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Shoulder Press", Exercise.EquipmentType.Machine),
                MuscleGroupId = GetMusId("Upper Chest"),
                ImpactMultiplier = 0.3, ActivationType = ActivationType.Synergist
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Shoulder Press", Exercise.EquipmentType.Machine),
                MuscleGroupId = GetMusId("Triceps Long Head"),
                ImpactMultiplier = 0.3, ActivationType = ActivationType.Synergist
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Shoulder Press", Exercise.EquipmentType.Machine),
                MuscleGroupId = GetMusId("Triceps Short Heads"),
                ImpactMultiplier = 0.7, ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Shoulder Press", Exercise.EquipmentType.Machine),
                MuscleGroupId = GetMusId("Upper Traps"),
                ImpactMultiplier = 0.7, ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Triceps Kickback"), MuscleGroupId = GetMusId("Triceps Long Head"),
                ImpactMultiplier = 0.1, ActivationType = ActivationType.Synergist
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Triceps Kickback"), MuscleGroupId = GetMusId("Triceps Short Heads"),
                ImpactMultiplier = 0.9, ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Ab Crunch"), MuscleGroupId = GetMusId("Upper Abs"), ImpactMultiplier = 0.7,
                ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Ab Crunch"), MuscleGroupId = GetMusId("Lower Abs"), ImpactMultiplier = 0.3,
                ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Leg Raise"), MuscleGroupId = GetMusId("Upper Abs"), ImpactMultiplier = 0.3,
                ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Leg Raise"), MuscleGroupId = GetMusId("Lower Abs"), ImpactMultiplier = 0.7,
                ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Leg Press"), MuscleGroupId = GetMusId("Quads"), ImpactMultiplier = 1.0,
                ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Leg Press"), MuscleGroupId = GetMusId("Glutes"), ImpactMultiplier = 0.5,
                ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Leg Press"), MuscleGroupId = GetMusId("Hamstrings"), ImpactMultiplier = 0.2,
                ActivationType = ActivationType.Synergist
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Leg Press"), MuscleGroupId = GetMusId("Inner Thigh"), ImpactMultiplier = 0.3,
                ActivationType = ActivationType.Synergist
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Leg Extension"), MuscleGroupId = GetMusId("Quads"), ImpactMultiplier = 1.0,
                ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Leg Curl"), MuscleGroupId = GetMusId("Hamstrings"), ImpactMultiplier = 1.0,
                ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Leg Curl"), MuscleGroupId = GetMusId("Calves"), ImpactMultiplier = 0.2,
                ActivationType = ActivationType.Synergist
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Rear Delt Fly", Exercise.EquipmentType.Machine),
                MuscleGroupId = GetMusId("Rear Delts"),
                ImpactMultiplier = 1.0, ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Rear Delt Fly", Exercise.EquipmentType.Machine),
                MuscleGroupId = GetMusId("Upper Back"),
                ImpactMultiplier = 0.4, ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Rear Delt Fly", Exercise.EquipmentType.Machine),
                MuscleGroupId = GetMusId("Rotator Cuff"),
                ImpactMultiplier = 0.6, ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Hammer Curl"), MuscleGroupId = GetMusId("Biceps"), ImpactMultiplier = 0.3,
                ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Hammer Curl"), MuscleGroupId = GetMusId("Brachialis"), ImpactMultiplier = 0.8,
                ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Hammer Curl"), MuscleGroupId = GetMusId("Forearms"), ImpactMultiplier = 0.6,
                ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Barbell Curl"), MuscleGroupId = GetMusId("Biceps"), ImpactMultiplier = 1.0,
                ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Barbell Curl"), MuscleGroupId = GetMusId("Brachialis"), ImpactMultiplier = 0.2,
                ActivationType = ActivationType.Synergist
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Barbell Curl"), MuscleGroupId = GetMusId("Forearms"), ImpactMultiplier = 0.2,
                ActivationType = ActivationType.Synergist
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Calf Raise"), MuscleGroupId = GetMusId("Calves"), ImpactMultiplier = 1.0,
                ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Row", Exercise.EquipmentType.Machine, "Seated"), MuscleGroupId = GetMusId("Lats"),
                ImpactMultiplier = 1.0,
                ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Row", Exercise.EquipmentType.Machine, "Seated"),
                MuscleGroupId = GetMusId("Upper Back"),
                ImpactMultiplier = 0.8, ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Row", Exercise.EquipmentType.Machine, "Seated"),
                MuscleGroupId = GetMusId("Biceps"), ImpactMultiplier = 0.5,
                ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Row", Exercise.EquipmentType.Machine, "Seated"),
                MuscleGroupId = GetMusId("Rear Delts"),
                ImpactMultiplier = 0.5, ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Row", Exercise.EquipmentType.Machine, "Seated"),
                MuscleGroupId = GetMusId("Forearms"),
                ImpactMultiplier = 0.5, ActivationType = ActivationType.Synergist
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Row", Exercise.EquipmentType.Machine, "Seated"),
                MuscleGroupId = GetMusId("Brachialis"),
                ImpactMultiplier = 0.15, ActivationType = ActivationType.Synergist
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Row", Exercise.EquipmentType.Cable, "One Arm"), MuscleGroupId = GetMusId("Lats"),
                ImpactMultiplier = 1.0,
                ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Row", Exercise.EquipmentType.Cable, "One Arm"),
                MuscleGroupId = GetMusId("Biceps"), ImpactMultiplier = 0.5,
                ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Row", Exercise.EquipmentType.Cable, "One Arm"),
                MuscleGroupId = GetMusId("Upper Back"),
                ImpactMultiplier = 0.8, ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Row", Exercise.EquipmentType.Cable, "One Arm"),
                MuscleGroupId = GetMusId("Rear Delts"),
                ImpactMultiplier = 0.5, ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Row", Exercise.EquipmentType.Cable, "One Arm"),
                MuscleGroupId = GetMusId("Forearms"), ImpactMultiplier = 0.5,
                ActivationType = ActivationType.Synergist
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Row", Exercise.EquipmentType.Cable, "One Arm"),
                MuscleGroupId = GetMusId("Brachialis"),
                ImpactMultiplier = 0.15, ActivationType = ActivationType.Synergist
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Lateral Raise", Exercise.EquipmentType.Cable),
                MuscleGroupId = GetMusId("Side Delts"),
                ImpactMultiplier = 1.0, ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Lateral Raise", Exercise.EquipmentType.Cable),
                MuscleGroupId = GetMusId("Upper Traps"),
                ImpactMultiplier = 0.5, ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Russian Twist"), MuscleGroupId = GetMusId("Obliques"), ImpactMultiplier = 1.0,
                ActivationType = ActivationType.Primary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Russian Twist"), MuscleGroupId = GetMusId("Upper Abs"), ImpactMultiplier = 0.4,
                ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Russian Twist"), MuscleGroupId = GetMusId("Lower Abs"), ImpactMultiplier = 0.4,
                ActivationType = ActivationType.Secondary
            },
            new ExerciseMuscleMap
            {
                ExerciseId = GetExId("Hip Abductor Machine"), MuscleGroupId = GetMusId("Outer Thigh"),
                ImpactMultiplier = 1.0, ActivationType = ActivationType.Primary
            }
        };

        var existingMappings = await _connection.Table<ExerciseMuscleMap>().ToListAsync();
        var newMappings = mockMappings.Where(m =>
            m.ExerciseId != 0 &&
            m.MuscleGroupId != 0 &&
            !existingMappings.Any(em => em.ExerciseId == m.ExerciseId && em.MuscleGroupId == m.MuscleGroupId)
        ).ToList();

        if (newMappings.Any())
        {
            await _connection.InsertAllAsync(newMappings);
        }

        var program = new WorkoutProgram
        {
            Name = "W's Upper Lower",
            Level = "Advanced",
            Goal = "Hypertrophy",
            TargetMuscles = "Full Body",
            Environment = "Gym",
            Week = 14,
            Cycle = 1,
            LastWeekUpdateDate = DateTime.Now
        };
        await _connection.InsertAsync(program);

        // Workout 1
        var w1 = new Workout { WorkoutProgramId = program.Id, Name = "Workout 1", Order = 1 };
        await _connection.InsertAsync(w1);
        await _connection.InsertAllAsync(new[]
        {
            new WorkoutItem
                { WorkoutId = w1.Id, ExerciseId = GetExId("Pull Up"), Sets = 2, RepsRange = "Failure", Order = 1 },
            new WorkoutItem
            {
                WorkoutId = w1.Id, ExerciseId = GetExId("Lat Pull Over", Exercise.EquipmentType.Cable), Sets = 2,
                RepsRange = "Failure",
                Order = 2
            },
            new WorkoutItem
            {
                WorkoutId = w1.Id, ExerciseId = GetExId("Bench Press", Exercise.EquipmentType.Machine, "Smith"),
                Sets = 2, RepsRange = "Failure",
                Order = 3
            },
            new WorkoutItem
            {
                WorkoutId = w1.Id, ExerciseId = GetExId("Chest Fly", Exercise.EquipmentType.Machine), Sets = 2,
                RepsRange = "Failure", Order = 4
            },
            new WorkoutItem
            {
                WorkoutId = w1.Id, ExerciseId = GetExId("Shoulder Press", Exercise.EquipmentType.Machine), Sets = 2,
                RepsRange = "Failure", Order = 5
            },
            new WorkoutItem
            {
                WorkoutId = w1.Id, ExerciseId = GetExId("Lateral Raise"), Sets = 2, RepsRange = "Failure", Order = 6
            },
            new WorkoutItem
            {
                WorkoutId = w1.Id, ExerciseId = GetExId("Triceps Pushdown"), Sets = 2, RepsRange = "Failure", Order = 7
            },
            new WorkoutItem
            {
                WorkoutId = w1.Id, ExerciseId = GetExId("Triceps Kickback"), Sets = 2, RepsRange = "Failure", Order = 8
            },
            new WorkoutItem
                { WorkoutId = w1.Id, ExerciseId = GetExId("Ab Crunch"), Sets = 2, RepsRange = "Failure", Order = 9 },
            new WorkoutItem
                { WorkoutId = w1.Id, ExerciseId = GetExId("Leg Raise"), Sets = 2, RepsRange = "Failure", Order = 10 }
        });

        // Workout 2
        var w2 = new Workout { WorkoutProgramId = program.Id, Name = "Workout 2", Order = 2 };
        await _connection.InsertAsync(w2);
        await _connection.InsertAllAsync(new[]
        {
            new WorkoutItem
                { WorkoutId = w2.Id, ExerciseId = GetExId("Leg Press"), Sets = 2, RepsRange = "Failure", Order = 1 },
            new WorkoutItem
            {
                WorkoutId = w2.Id, ExerciseId = GetExId("Leg Extension"), Sets = 2, RepsRange = "Failure", Order = 2
            },
            new WorkoutItem
                { WorkoutId = w2.Id, ExerciseId = GetExId("Leg Curl"), Sets = 2, RepsRange = "Failure", Order = 3 },
            new WorkoutItem
            {
                WorkoutId = w2.Id, ExerciseId = GetExId("Lateral Raise"), Sets = 2, RepsRange = "Failure", Order = 4
            },
            new WorkoutItem
            {
                WorkoutId = w2.Id, ExerciseId = GetExId("Rear Delt Fly", Exercise.EquipmentType.Machine), Sets = 2,
                RepsRange = "Failure",
                Order = 5
            },
            new WorkoutItem
                { WorkoutId = w2.Id, ExerciseId = GetExId("Hammer Curl"), Sets = 2, RepsRange = "Failure", Order = 6 },
            new WorkoutItem
                { WorkoutId = w2.Id, ExerciseId = GetExId("Barbell Curl"), Sets = 2, RepsRange = "Failure", Order = 7 },
            new WorkoutItem
                { WorkoutId = w2.Id, ExerciseId = GetExId("Calf Raise"), Sets = 2, RepsRange = "Failure", Order = 8 },
            new WorkoutItem
                { WorkoutId = w2.Id, ExerciseId = GetExId("Ab Crunch"), Sets = 2, RepsRange = "Failure", Order = 9 },
            new WorkoutItem
                { WorkoutId = w2.Id, ExerciseId = GetExId("Leg Raise"), Sets = 2, RepsRange = "Failure", Order = 10 }
        });

        // Workout 3
        var w3 = new Workout { WorkoutProgramId = program.Id, Name = "Workout 3", Order = 3 };
        await _connection.InsertAsync(w3);
        await _connection.InsertAllAsync(new[]
        {
            new WorkoutItem
            {
                WorkoutId = w3.Id, ExerciseId = GetExId("Bench Press", Exercise.EquipmentType.Machine, "Smith"),
                Sets = 2, RepsRange = "Failure",
                Order = 1
            },
            new WorkoutItem
            {
                WorkoutId = w3.Id, ExerciseId = GetExId("Chest Fly", Exercise.EquipmentType.Machine), Sets = 2,
                RepsRange = "Failure", Order = 2
            },
            new WorkoutItem
                { WorkoutId = w3.Id, ExerciseId = GetExId("Pull Up"), Sets = 2, RepsRange = "Failure", Order = 3 },
            new WorkoutItem
            {
                WorkoutId = w3.Id, ExerciseId = GetExId("Row", Exercise.EquipmentType.Machine, "Seated"), Sets = 2,
                RepsRange = "Failure",
                Order = 4
            },
            new WorkoutItem
            {
                WorkoutId = w3.Id, ExerciseId = GetExId("Row", Exercise.EquipmentType.Cable, "One Arm"), Sets = 2,
                RepsRange = "Failure", Order = 5
            },
            new WorkoutItem
            {
                WorkoutId = w3.Id, ExerciseId = GetExId("Lateral Raise"), Sets = 2, RepsRange = "Failure", Order = 6
            },
            new WorkoutItem
            {
                WorkoutId = w3.Id, ExerciseId = GetExId("Lateral Raise", Exercise.EquipmentType.Cable), Sets = 2,
                RepsRange = "Failure",
                Order = 7
            },
            new WorkoutItem
            {
                WorkoutId = w3.Id, ExerciseId = GetExId("Triceps Pushdown"), Sets = 2, RepsRange = "Failure", Order = 8
            },
            new WorkoutItem
                { WorkoutId = w3.Id, ExerciseId = GetExId("Ab Crunch"), Sets = 2, RepsRange = "Failure", Order = 9 },
            new WorkoutItem
                { WorkoutId = w3.Id, ExerciseId = GetExId("Leg Raise"), Sets = 2, RepsRange = "Failure", Order = 10 }
        });

        await SeedMockWorkoutLogsAsync();
    }

    public async Task<int> SeedMockWorkoutLogsAsync()
    {
        await Init();

        var dbWorkouts = await _connection!.Table<Workout>().ToListAsync();
        var validWorkoutIds = dbWorkouts.Select(w => w.Id).ToList();

        var program = await _connection.Table<WorkoutProgram>().Where(p => p.Name == "W's Upper Lower")
            .FirstOrDefaultAsync();
        List<int> mockWorkoutIds = new List<int>();
        if (program != null)
        {
            mockWorkoutIds = dbWorkouts.Where(w => w.WorkoutProgramId == program.Id).Select(w => w.Id).ToList();
        }

        var allLogs = await _connection.Table<WorkoutLog>().ToListAsync();

        var logsToDelete = allLogs.Where(l =>
                !validWorkoutIds.Contains(l.WorkoutId) || // Orphaned
                mockWorkoutIds.Contains(l.WorkoutId) // Belongs to mock program
        ).ToList();

        if (logsToDelete.Any())
        {
            foreach (var l in logsToDelete)
            {
                await _connection.DeleteAsync(l);
            }
        }

        if (program == null || !mockWorkoutIds.Any()) return 0;

        var workouts = dbWorkouts.Where(w => mockWorkoutIds.Contains(w.Id)).OrderBy(w => w.Order).ToList();
        var dbExercises = await _connection.Table<Exercise>().ToListAsync();

        var workoutItems = new Dictionary<int, List<WorkoutItem>>();
        foreach (var w in workouts)
        {
            var items = await _connection.Table<WorkoutItem>().Where(i => i.WorkoutId == w.Id).OrderBy(i => i.Order)
                .ToListAsync();
            workoutItems[w.Id] = items;
        }

        var logs = new List<WorkoutLog>();
        var r = new Random();
        var startDate = DateTime.Now.Date.AddDays(-90);
        int dayIndex = 0;

        double GetProgressiveWeight(int dayIdx, double startWeight)
        {
            double weeksPassed = dayIdx / 7.0;
            return Math.Round(startWeight + (weeksPassed * 1.5) + (r.NextDouble() * 2 - 1), 1);
        }

        int GetProgressiveReps(int dayIdx, int baseReps)
        {
            return baseReps + r.Next(0, 3);
        }

        while (dayIndex <= 90)
        {
            int weekDay = dayIndex % 7;
            int? workoutIdx = null;

            if (workouts.Count >= 3)
            {
                if (weekDay == 0) workoutIdx = 0;
                else if (weekDay == 2) workoutIdx = 1;
                else if (weekDay == 4) workoutIdx = 2;
            }
            else if (workouts.Count > 0)
            {
                workoutIdx = weekDay % workouts.Count;
                if (weekDay == 1 || weekDay == 3 || weekDay == 5 || weekDay == 6) workoutIdx = null;
            }

            if (workoutIdx.HasValue && workoutIdx.Value < workouts.Count)
            {
                var currentWorkout = workouts[workoutIdx.Value];
                var currentItems = workoutItems[currentWorkout.Id];

                var wDate = startDate.AddDays(dayIndex).AddHours(18).AddMinutes(r.Next(-15, 16));
                int timeOffsetMins = 0;

                foreach (var item in currentItems)
                {
                    var ex = dbExercises.FirstOrDefault(e => e.Id == item.ExerciseId);
                    if (ex == null) continue;

                    var exName = ex.Name;

                    double baseWeight = 50;
                    if (exName.Contains("Press")) baseWeight = 60;
                    if (exName.Contains("Fly") || exName.Contains("Raise")) baseWeight = 20;
                    if (exName.Contains("Curl") || exName.Contains("Pushdown") || exName.Contains("Kickback"))
                        baseWeight = 15;
                    if (exName.Contains("Leg Press")) baseWeight = 120;
                    if (exName.Contains("Leg Extension") || exName.Contains("Leg Curl")) baseWeight = 45;
                    if (exName.Contains("Pull Up") || exName.Contains("Crunch") || exName.Contains("Leg Raise") ||
                        exName.Contains("Plank") || exName.Contains("Dips") ||
                        exName.Contains("Chin Up")) baseWeight = 0;
                    if (exName.Contains("Pull Over")) baseWeight = 30;

                    for (int s = 1; s <= item.Sets; s++)
                    {
                        logs.Add(new WorkoutLog
                        {
                            WorkoutId = currentWorkout.Id,
                            ExerciseId = item.ExerciseId,
                            ExerciseNameSnapshot = exName,
                            Date = wDate.AddMinutes(timeOffsetMins),
                            SetNumber = s,
                            Weight = baseWeight > 0 ? GetProgressiveWeight(dayIndex, baseWeight) : 0,
                            Reps = GetProgressiveReps(dayIndex, r.Next(8, 12)),
                            RIR = 0,
                            FormRating = r.Next(3, 6),
                            Note = "",
                            IsCompleted = true,
                            IsSaved = true,
                            Week = (dayIndex / 7) + 1,
                            Cycle = 1
                        });
                        timeOffsetMins += r.Next(2, 4);
                    }

                    timeOffsetMins += r.Next(3, 6);
                }
            }

            dayIndex++;
        }

        await _connection.InsertAllAsync(logs);
        NotifyDatabaseChanged();

        return logs.Count;
    }

    private async Task SeedAgirsaglam5x5ProgramAsync()
    {
        var dbExercises = await _connection!.Table<Exercise>().ToListAsync();
        int GetExId(string name) => dbExercises.FirstOrDefault(e => e.Name == name)?.Id ?? 0;

        var program = new WorkoutProgram
        {
            Name = "Ağırsağlam's 5x5",
            Level = "Intermediate",
            Goal = "Strength",
            TargetMuscles = "Full Body",
            Environment = "Gym",
            Week = 0,
            LastWeekUpdateDate = DateTime.Now
        };
        await _connection.InsertAsync(program);

        // Day 1
        var w1 = new Workout { WorkoutProgramId = program.Id, Name = "Day 1", Order = 1 };
        await _connection.InsertAsync(w1);
        await _connection.InsertAllAsync(new[]
        {
            new WorkoutItem { WorkoutId = w1.Id, ExerciseId = GetExId("Squat"), Sets = 5, RepsRange = "5", Order = 1 },
            new WorkoutItem
                { WorkoutId = w1.Id, ExerciseId = GetExId("Bench Press"), Sets = 5, RepsRange = "5", Order = 2 },
            new WorkoutItem { WorkoutId = w1.Id, ExerciseId = GetExId("Pull Up"), Sets = 5, RepsRange = "5", Order = 3 }
        });

        // Day 2
        var w2 = new Workout { WorkoutProgramId = program.Id, Name = "Day 2", Order = 2 };
        await _connection.InsertAsync(w2);
        await _connection.InsertAllAsync(new[]
        {
            new WorkoutItem { WorkoutId = w2.Id, ExerciseId = GetExId("Squat"), Sets = 5, RepsRange = "5", Order = 1 },
            new WorkoutItem
                { WorkoutId = w2.Id, ExerciseId = GetExId("Overhead Press"), Sets = 5, RepsRange = "5", Order = 2 },
            new WorkoutItem
                { WorkoutId = w2.Id, ExerciseId = GetExId("Deadlift"), Sets = 1, RepsRange = "5", Order = 3 },
            new WorkoutItem { WorkoutId = w2.Id, ExerciseId = GetExId("Dips"), Sets = 5, RepsRange = "5", Order = 4 }
        });

        // Day 3
        var w3 = new Workout { WorkoutProgramId = program.Id, Name = "Day 3", Order = 3 };
        await _connection.InsertAsync(w3);
        await _connection.InsertAllAsync(new[]
        {
            new WorkoutItem { WorkoutId = w3.Id, ExerciseId = GetExId("Squat"), Sets = 5, RepsRange = "5", Order = 1 },
            new WorkoutItem
                { WorkoutId = w3.Id, ExerciseId = GetExId("Bench Press"), Sets = 5, RepsRange = "5", Order = 2 },
            new WorkoutItem
                { WorkoutId = w3.Id, ExerciseId = GetExId("Barbell Row"), Sets = 5, RepsRange = "5", Order = 3 }
        });
    }
}

