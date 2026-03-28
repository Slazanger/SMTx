using System;
using Avalonia.Media.Imaging;
using ReactiveUI;

namespace SMTx.ViewModels;

public sealed class EveCharacterDetailViewModel : ViewModelBase
{
    private bool _isLoading;
    private string _loadError = "";
    private string _characterName = "";
    private string _characterIdText = "";
    private string _corporationLine = "";
    private string _allianceLine = "";
    private string _locationLine = "";
    private string _shipLine = "";
    private bool _hasCorporation;
    private bool _hasAlliance;
    private Bitmap? _portrait;
    private Bitmap? _corporationLogo;
    private Bitmap? _allianceLogo;

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            this.RaiseAndSetIfChanged(ref _isLoading, value);
            this.RaisePropertyChanged(nameof(IsDetailReady));
        }
    }

    public string LoadError
    {
        get => _loadError;
        set
        {
            this.RaiseAndSetIfChanged(ref _loadError, value);
            this.RaisePropertyChanged(nameof(HasLoadError));
            this.RaisePropertyChanged(nameof(IsDetailReady));
        }
    }

    public bool HasLoadError => !string.IsNullOrEmpty(_loadError);

    public bool IsDetailReady => !_isLoading && string.IsNullOrEmpty(_loadError);

    public string CharacterName
    {
        get => _characterName;
        set => this.RaiseAndSetIfChanged(ref _characterName, value);
    }

    public string CharacterIdText
    {
        get => _characterIdText;
        set => this.RaiseAndSetIfChanged(ref _characterIdText, value);
    }

    public string CorporationLine
    {
        get => _corporationLine;
        set => this.RaiseAndSetIfChanged(ref _corporationLine, value);
    }

    public string AllianceLine
    {
        get => _allianceLine;
        set => this.RaiseAndSetIfChanged(ref _allianceLine, value);
    }

    public string LocationLine
    {
        get => _locationLine;
        set
        {
            this.RaiseAndSetIfChanged(ref _locationLine, value);
            this.RaisePropertyChanged(nameof(HasLocation));
        }
    }

    public string ShipLine
    {
        get => _shipLine;
        set
        {
            this.RaiseAndSetIfChanged(ref _shipLine, value);
            this.RaisePropertyChanged(nameof(HasShip));
        }
    }

    public bool HasLocation => !string.IsNullOrEmpty(_locationLine);

    public bool HasShip => !string.IsNullOrEmpty(_shipLine);

    public bool HasCorporation => _hasCorporation;

    public bool HasAlliance => _hasAlliance;

    public Bitmap? Portrait
    {
        get => _portrait;
        private set => ReplaceBitmap(ref _portrait, value, nameof(Portrait));
    }

    public Bitmap? CorporationLogo
    {
        get => _corporationLogo;
        private set => ReplaceBitmap(ref _corporationLogo, value, nameof(CorporationLogo));
    }

    public Bitmap? AllianceLogo
    {
        get => _allianceLogo;
        private set => ReplaceBitmap(ref _allianceLogo, value, nameof(AllianceLogo));
    }

    public void ClearSelection()
    {
        IsLoading = false;
        LoadError = "";
        CharacterName = "";
        CharacterIdText = "";
        CorporationLine = "";
        AllianceLine = "";
        LocationLine = "";
        ShipLine = "";
        _hasCorporation = false;
        _hasAlliance = false;
        this.RaisePropertyChanged(nameof(HasCorporation));
        this.RaisePropertyChanged(nameof(HasAlliance));
        Portrait = null;
        CorporationLogo = null;
        AllianceLogo = null;
        this.RaisePropertyChanged(nameof(IsDetailReady));
    }

    public void BeginLoad(EvePilotRow row)
    {
        DisposeBitmaps();
        IsLoading = true;
        LoadError = "";
        CharacterName = row.Name;
        CharacterIdText = row.CharacterId.ToString();
        CorporationLine = "";
        AllianceLine = "";
        LocationLine = "";
        ShipLine = "";
        _hasCorporation = false;
        _hasAlliance = false;
        this.RaisePropertyChanged(nameof(HasCorporation));
        this.RaisePropertyChanged(nameof(HasAlliance));
    }

    public void SetFailed(string message)
    {
        IsLoading = false;
        LoadError = message;
        LocationLine = "";
        ShipLine = "";
        DisposeBitmaps();
    }

    public void ApplyLoaded(
        string characterName,
        long characterId,
        string corporationLine,
        bool hasCorporation,
        string allianceLine,
        bool hasAlliance,
        string locationLine,
        string shipLine,
        Bitmap? portrait,
        Bitmap? corpLogo,
        Bitmap? allianceLogo)
    {
        IsLoading = false;
        LoadError = "";
        CharacterName = characterName;
        CharacterIdText = characterId.ToString();
        CorporationLine = corporationLine;
        AllianceLine = allianceLine;
        LocationLine = locationLine;
        ShipLine = shipLine;
        _hasCorporation = hasCorporation;
        _hasAlliance = hasAlliance;
        this.RaisePropertyChanged(nameof(HasCorporation));
        this.RaisePropertyChanged(nameof(HasAlliance));
        Portrait = portrait;
        CorporationLogo = corpLogo;
        AllianceLogo = allianceLogo;
    }

    private void ReplaceBitmap(ref Bitmap? field, Bitmap? value, string propertyName)
    {
        if (ReferenceEquals(field, value))
            return;
        field?.Dispose();
        field = value;
        this.RaisePropertyChanged(propertyName);
    }

    private void DisposeBitmaps()
    {
        Portrait = null;
        CorporationLogo = null;
        AllianceLogo = null;
    }
}
