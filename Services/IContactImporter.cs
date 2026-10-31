using UltimateManager.Models;

namespace UltimateManager.Services;

public interface IContactImporter
{
    Task<IReadOnlyList<Client>> ImportAsync(CancellationToken cancellationToken = default);
}