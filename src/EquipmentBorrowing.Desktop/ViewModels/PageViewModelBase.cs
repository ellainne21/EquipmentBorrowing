using CommunityToolkit.Mvvm.ComponentModel;
using EquipmentBorrowing.Desktop.Api;

namespace EquipmentBorrowing.Desktop.ViewModels;

// Shared state for pages that load data: loading flag, load error, and a status message.
public abstract partial class PageViewModelBase : ViewModelBase
{
    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError))]
    public partial string? LoadError { get; set; }

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    public bool HasLoadError => !string.IsNullOrEmpty(LoadError);

    // One label per kind of failure, so the user can tell them apart:
    // "No connection" = no HTTP response at all. "Server error" = the API answered with a failure.
    // "Result unknown" = a change was sent but its result is not known (refresh before retrying).
    protected static string Describe(ApiOutcome outcome, string message) => outcome switch
    {
        ApiOutcome.ValidationError => $"Check your input: {message}",
        ApiOutcome.NotFound => $"Not found: {message}",
        ApiOutcome.Conflict => $"Not allowed: {message}",
        ApiOutcome.ServerError => $"Server error: {message}",
        ApiOutcome.Unreachable => $"No connection: {message}",
        ApiOutcome.UnknownOutcome => $"Result unknown: {message}",
        _ => message
    };
}