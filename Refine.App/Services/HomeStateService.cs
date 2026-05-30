using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Refine.App.Models;

namespace Refine.App.Services
{
    public class HomeStateService
    {
        private readonly LocalDbService _dbService;
        private readonly AnalyticsService _analyticsService;

        public bool IsLoaded { get; private set; } = false;

        public User? User { get; private set; }
        public List<WorkoutProgram>? Programs { get; private set; }

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

        public event Action? OnStateChanged;

        private bool _isLoadingData = false;

        public HomeStateService(LocalDbService dbService, AnalyticsService analyticsService)
        {
            _dbService = dbService;
            _analyticsService = analyticsService;
        }

        public async Task LoadDataAsync(bool forceRefresh = false)
        {
            if (_isLoadingData) return;
            _isLoadingData = true;

            try
            {
                User = await _dbService.GetUserAsync();
                Programs = await _dbService.GetAllProgramsAsync();

                var allLogs = await _dbService.GetAllLogsAsync();

                await LoadTopExerciseAsync(allLogs);
                await LoadStreakAsync(allLogs);
                await LoadUpNextWorkoutAsync();

                IsLoaded = true;
                NotifyStateChanged();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"HATA: {ex.Message}");
            }
            finally
            {
                _isLoadingData = false;
            }
        }

        private async Task LoadTopExerciseAsync(List<WorkoutLog> logs)
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

        private async Task LoadStreakAsync(List<WorkoutLog> allLogs)
        {
            var program = await _dbService.GetSelectedWorkoutProgramAsync();
            if (program != null && program.Workouts != null)
            {
                var workoutIds = program.Workouts.Select(w => w.Id).ToList();
                var programLogs = allLogs.Where(l => workoutIds.Contains(l.WorkoutId)).ToList();
                
                if (programLogs.Any())
                {
                    var startDate = programLogs.Min(l => l.Date).Date;
                    var today = DateTime.Now.Date;
                    StreakTotalDays = (today - startDate).Days + 1;
                    StreakCompletedDays = programLogs.Select(l => l.Date.Date).Distinct().Count();
                }
            }
        }

        private async Task LoadUpNextWorkoutAsync()
        {
            var program = await _dbService.GetSelectedWorkoutProgramAsync();
            if (program != null && program.Workouts != null && program.Workouts.Any())
            {
                var orderedWorkouts = program.Workouts.OrderBy(w => w.Order).ToList();
                var completedWorkouts = new Dictionary<int, bool>();
                
                foreach(var w in orderedWorkouts)
                {
                    completedWorkouts[w.Id] = await _dbService.HasLogForWeekAsync(w.Id, false);
                }
                
                var completedList = orderedWorkouts.Where(w => completedWorkouts.TryGetValue(w.Id, out bool comp) && comp).ToList();
                
                if (!completedList.Any())
                {
                    UpNextWorkout = orderedWorkouts.FirstOrDefault();
                }
                else
                {
                    var highestCompletedOrder = completedList.Max(w => w.Order);
                    UpNextWorkout = orderedWorkouts.FirstOrDefault(w => w.Order > highestCompletedOrder);
                    
                    if (UpNextWorkout == null)
                    {
                        UpNextWorkout = orderedWorkouts.FirstOrDefault();
                    }
                }

                if (UpNextWorkout != null && UpNextWorkout.Items != null)
                {
                    UpNextDuration = (UpNextWorkout.Items.Sum(i => i.Sets)) * 4;
                    var targetMuscles = UpNextWorkout.Items
                        .Where(i => !string.IsNullOrWhiteSpace(i.Exercise?.PrimaryMuscleCategory) && i.Exercise.PrimaryMuscleCategory != "Genel")
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
                        UpNextMuscleGroups = "Full Body";
                    }
                }
            }
        }

        private void NotifyStateChanged() => OnStateChanged?.Invoke();
    }
}
