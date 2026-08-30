using Api.Mappers.Helper;
using Api.Request.Contact;
using Api.Request.List;
using Riok.Mapperly.Abstractions;
using Services.Command.Contact;

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

        [MapProperty(nameof(AddContactRequest.FirstName), nameof(AddContactCommand.FirstName), Use = nameof(NormalizeName))]
        [MapProperty(nameof(AddContactRequest.LastName), nameof(AddContactCommand.LastName), Use = nameof(NormalizeName))]
        [MapProperty(nameof(AddContactRequest.JobTitle), nameof(AddContactCommand.JobTitle), Use = nameof(NormalizeName))]
        public partial AddContactCommand MapAdd(AddContactRequest request);

        [MapProperty(nameof(EditContactRequest.FirstName), nameof(EditContactCommand.FirstName), Use = nameof(NormalizeName))]
        [MapProperty(nameof(EditContactRequest.LastName), nameof(EditContactCommand.LastName), Use = nameof(NormalizeName))]
        [MapProperty(nameof(EditContactRequest.JobTitle), nameof(EditContactCommand.JobTitle), Use = nameof(NormalizeName))]
        public partial EditContactCommand MapEdit(EditContactRequest request);

        [MapProperty(nameof(AddContactDetailRequest.Label), nameof(AddContactDetailCommand.Label), Use = nameof(NormalizeName))]
        [MapProperty(nameof(AddContactDetailRequest.Value), nameof(AddContactDetailCommand.Value), Use = nameof(Trim))]
        private partial AddContactDetailCommand MapAddDetail(AddContactDetailRequest request);

        [MapProperty(nameof(EditContactDetailRequest.Label), nameof(EditContactDetailCommand.Label), Use = nameof(NormalizeName))]
        [MapProperty(nameof(EditContactDetailRequest.Value), nameof(EditContactDetailCommand.Value), Use = nameof(Trim))]
        private partial EditContactDetailCommand MapEditDetail(EditContactDetailRequest request);

        public partial ChangeContactOwnerCommand MapChangeOwner(ChangeContactOwnerRequest request);

        private string? NormalizeName(string? value) => StringNormalizerHelper.NormalizeName(value);
        private string? Trim(string? value) => StringNormalizerHelper.Trim(value);
    }
}
