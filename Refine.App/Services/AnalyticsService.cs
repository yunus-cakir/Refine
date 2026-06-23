using System;
using System.Collections.Generic;
using System.Linq;
using Refine.App.Models;

namespace Refine.App.Services;

public class ChartDataPoint
{
    public DateTime Date { get; set; }
    public double Value { get; set; }
    public int? LatestWorkoutId { get; set; }
    public long? LatestWorkoutTicks { get; set; }
}

public class AnalyticsService
{
    public List<ChartDataPoint> ProcessLogs(List<WorkoutLog> cachedLogs, string timeframe, string metric, bool isBodyweight = false)
    {
        if (cachedLogs == null || !cachedLogs.Any())
            return new List<ChartDataPoint>();

        var today = DateTime.Today;
        var cutoffDate = DateTime.MinValue;
        if (timeframe == "14D") cutoffDate = today.AddDays(-14);
        else if (timeframe == "30D") cutoffDate = today.AddDays(-30);
        else if (timeframe == "90D") cutoffDate = today.AddDays(-90);

        var filteredLogs = cachedLogs.Where(l => l.Date.Date >= cutoffDate);
        
        if (!filteredLogs.Any())
            return new List<ChartDataPoint>();

        // Strategy pattern to avoid string comparisons in the loop
        Func<IGrouping<DateTime, WorkoutLog>, double> metricStrategy;

        if (isBodyweight)
        {
            metricStrategy = metric switch
            {
                "1RM" => group => group.Max(l => l.Reps ?? 0),
                "MaxWeight" => group => group.Sum(l => l.Reps ?? 0),
                "ProgressiveOverload" => group => group.Sum(l => 
                    (l.Reps ?? 0) * ((l.RIR ?? 4) switch { 0 or 1 => 1.2, 2 or 3 => 1.0, _ => 0.7 })
                ),
                _ => group => group.Max(l => l.Reps ?? 0)
            };
        }
        else
        {
            metricStrategy = metric switch
            {
                "1RM" => group => group.Max(l => (l.Weight ?? 0) * (1 + (l.Reps ?? 0) / 30.0)),
                "MaxWeight" => group => group.Max(l => l.Weight ?? 0),
                "ProgressiveOverload" => group => group.Sum(l => 
                    (l.Weight ?? 0) * (l.Reps ?? 0) * ((l.RIR ?? 4) switch { 0 or 1 => 1.2, 2 or 3 => 1.0, _ => 0.7 })
                ),
                _ => group => 0
            };
        }

        return filteredLogs
            .GroupBy(l => l.Date.Date)
            .OrderBy(g => g.Key)
            .Select(group => new ChartDataPoint
            {
                Date = group.Key,
                Value = Math.Round(metricStrategy(group), 2)
            })
            .ToList();
    }
}
