using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Refine.App.Models;

namespace Refine.App.Services
{
    public class SubMuscleFatigueInfo
    {
        public string MuscleName { get; set; } = "";
        public decimal FatiguePercentage { get; set; }
    }

    public class MuscleFatigueInfo
    {
        public string MuscleCategory { get; set; } = "";
        public decimal FatiguePercentage { get; set; }
        public List<SubMuscleFatigueInfo> SubMuscles { get; set; } = new();
    }

    public class RecoveryState
    {
        public int OverallRecoveryScore { get; set; } // 0-100
        public decimal CurrentCnsLoad { get; set; }
        public int EstimatedRecoveryHours { get; set; }
        public decimal CnsThreshold { get; set; }
        public List<MuscleFatigueInfo> MuscleFatigues { get; set; } = new();
        public List<ChartDataPoint> CnsHistory { get; set; } = new();
    }

    public class RecoveryService
    {
        private readonly LocalDbService _dbService;

        public RecoveryService(LocalDbService dbService)
        {
            _dbService = dbService;
        }

        public async Task<RecoveryState> CalculateRecoveryAsync()
        {
            var user = await _dbService.GetUserAsync();
            var cnsThreshold = user?.WorkoutSettings?.CnsThreshold ?? 150m;
            decimal cnsDecayPerHour = cnsThreshold / 72m; // Linear decay: full threshold recovers in 72 hours

            // Fetch logs from the last 7 days for rolling volume
            var cutoffDate = DateTime.Now.AddDays(-7);
            var allLogs = await _dbService.GetAllLogsAsync();
            var recentLogs = allLogs.Where(l => l.Date >= cutoffDate && l.IsSaved).OrderBy(l => l.Date).ToList();
            var exercises = await _dbService.GetExercisesAsync();

            var state = new RecoveryState();
            state.CnsThreshold = cnsThreshold;

            if (!recentLogs.Any())
            {
                state.OverallRecoveryScore = 100;
                return state;
            }

            decimal currentCnsLoad = 0;
            DateTime lastTime = recentLogs.First().Date;
            Dictionary<DateTime, decimal> dailyCnsMap = new();

            foreach (var log in recentLogs)
            {
                var hoursDiff = (decimal)(log.Date - lastTime).TotalHours;
                if (hoursDiff > 0)
                {
                    // Decay CNS
                    currentCnsLoad = Math.Max(0, currentCnsLoad - (cnsDecayPerHour * hoursDiff));
                }

                // Add Load
                var exercise = exercises.FirstOrDefault(e => e.Id == log.ExerciseId);
                if (exercise != null)
                {
                    // CNS Load
                    decimal cnsScore =
                        exercise.CnsFatigueScore > 0 ? exercise.CnsFatigueScore : 3.0m; // Default to 3 if not set
                    decimal intensityFactor = 1.0m;
                    if (log.RIR.HasValue)
                    {
                        // RIR 0 -> 1.2x, RIR 1 -> 1.1x, RIR 2 -> 1.0x, RIR 3 -> 0.9x
                        intensityFactor = 1.0m + ((2.0m - log.RIR.Value) * 0.1m);
                        intensityFactor = Math.Max(0.5m, Math.Min(1.5m, intensityFactor));
                    }

                    currentCnsLoad += cnsScore * intensityFactor;
                }

                lastTime = log.Date;
                dailyCnsMap[log.Date.Date] = currentCnsLoad;
            }

            // Apply decay up to NOW for CNS
            var finalHoursDiff = (decimal)(DateTime.Now - lastTime).TotalHours;
            if (finalHoursDiff > 0)
            {
                currentCnsLoad = Math.Max(0, currentCnsLoad - (cnsDecayPerHour * finalHoursDiff));
            }

            // Final calculations
            state.CurrentCnsLoad = currentCnsLoad;
            state.OverallRecoveryScore = (int)Math.Max(0, Math.Min(100, 100 - (currentCnsLoad / cnsThreshold * 100)));

            if (currentCnsLoad > 0)
            {
                state.EstimatedRecoveryHours = (int)Math.Ceiling(currentCnsLoad / cnsDecayPerHour);
            }

            state.MuscleFatigues = CalculateMuscleFatigue(recentLogs, exercises);

            // Populate 7-day history for the chart
            var historyStart = DateTime.Now.Date.AddDays(-6);
            for (int i = 0; i < 7; i++)
            {
                var d = historyStart.AddDays(i);
                decimal val = dailyCnsMap.ContainsKey(d) ? dailyCnsMap[d] : 0;

                int? latestWkId = null;
                long? latestWkTicks = null;
                var logsOnDay = recentLogs.Where(l => l.Date.Date == d).OrderByDescending(l => l.Date).ToList();
                if (logsOnDay.Any())
                {
                    var latestLog = logsOnDay.First();
                    latestWkId = latestLog.WorkoutId;
                    latestWkTicks = latestLog.Date.Ticks;
                }

                state.CnsHistory.Add(new ChartDataPoint 
                { 
                    Date = d, 
                    Value = (double)val,
                    LatestWorkoutId = latestWkId,
                    LatestWorkoutTicks = latestWkTicks
                });
            }

            return state;
        }

