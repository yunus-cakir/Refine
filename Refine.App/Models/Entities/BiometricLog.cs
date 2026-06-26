using SQLite;
using SQLiteNetExtensions.Attributes;
using System;

namespace Refine.App.Models.Entities;

public class BiometricLog
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [ForeignKey(typeof(User))]
    public int UserId { get; set; }

    public DateTime Date { get; set; } = DateTime.Now;

    public double? Weight { get; set; }

    // Opsiyonel Vücut Ölçüleri (cm)
    public double? Neck { get; set; }
    public double? Shoulder { get; set; }
    public double? Waist { get; set; }
    public double? Hip { get; set; }
}
