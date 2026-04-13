using System;
using LiteDB;

namespace JobTracker.Models;

public class TimeRecord
{
    public int Id { get; set; }
    public DateTime TimeStart { get; set; }
    public DateTime? TimeEnd { get; set; }
    public string? Note { get; set; }
    public int ProjectId { get; set; }
    public int LabelId { get; set; }

    [BsonIgnore]
    public TimeSpan Duration => TimeEnd.HasValue
        ? TimeEnd.Value - TimeStart
        : DateTime.Now - TimeStart;
}