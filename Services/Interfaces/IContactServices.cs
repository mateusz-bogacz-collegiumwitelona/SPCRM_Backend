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
        Task<Result<PagedResult<MailingClientResponse>>> GetClientDataToMailingAsync(SimpleListCommand command);
        Task<Result> AddContactAsync(AddContactCommand command, Guid userId);
        Task<Result<List<string>>> GetContactTypeAsync();
        Task<Result> EditContactAsync(EditContactCommand command, Guid currentUserId);
    }
}
