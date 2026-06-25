using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Refine.App.Models;

namespace Refine.App.Services
{
    public class HomeStateService : IDisposable
    {
        private readonly LocalDbService _dbService;
        private readonly AnalyticsService _analyticsService;
        private readonly RecoveryService _recoveryService;

        public bool IsUserLoaded { get; private set; } = false;
        public bool IsProgramsLoaded { get; private set; } = false;
        public bool IsTopExerciseLoaded { get; private set; } = false;
        public bool IsStreakLoaded { get; private set; } = false;
        public bool IsUpNextLoaded { get; private set; } = false;
        public bool IsRecoveryLoaded { get; private set; } = false;

        public bool IsLoaded =>
            IsUserLoaded && IsProgramsLoaded && IsTopExerciseLoaded && IsStreakLoaded && IsUpNextLoaded &&
            IsRecoveryLoaded;

        public User? User { get; private set; }
        public List<WorkoutProgram>? Programs { get; private set; }
        public RecoveryState RecoveryState { get; private set; } = new();

        public Exercise? TopExercise { get; private set; }
        public List<ChartDataPoint> TopExerciseDataPoints { get; private set; } = new();
        public double TopExerciseRateOfChange { get; private set; } = 0;
        public DateTime TopExerciseLastTrained { get; private set; }

        public int StreakCompletedDays { get; private set; } = 0;
        public int StreakTotalDays { get; private set; } = 0;
        public double StreakRatio => StreakTotalDays > 0 ? (double)StreakCompletedDays / StreakTotalDays : 0;

        public Workout? UpNextWorkout { get; private set; }
        public int UpNextDuration { get; private set; } = 0;
        public string UpNextMuscleGroups { get; private set; } = "";
        public bool UpNextIsSaved { get; private set; } = false;
        public int UpNextWeek { get; private set; } = 1;
        public List<bool> UpNextWorkoutStates { get; private set; } = new();
        public int UpNextWorkoutIndex { get; private set; } = -1;

        public event Action? OnStateChanged;


        public HomeStateService(LocalDbService dbService, AnalyticsService analyticsService,
            RecoveryService recoveryService)
        {
            _dbService = dbService;
            _analyticsService = analyticsService;
            _recoveryService = recoveryService;

            _dbService.OnDatabaseChanged += HandleDatabaseChanged;
        }

        private void HandleDatabaseChanged()
        {
            _ = LoadDataAsync(showSkeleton: false);
        }

        public void Dispose()
        {
            _dbService.OnDatabaseChanged -= HandleDatabaseChanged;
        }

        private Task? _loadTask;

        public Task LoadDataAsync(bool showSkeleton = false)
        {
            if (_loadTask != null && !_loadTask.IsCompleted)
            {
                return _loadTask;
            }

            _loadTask = LoadDataInternalAsync(showSkeleton);
            return _loadTask;
        }

        private async Task LoadDataInternalAsync(bool showSkeleton)
        {
            if (showSkeleton)
            {
                IsUserLoaded = false;
                IsProgramsLoaded = false;
                IsTopExerciseLoaded = false;
                IsStreakLoaded = false;
                IsUpNextLoaded = false;
                IsRecoveryLoaded = false;
                NotifyStateChanged();
            }

            try
            {
                await LoadUserAsync();
                
                await Task.WhenAll(
                    LoadProgramsAsync(),
                    LoadUpNextWorkoutAsync(),
                    LoadAnalyticsAsync()
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"HATA LoadDataInternalAsync: {ex.Message}");
            }
        }

        private async Task LoadUserAsync()
        {
            try
            {
                User = await _dbService.GetUserAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"HATA User: {ex.Message}");
            }
            finally
            {
                IsUserLoaded = true;
                NotifyStateChanged();
            }
        }

