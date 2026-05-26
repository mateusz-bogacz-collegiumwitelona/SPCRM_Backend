using Domain.Common;
using DTO.Request;
using DTO.Response;

namespace Services.Interfaces
{
    public interface IContactServices
    {
        Task<Result<PagedResult<ContactsResponse>>> GetContactsAsync(PaggedRequest pagged, ContactFilterRequest filter, SearchRequest search);
        Task<Result<List<string>>> GetCompaniesAsync();
        Task<Result<PagedResult<CompanyContactResponse>>> GetCompanyContactsAsync(Guid comapnyId, PaggedRequest pagged);
    }
}
