using CommunityToolkit.Mvvm.ComponentModel;

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
}