        private async Task LoadProgramsAsync()
        {
            try
            {
                Programs = await _dbService.GetAllProgramsAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"HATA Programs: {ex.Message}");
            }
            finally
            {
                IsProgramsLoaded = true;
                NotifyStateChanged();
            }
        }

        private async Task LoadAnalyticsAsync()
        {
            try
            {
                var allLogs = await _dbService.GetAllLogsAsync();
                _ = LoadTopExerciseAsync(allLogs);
                _ = LoadStreakAsync(allLogs);
                _ = LoadRecoveryAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"HATA Analytics: {ex.Message}");
                IsTopExerciseLoaded = true;
                IsStreakLoaded = true;
                IsRecoveryLoaded = true;
                NotifyStateChanged();
            }
        }

        private async Task LoadRecoveryAsync()
        {
            try
            {
                RecoveryState = await _recoveryService.CalculateRecoveryAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"HATA Recovery: {ex.Message}");
            }
            finally
            {
                IsRecoveryLoaded = true;
                NotifyStateChanged();
            }
        }

        private async Task LoadTopExerciseAsync(List<WorkoutLog> logs)
        {
            try
            {
                var exercises = await _dbService.GetExercisesAsync();

                var recentLogsGrouped = logs
                    .GroupBy(l => l.ExerciseId)
                    .Select(g => new
                    {
                        ExerciseId = g.Key,
                        LastTrained = g.Max(l => l.Date),
                        Logs = g.ToList()
                    })
                    .ToList();

                double maxRateOfChange = double.MinValue;
                Exercise? bestExercise = null;
                List<ChartDataPoint> bestDataPoints = new();
                DateTime bestLastTrained = DateTime.MinValue;

                foreach (var group in recentLogsGrouped)
                {
                    var exercise = exercises.FirstOrDefault(e => e.Id == group.ExerciseId);
                    if (exercise == null) continue;

                    var points = _analyticsService.ProcessLogs(group.Logs, "30D", "ProgressiveOverload");
                    if (points.Count < 2 || points.All(p => p.Value <= 0)) continue;

                    var first = points.FirstOrDefault()?.Value ?? 0;
                    var last = points.LastOrDefault()?.Value ?? 0;
                    var rateOfChange = first > 0 ? (last - first) / first : 0;

                    if (rateOfChange > maxRateOfChange)
                    {
                        maxRateOfChange = rateOfChange;
                        bestExercise = exercise;
                        bestDataPoints = points;
                        bestLastTrained = group.LastTrained;
                    }
                }

                if (bestExercise != null)
                {
                    TopExercise = bestExercise;
                    TopExerciseDataPoints = bestDataPoints;
                    TopExerciseRateOfChange = maxRateOfChange;
                    TopExerciseLastTrained = bestLastTrained;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"HATA TopExercise: {ex.Message}");
            }
            finally
            {
                IsTopExerciseLoaded = true;
                NotifyStateChanged();
            }
        }

        private async Task LoadStreakAsync(List<WorkoutLog> allLogs)
        {
            try
            {
                int tempTotalDays = 0;
                int tempCompletedDays = 0;

                var program = await _dbService.GetSelectedWorkoutProgramAsync();
                if (program != null && program.Workouts != null)
                {
                    var workoutIds = program.Workouts.Select(w => w.Id).ToList();
                    var programLogs = allLogs.Where(l => workoutIds.Contains(l.WorkoutId) && l.Cycle == program.Cycle)
                        .ToList();

                    if (programLogs.Any())
                    {
                        var startDate = programLogs.Min(l => l.Date).Date;
                        var today = DateTime.Now.Date;
                        tempTotalDays = (today - startDate).Days + 1;
                        tempCompletedDays = programLogs.Select(l => l.Date.Date).Distinct().Count();
                    }
                }

                StreakTotalDays = tempTotalDays;
                StreakCompletedDays = tempCompletedDays;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"HATA Streak: {ex.Message}");
            }
            finally
            {
                IsStreakLoaded = true;
                NotifyStateChanged();
            }
        }

        private async Task LoadUpNextWorkoutAsync()
        {
            try
            {
                var program = await _dbService.GetSelectedWorkoutProgramAsync();
                var user = await _dbService.GetUserAsync();
                if (program != null && program.Workouts != null && program.Workouts.Any())
                {
                    var orderedWorkouts = program.Workouts.OrderBy(w => w.Order).ToList();

                    int searchWeek = program.Week;
                    bool found = false;

                    while (!found)
                    {
                        var completedWorkoutsForWeek = new Dictionary<int, bool>();
                        foreach (var w in orderedWorkouts)
                        {
                            bool isCompleted = await _dbService.HasLogForWeekAsync(w.Id, searchWeek, program.Cycle);
                            bool isSaved = await _dbService.HasSavedLogForWeekAsync(w.Id, searchWeek, program.Cycle);
                            completedWorkoutsForWeek[w.Id] = isCompleted || isSaved;
                        }

                        // Search for the first workout where IsSaved = false
                        var firstIncomplete = orderedWorkouts.FirstOrDefault(w => !completedWorkoutsForWeek[w.Id]);

                        if (firstIncomplete != null)
                        {
                            // Found the next actionable workout
                            UpNextWorkout = firstIncomplete;
                            UpNextWeek = searchWeek;

                            UpNextWorkoutStates = orderedWorkouts.Select(w => completedWorkoutsForWeek[w.Id]).ToList();
                            UpNextWorkoutIndex = orderedWorkouts.FindIndex(w => w.Id == firstIncomplete.Id);

                            found = true;
                        }
                        else
                        {
                            // All workouts in this week are completed. Advance to the next week.
                            searchWeek++;
                        }
                    }

                    if (UpNextWorkout != null && UpNextWorkout.Items != null)
                    {
                        UpNextDuration = (UpNextWorkout.Items.Sum(i => i.Sets)) *
                            ((user?.WorkoutSettings?.PreferredRestTime ?? 90) +
                             (user?.WorkoutSettings?.AverageSetDuration ?? 45)) / 60;
                        var targetMuscles = UpNextWorkout.Items
                            .Where(i => !string.IsNullOrWhiteSpace(i.Exercise?.PrimaryMuscleCategory) &&
                                        i.Exercise.PrimaryMuscleCategory != "General")
                            .GroupBy(i => i.Exercise!.PrimaryMuscleCategory!)
                            .OrderByDescending(g => g.Count())
                            .ThenByDescending(g => g.Sum(item => item.Sets))
                            .Select(g => g.Key)
                            .Take(2)
                            .ToList();

                        if (targetMuscles.Any())
                        {
                            UpNextMuscleGroups = string.Join(" & ", targetMuscles);
                        }
                        else
                        {
                            UpNextMuscleGroups = "GENERAL";
                        }

                        UpNextIsSaved =
                            await _dbService.HasSavedLogForWeekAsync(UpNextWorkout.Id, UpNextWeek, program.Cycle);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"HATA UpNext: {ex.Message}");
            }
            finally
            {
                IsUpNextLoaded = true;
                NotifyStateChanged();
            }
        }

        private void NotifyStateChanged() => OnStateChanged?.Invoke();
    }
}
