using CommunityToolkit.Mvvm.ComponentModel;

namespace UltimateManager.Models;

public partial class ImportContactItem : ObservableObject
{
    private bool _isSelected;

    public ImportContactItem(Client client)
    {
        Client = client;
        IsSelected = true;
    }

    public Client Client { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
