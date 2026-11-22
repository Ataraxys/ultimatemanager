using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimateManager.Models.DynamicForms;
using UltimateManager.Services;

namespace UltimateManager.ViewModels;

public partial class TemplatesViewModel(IDynamicFormService formService, IAutomaticBackupService automaticBackupService) : ObservableObject
{
    private readonly IDynamicFormService _formService = formService;
    private readonly IAutomaticBackupService _automaticBackupService = automaticBackupService;

    public ObservableCollection<EntityTemplate> Templates { get; } = [];

    [ObservableProperty]
    private bool isBusy;

    public event EventHandler? AddRequested;
    public event EventHandler<EntityTemplate>? TemplateSelected;

    [RelayCommand]
    private Task AddAsync()
    {
        AddRequested?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task SelectAsync(EntityTemplate template)
    {
        TemplateSelected?.Invoke(this, template);
        return Task.CompletedTask;
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            await _formService.EnsureDefaultClientTemplateAsync(cancellationToken);
            Templates.Clear();
            foreach (var template in await _formService.GetTemplatesAsync(cancellationToken))
            {
                Templates.Add(template.Template);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<EntityTemplate> CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        var template = await _formService.CreateTemplateAsync(name, cancellationToken: cancellationToken);
        Templates.Add(template.Template);
        await _automaticBackupService.SaveAfterChangeAsync(cancellationToken);
        return template.Template;
    }
}
