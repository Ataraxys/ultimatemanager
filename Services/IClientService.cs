using UltimateManager.Models;

namespace UltimateManager.Services;

public interface IClientService
{
    Task<IReadOnlyList<Client>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Client?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Client> CreateAsync(Client client, CancellationToken cancellationToken = default);

    Task<Client> UpdateAsync(Client client, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Client>> SearchAsync(string searchText, CancellationToken cancellationToken = default);
}
