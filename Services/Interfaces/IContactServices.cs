using Domain.Common;
using DTO.Request;
using DTO.Response;

namespace Services.Interfaces
{
    public interface IContactServices
    {
        Task<Result<PagedResult<ContactsResponse>>> GetContactsAsync(
                    PaggedRequest pagged,
                    ContactFilterRequest filter,
                    SortingRequest sorting,
                    SearchRequest search
                    );
        Task<Result<List<string>>> GetCompaniesAsync();
        Task<Result<PagedResult<CompanyContactResponse>>> GetCompanyContactsAsync(Guid comapnyId, PaggedRequest pagged);
        Task<Result<ContactsResponse>> GetContactDetailAsync(Guid contactId);
        Task<Result<List<ContactWayResponse>>> GetContactWayAsync(Guid contactId);
        Task<Result<PagedResult<ContactNoteResponse>>> GetContactNoteAsync(Guid contatcId, PaggedRequest pagged, SearchRequest search);
    }
}
