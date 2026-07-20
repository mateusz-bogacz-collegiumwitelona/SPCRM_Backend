using Api.Request;
using Riok.Mapperly.Abstractions;
using Services.Command;

namespace Api.Mappers
{
    [Mapper]
    public partial class ContactMapper
    {
        public partial ContactListCommand MapContactList(
            PaggedRequest pagged, 
            ContactFilterRequest filter, 
            SortingRequest sorting, 
            SearchRequest search
            );
    }
}
