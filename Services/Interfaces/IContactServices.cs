using Domain.Common;
using Services.Command;
using Services.Response;

namespace Services.Interfaces
{
    public interface IContactServices
    {
        Task<Result<PagedResult<ContactsResponse>>> GetContactsAsync(ContactListCommand command);
        Task<Result<List<string>>> GetCompaniesAsync();
        Task<Result<PagedResult<CompanyContactResponse>>> GetCompanyContactsAsync(CompanyCommand command);
        Task<Result<ContactsResponse>> GetContactDetailAsync(Guid contactId);
        Task<Result<List<ContactWayResponse>>> GetContactWayAsync(Guid contactId);
        Task<Result<PagedResult<ContactNoteResponse>>> GetContactNoteAsync(NoteListCommand command);
    }
}
