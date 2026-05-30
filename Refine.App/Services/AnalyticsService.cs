using System;
using System.Collections.Generic;
using System.Linq;
using Refine.App.Models;

namespace Refine.App.Services;

public class ChartDataPoint
{
    public DateTime Date { get; set; }
    public double Value { get; set; }
}

public class AnalyticsService
{
    public List<ChartDataPoint> ProcessLogs(List<WorkoutLog> cachedLogs, string timeframe, string metric)
    {
        if (cachedLogs == null || !cachedLogs.Any())
            return new List<ChartDataPoint>();

        var cutoffDate = DateTime.MinValue;
        if (timeframe == "14D") cutoffDate = DateTime.Now.Date.AddDays(-14);
        else if (timeframe == "30D") cutoffDate = DateTime.Now.Date.AddDays(-30);
        else if (timeframe == "90D") cutoffDate = DateTime.Now.Date.AddDays(-90);

        var filteredLogs = cachedLogs.Where(l => l.Date.Date >= cutoffDate).ToList();
        
        if (!filteredLogs.Any())
            return new List<ChartDataPoint>();

        // Group by Date (ignoring time) to get a single point per session day
        var groupedLogs = filteredLogs.GroupBy(l => l.Date.Date)
                                      .OrderBy(g => g.Key);

        var dataPoints = new List<ChartDataPoint>();

        foreach (var group in groupedLogs)
        {
            double metricValue = 0;

            if (metric == "1RM")
            {
                // Epley Formula: Weight * (1 + Reps / 30) for each set, take the maximum across the day
                metricValue = group.Max(l => (l.Weight ?? 0) * (1 + (l.Reps ?? 0) / 30.0));
            }
            else if (metric == "MaxWeight")
            {
                // Max Weight lifted that day
                metricValue = group.Max(l => l.Weight ?? 0);
            }
            else if (metric == "ProgressiveOverload")
            {
                // Progressive Overload: Effort-Adjusted Volume
                double scoreSum = 0;
                foreach (var log in group)
                {
                    double weight = log.Weight ?? 0;
                    int reps = log.Reps ?? 0;
                    int rir = log.RIR ?? 4; // Default to light set if RIR is missing
                    
                    double effortMultiplier = rir switch
                    {
                        0 or 1 => 1.2,
                        2 or 3 => 1.0,
                        _ => 0.7
                    };
                    
                    scoreSum += weight * reps * effortMultiplier;
                }
                metricValue = scoreSum;
            }

            dataPoints.Add(new ChartDataPoint
            {
                Date = group.Key,
                Value = Math.Round(metricValue, 2)
            });
        }

        return dataPoints;
    }
}
