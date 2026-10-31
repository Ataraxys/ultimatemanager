using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimateManager.Models;
using UltimateManager.Services;

namespace UltimateManager.ViewModels;

public partial class ClientsViewModel(IClientService clientService) : ObservableObject
{
    private readonly IClientService _clientService = clientService;
    private CancellationTokenSource? _searchCancellation;

    public ObservableCollection<Client> Clients { get; } = [];

    public event EventHandler? AddRequested;

    public event EventHandler<Client>? ClientSelected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoClients))]
    private bool isLoading;

    [ObservableProperty]
    private string searchText = string.Empty;

    public bool HasNoClients => !IsLoading && Clients.Count == 0;

    partial void OnSearchTextChanged(string value)
    {
        _ = SearchAsync();
    }

    [RelayCommand]
    private Task AddAsync()
    {
        AddRequested?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task SelectAsync(Client client)
    {
        ClientSelected?.Invoke(this, client);
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task DeleteAsync(Client client)
    {
        if (IsLoading)
        {
            return;
        }

        await _clientService.DeleteAsync(client.Id);
        Clients.Remove(client);
        OnPropertyChanged(nameof(HasNoClients));
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsLoading)
        {
            return;
        }

        try
        {
            IsLoading = true;
            var clients = await _clientService.GetAllAsync();
            ReplaceClients(clients);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        _searchCancellation = new CancellationTokenSource();
        var cancellationToken = _searchCancellation.Token;

        try
        {
            IsLoading = true;
            var clients = string.IsNullOrWhiteSpace(SearchText)
                ? await _clientService.GetAllAsync(cancellationToken)
                : await _clientService.SearchAsync(SearchText.Trim(), cancellationToken);

            ReplaceClients(clients);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (cancellationToken == _searchCancellation.Token)
            {
                IsLoading = false;
            }
        }
    }

    private void ReplaceClients(IEnumerable<Client> clients)
    {
        Clients.Clear();
        foreach (var client in clients)
        {
            Clients.Add(client);
        }

        OnPropertyChanged(nameof(HasNoClients));
    }
}
