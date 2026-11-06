namespace GastosBackend.Application.Interfaces
{
    /// <summary>What a repository returns for a paged query: the page and how many items match in total.</summary>
    public record PagedResult<T>(List<T> Items, int Total);
}
