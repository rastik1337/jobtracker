using JobTracker.Data;
using JobTracker.Models;

namespace JobTracker.ViewModels;

public partial class MainViewModel(JobTrackerRepository repository) : ViewModelBase
{
    private readonly JobTrackerRepository _repository = repository;
}