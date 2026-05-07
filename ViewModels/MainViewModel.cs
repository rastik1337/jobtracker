using System;
using System.Collections.Generic;
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
        !string.IsNullOrWhiteSpace(SelectedProjectName)
        && !string.IsNullOrWhiteSpace(SelectedLabelName);

    public record ProjectSummary(int Id, string Name, TimeSpan TotalTime);

    [ObservableProperty]
    private ObservableCollection<ProjectSummary> _projectSummaries = new();

    [ObservableProperty]
    private bool _isConfirmOpen;

    [ObservableProperty]
    private string _confirmMessage = string.Empty;
    private Action? _onConfirmAction;

    public enum DateRange
    {
        AllTime,
        ThreeMonths,
        Month,
        Week,
    }

    [ObservableProperty]
    private DateRange _selectedDateRange = DateRange.AllTime;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStatsProjectSelected))]
    private Project? _statsSelectedProject;

    public bool IsStatsProjectSelected => StatsSelectedProject != null;

    public record LabelSummary(string Name, TimeSpan TotalTime);

    [ObservableProperty]
    private ObservableCollection<LabelSummary> _labelSummaries = new();

    public IEnumerable<DateRange> DateRanges => Enum.GetValues<DateRange>();

    partial void OnStatsSelectedProjectChanged(Project? value) => UpdateStatistics();

    partial void OnSelectedDateRangeChanged(DateRange value) => UpdateStatistics();

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
            SelectedProjectName = Projects
                .FirstOrDefault(X => X.Id == ActiveRecord!.ProjectId)!
                .Name;
            SelectedLabelName = Labels.FirstOrDefault(X => X.Id == ActiveRecord!.LabelId)!.Name;
        }
        else
        {
            var lastRecord = _repository.GetLastRecord();
            if (lastRecord != null)
            {
                var project =
                    Projects.FirstOrDefault(X => X.Id == lastRecord.ProjectId)
                    ?? throw new Exception("Record exists but its project does not");

                SelectedProjectName = project!.Name;
                SelectedLabelName = Labels.FirstOrDefault(X => X.Id == lastRecord.LabelId)!.Name;
                StatsSelectedProject = project;
            }
        }
    }

    private void UpdateStatistics()
    {
        if (StatsSelectedProject == null)
        {
            LabelSummaries.Clear();
            return;
        }

        DateTime? from = SelectedDateRange switch
        {
            DateRange.Week => DateTime.Now.AddDays(-7),
            DateRange.Month => DateTime.Now.AddMonths(-1),
            DateRange.ThreeMonths => DateTime.Now.AddMonths(-3),
            _ => null,
        };

        var records = _repository.GetRecordsForProject(StatsSelectedProject.Id, from);
        var labelGroups = records
            .GroupBy(r => r.LabelId)
            .Select(g =>
            {
                var labelName = _repository.GetLabel(g.Key)?.Name ?? "Unknown label";
                var totalTicks = g.Sum(r => r.Duration.Ticks);
                return new LabelSummary(labelName, new TimeSpan(totalTicks));
            })
            .OrderByDescending(x => x.TotalTime);

        LabelSummaries = new ObservableCollection<LabelSummary>(labelGroups);
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
            .FirstOrDefault(p =>
                p.Name.Equals(SelectedProjectName, StringComparison.OrdinalIgnoreCase)
            );

        if (project == null)
        {
            project = _repository.InsertProject(new Project { Name = SelectedProjectName });
            Projects.Add(project);
        }

        var label = _repository
            .GetAllLabels()
            .FirstOrDefault(l =>
                l.Name.Equals(SelectedLabelName, StringComparison.OrdinalIgnoreCase)
            );

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
        RequestConfirmation(
            "Are you sure you want to discard the current tracked time?",
            () =>
            {
                _repository.DiscardActiveTimeRecord();
                Reset();
            }
        );
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
        var summaries = _repository
            .GetAllProjects()
            .Select(p =>
            {
                var records = _repository.GetRecordsForProject(p.Id, null);
                var totalTicks = records
                    .Where(r => r.TimeEnd.HasValue)
                    .Sum(r => (r.TimeEnd!.Value - r.TimeStart).Ticks);

                return new ProjectSummary(p.Id, p.Name, new TimeSpan(totalTicks));
            })
            .OrderByDescending(r => r.TotalTime);

        ProjectSummaries = new ObservableCollection<ProjectSummary>(summaries);
    }

    private void RefreshLabels()
    {
        Labels.Clear();
        foreach (var label in _repository.GetAllLabels())
        {
            Labels.Add(label);
        }
        if (
            Labels
                .AsQueryable()
                .FirstOrDefault(x =>
                    x.Name.Equals(SelectedLabelName, StringComparison.OrdinalIgnoreCase)
                ) == null
        )
        {
            SelectedLabelName = string.Empty;
        }
    }

    private void RequestConfirmation(string message, Action onConfirm)
    {
        ConfirmMessage = message;
        _onConfirmAction = onConfirm;
        IsConfirmOpen = true;
    }

    [RelayCommand]
    public void ConfirmAction()
    {
        _onConfirmAction?.Invoke();
        IsConfirmOpen = false;
    }

    [RelayCommand]
    public void CancelAction() => IsConfirmOpen = false;

    [RelayCommand]
    public void DeleteProject(ProjectSummary projectSummary)
    {
        RequestConfirmation(
            $"Are you sure you want to delete project '{projectSummary.Name}' and all its recorded time?",
            () =>
            {
                try
                {
                    var project = Projects.First(x => x.Id == projectSummary.Id);
                    _repository.DeleteProject(project.Id);

                    Projects.Remove(project);
                    if (StatsSelectedProject == project)
                        StatsSelectedProject = null;
                    LoadProjectSummaries();
                    RefreshLabels();
                }
                catch (Exception ex)
                {
                    ConfirmMessage = $"Failed to delete project: {ex.Message}";
                    IsConfirmOpen = true;
                }
            }
        );
    }

    [RelayCommand]
    public void DeleteLabel(Label label)
    {
        RequestConfirmation(
            $"Are you sure you want to delete label '{label.Name}' and all its recorded time?",
            () =>
            {
                try
                {
                    _repository.DeleteLabel(label.Id);
                    Labels.Remove(label);
                    UpdateStatistics();
                }
                catch (Exception ex)
                {
                    ConfirmMessage = $"Failed to delete label: {ex.Message}";
                    IsConfirmOpen = true;
                }
            }
        );
    }
}
