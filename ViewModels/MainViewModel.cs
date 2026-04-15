using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JobTracker.Data;
using JobTracker.Models;

namespace JobTracker.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly JobTrackerRepository _repository;
    private readonly DispatcherTimer _timer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTracking))]
    private TimeRecord? _activeRecord;

    [ObservableProperty]
    private TimeSpan _elapsedTime;

    public bool IsTracking => ActiveRecord != null;
    public ObservableCollection<Project> Projects { get; }
    public ObservableCollection<Label> Labels { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartTrackingCommand))]
    private string _selectedProjectName = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartTrackingCommand))]
    private string _selectedLabelName = string.Empty;

    private bool CanStartTracking =>
    !string.IsNullOrWhiteSpace(SelectedProjectName) &&
    !string.IsNullOrWhiteSpace(SelectedLabelName);

    public record ProjectSummary(string Name, TimeSpan TotalTime);

    [ObservableProperty]
    private ObservableCollection<ProjectSummary> _projectSummaries = new();

    public MainViewModel(JobTrackerRepository repository)
    {
        _repository = repository;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (s, e) => UpdateElapsedTime();

        ActiveRecord = _repository.GetActiveTimeRecord();
        Projects = new ObservableCollection<Project>(_repository.GetAllProjects());
        Labels = new ObservableCollection<Label>(_repository.GetAllLabels());
        LoadProjectSummaries();

        if (IsTracking)
        {
            _timer.Start();
        }
    }

    [RelayCommand(CanExecute = nameof(CanStartTracking))]
    public void StartTracking()
    {
        if (!CanStartTracking)
        {
            return;
        }

        var project = _repository
            .GetAllProjects()
            .FirstOrDefault(p => p.Name.Equals(SelectedProjectName, StringComparison.OrdinalIgnoreCase));

        if (project == null)
        {
            project = _repository.InsertProject(new Project { Name = SelectedProjectName });
            Projects.Add(project);
        }

        var label = _repository
            .GetAllLabels()
            .FirstOrDefault(l => l.Name.Equals(SelectedLabelName, StringComparison.OrdinalIgnoreCase));

        if (label == null)
        {
            label = _repository.InsertLabel(new Label { Name = SelectedLabelName });
            Labels.Add(label);
        }

        ActiveRecord = _repository.StartTimeRecord(project.Id, label?.Id ?? 0);
        ElapsedTime = TimeSpan.Zero;
        _timer.Start();
    }

    [RelayCommand]
    public void StopTracking()
    {
        _repository.StopActiveTimeRecord(DateTime.Now);
        Reset();
    }

    [RelayCommand]
    public void DiscardTracking()
    {
        _repository.DiscardActiveTimeRecord();
        Reset();
    }
    private void Reset()
    {
        _timer.Stop();
        ActiveRecord = null;
        ElapsedTime = TimeSpan.Zero;
        LoadProjectSummaries();
    }

    private void UpdateElapsedTime()
    {
        if (ActiveRecord != null)
        {
            ElapsedTime = DateTime.Now - ActiveRecord.TimeStart;
        }
    }

    private void LoadProjectSummaries()
    {
        var summaries = _repository.GetAllProjects().Select(p =>
        {
            var records = _repository.GetRecordsForProject(p.Id);
            var totalTicks = records
                .Where(r => r.TimeEnd.HasValue)
                .Sum(r => (r.TimeEnd!.Value - r.TimeStart).Ticks);

            return new ProjectSummary(p.Name, new TimeSpan(totalTicks));
        }).OrderByDescending(r => r.TotalTime);

        ProjectSummaries = new ObservableCollection<ProjectSummary>(summaries);
    }
}