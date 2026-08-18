using Api.Request;
using Riok.Mapperly.Abstractions;
using Services.Command;

namespace Api.Mappers
{
    [Mapper]
    public partial class ContactMapper
    {

        public ContactListCommand MapContactList(
            PaggedRequest pagged,
            ContactFilterRequest filter,
            SortingRequest sorting,
            SearchRequest search
            )
        => new ContactListCommand
        {
            PageNumber = pagged.PageNumber,
            PageSize = pagged.PageSize,
            ComapnyName = filter.ComapnyName,
            IsPrimary = filter.IsPrimary,
            SortBy = sorting.SortBy,
            SortDescending = sorting.SortDescending,
            SearchTerm = search.SearchTerm,
            OwnerId = filter.OwnerId
        };

        public partial AddContactCommand MapAdd(AddContactRequest request);

        public partial EditContactCommand MapEdit(EditContactRequest request);

        public partial ChangeContactOwnerCommand MapChangeOwner(ChangeContactOwnerRequest request);
    }
}
