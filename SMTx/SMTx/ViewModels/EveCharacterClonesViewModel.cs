using System.Collections.ObjectModel;
using ReactiveUI;

namespace SMTx.ViewModels;

public sealed record CloneRowDto(string Title, string LocationText, string ImplantsText);

public sealed record ClonePanelState(IReadOnlyList<CloneRowDto> Rows, string? StatusMessage);

public sealed class EveCloneRowViewModel : ViewModelBase
{
    public EveCloneRowViewModel(CloneRowDto dto)
    {
        Title = dto.Title;
        LocationText = dto.LocationText;
        ImplantsText = dto.ImplantsText;
    }

    public string Title { get; }
    public string LocationText { get; }
    public string ImplantsText { get; }
}

public sealed class EveCharacterClonesViewModel : ViewModelBase
{
    private bool _isLoading;
    private string _statusMessage = "";

    public EveCharacterClonesViewModel()
    {
        Rows = new ObservableCollection<EveCloneRowViewModel>();
    }

    public ObservableCollection<EveCloneRowViewModel> Rows { get; }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            this.RaiseAndSetIfChanged(ref _isLoading, value);
            this.RaisePropertyChanged(nameof(ShowEmptyHint));
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            this.RaiseAndSetIfChanged(ref _statusMessage, value);
            this.RaisePropertyChanged(nameof(HasStatusMessage));
        }
    }

    public bool HasRows => Rows.Count > 0;

    public bool HasStatusMessage => !string.IsNullOrEmpty(_statusMessage);

    public bool ShowEmptyHint => !IsLoading && Rows.Count == 0 && string.IsNullOrEmpty(_statusMessage);

    public void Clear()
    {
        IsLoading = false;
        StatusMessage = "";
        Rows.Clear();
        RaiseRowRelated();
    }

    public void BeginLoad()
    {
        IsLoading = true;
        StatusMessage = "";
        Rows.Clear();
        RaiseRowRelated();
    }

    public void Apply(ClonePanelState state)
    {
        IsLoading = false;
        StatusMessage = state.StatusMessage ?? "";
        Rows.Clear();
        foreach (var d in state.Rows)
            Rows.Add(new EveCloneRowViewModel(d));
        RaiseRowRelated();
    }

    private void RaiseRowRelated()
    {
        this.RaisePropertyChanged(nameof(HasRows));
        this.RaisePropertyChanged(nameof(ShowEmptyHint));
        this.RaisePropertyChanged(nameof(HasStatusMessage));
    }
}
