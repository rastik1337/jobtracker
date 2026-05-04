using System;
using System.Collections.Generic;
using System.IO;
using LiteDB;
using JobTracker.Models;

namespace JobTracker.Data;

public class JobTrackerRepository : IDisposable
{
    private readonly LiteDatabase _db;
    private readonly ILiteCollection<Project> _projects;
    private readonly ILiteCollection<Label> _labels;
    private readonly ILiteCollection<TimeRecord> _timeRecords;

    public JobTrackerRepository()
    {
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var path = Path.Combine(folder, "JobTracker", "jobtracker.db");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        _db = new LiteDatabase(path);

        _projects = _db.GetCollection<Project>("projects");
        _labels = _db.GetCollection<Label>("labels");
        _timeRecords = _db.GetCollection<TimeRecord>("time_records");

        _projects.EnsureIndex(x => x.Name, true);
        _labels.EnsureIndex(x => x.Name, true);

        _timeRecords.EnsureIndex(x => x.ProjectId);
        _timeRecords.EnsureIndex(x => x.LabelId);
    }

    public TimeRecord? GetActiveTimeRecord()
    {
        return _timeRecords.FindOne(X => X.TimeEnd == null);
    }

    public TimeRecord StartTimeRecord(int projectId, int labelId)
    {
        var active = GetActiveTimeRecord();
        if (active != null)
        {
            throw new InvalidOperationException("A time record is already active. Please stop or discard it first.");
        }

        var newRecord = new TimeRecord
        {
            ProjectId = projectId,
            LabelId = labelId,
            TimeStart = DateTime.Now
        };

        _timeRecords.Insert(newRecord);
        return newRecord;
    }

    public void StopActiveTimeRecord(DateTime endTime, string? note = null)
    {
        var active = GetActiveTimeRecord();
        if (active != null)
        {
            active.TimeEnd = endTime;
            active.Note = note;
            _timeRecords.Update(active);
        }
    }

    public void DiscardActiveTimeRecord()
    {
        var active = GetActiveTimeRecord();
        if (active != null)
        {
            _timeRecords.Delete(active.Id);
        }
    }

    public IEnumerable<Project> GetAllProjects() => _projects.FindAll();
    public Project InsertProject(Project project) { _projects.Insert(project); return project; }
    public IEnumerable<TimeRecord> GetRecordsForProject(int projectId)
    {
        return _timeRecords.Find(x => x.ProjectId == projectId);
    }

    public IEnumerable<Label> GetAllLabels() => _labels.FindAll();
    public Label InsertLabel(Label label) { _labels.Insert(label); return label; }

    public IEnumerable<TimeRecord> GetAllTimeRecords() => _timeRecords.FindAll();
    public void DeleteTimeRecord(int id) => _timeRecords.Delete(id);
    public TimeRecord? GetLastRecord()
    {
        return _timeRecords.Query()
            .OrderByDescending(x => x.TimeStart)
            .FirstOrDefault();
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }
}