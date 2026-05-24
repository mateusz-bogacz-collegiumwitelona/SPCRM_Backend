using Domain.Common;
using DTO.Request;
using DTO.Response;

namespace Services.Interfaces
{
    public interface IContactServices
    {
        Task<Result<PagedResult<ContactsResponse>>> GetContacts(PaggedRequest pagged, ContactFilterRequest filter, SearchRequest search);
        Task<Result<List<string>>> GetCompanies();
    }
}