        public List<MuscleFatigueInfo> CalculateMuscleFatigue(IEnumerable<WorkoutLog> logs, IEnumerable<Exercise> exercises, bool isSingleSession = false)
        {
            var mrvMap = new Dictionary<string, decimal>
            {
                { "Pectoralis Major", 22m }, { "Upper Chest", 20m },
                { "Lats", 25m }, { "Rhomboids", 25m },
                { "Front Delt", 15m }, { "Side Delt", 26m }, { "Rear Delt", 24m },
                { "Biceps", 26m }, { "Triceps", 24m },
                { "Quads", 20m }, { "Hamstrings", 18m }, { "Glutes", 20m },
                { "Abs", 25m }
            };

            Dictionary<string, string> subToCategoryMap = new();
            foreach (var ex in exercises)
            {
                if (ex.MuscleMaps != null)
                {
                    foreach (var map in ex.MuscleMaps)
                    {
                        if (map.MuscleGroup != null && !string.IsNullOrEmpty(map.MuscleGroup.Name))
                        {
                            subToCategoryMap[map.MuscleGroup.Name] = map.MuscleGroup.Category;
                        }
                    }
                }
            }

            Dictionary<string, decimal> subMuscleFatigues = new();

            foreach (var log in logs)
            {
                var exercise = exercises.FirstOrDefault(e => e.Id == log.ExerciseId);
                if (exercise != null && exercise.MuscleMaps != null)
                {
                    decimal reps = log.Reps ?? 10m;
                    decimal rirMultiplier = 1.0m;
                    if (log.RIR.HasValue)
                    {
                        rirMultiplier = log.RIR.Value switch
                        {
                            0 => 1.5m,
                            1 => 1.25m,
                            2 => 1.0m,
                            3 => 0.8m,
                            _ => 0.5m
                        };
                    }

                    foreach (var map in exercise.MuscleMaps)
                    {
                        var subName = map.MuscleGroup?.Name;
                        if (string.IsNullOrEmpty(subName)) continue;

                        decimal volumeAdded = (reps / 10.0m) * rirMultiplier * (decimal)map.ImpactMultiplier;

                        if (!subMuscleFatigues.ContainsKey(subName))
                            subMuscleFatigues[subName] = 0;

                        subMuscleFatigues[subName] += volumeAdded;
                    }
                }
            }

            var generatedSubMuscles = subMuscleFatigues
                .Select(sub => 
                {
                    decimal weeklyMrv = mrvMap.ContainsKey(sub.Key) ? mrvMap[sub.Key] : 20m;
                    
                    // If evaluating a single session, the optimal impact is much lower than the full 7-day budget.
                    // Scale it to roughly 45% of the weekly MRV, capped around 12 sets to represent maximum session capacity.
                    decimal divisorMrv = isSingleSession ? Math.Min(12m, weeklyMrv * 0.45m) : weeklyMrv;
                    
                    return new SubMuscleFatigueInfo
                    {
                        MuscleName = sub.Key,
                        FatiguePercentage = (sub.Value / divisorMrv) * 100m
                    };
                }).ToList();

            var groupedByCategory = generatedSubMuscles
                .Where(sub => subToCategoryMap.ContainsKey(sub.MuscleName) && sub.FatiguePercentage > 0)
                .GroupBy(sub => subToCategoryMap[sub.MuscleName]);

            return groupedByCategory
                .Select(g => new MuscleFatigueInfo
                {
                    MuscleCategory = g.Key,
                    FatiguePercentage = g.Max(sub => sub.FatiguePercentage),
                    SubMuscles = g.OrderByDescending(sub => sub.FatiguePercentage).ToList()
                })
                .OrderByDescending(m => m.FatiguePercentage)
                .ToList();
        }
    }
}
