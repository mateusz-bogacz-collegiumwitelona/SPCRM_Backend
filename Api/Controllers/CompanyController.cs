using Api.Controllers.Base;
using Api.Mappers;
using Api.Request.Company;
using Api.Request.List;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Services.Interfaces;
using Services.Response.Company;

namespace Api.Controllers
{
    [Route("api/company")]
    [ApiController]
    public class CompanyController : BaseControlle
    {
        [EndpointSummary("Get data to global map")]
        [EndpointDescription("Show data of every company on the global map.")]
        [ProducesResponseType(typeof(Result<List<CompaniesMapResponse>>), StatusCodes.Status200OK)]
        [HttpGet("map")]
        [Authorize(Roles = "Manager,User")]
        [EnableRateLimiting("expensive")]
        public async Task<IActionResult> GetMapAsync(
            [FromServices] ICompanyServices companyServices,
            string? searchTerm = null
            )
        {
            var result = await companyServices.GetMapAsync(searchTerm);
            return HandleResult(result);
        }

        [EndpointSummary("Get detail about company")]
        [EndpointDescription("Show detail about company. This endpoint return onliy name, Nip and data to map")]
        [ProducesResponseType(typeof(Result<CompanyDetailResponse>), StatusCodes.Status200OK)]
        [HttpGet]
        [Authorize(Roles = "Manager,User")]
        public async Task<IActionResult> GetCompanyDetailsAsync(
            [FromServices] ICompanyServices companyServices,
            [FromQuery] Guid companyId
            )
        {
            var result = await companyServices.GetCompanyDetailsAsync(companyId, CurrentUserId);
            return HandleResult(result);
        }

        [EndpointSummary("Get all company adresses")]
        [EndpointDescription("Show all company adresses. This endpoint return only city, street, zip-code, lat and log")]
        [ProducesResponseType(typeof(Result<PagedResult<AddressDetailResponse>>), StatusCodes.Status200OK)]
        [HttpGet("addresses")]
        [Authorize(Roles = "Manager,User")]
        public async Task<IActionResult> GetCompanyAddressesAsync(
            [FromServices] CompanyMapper mapper,
            [FromServices] ICompanyServices companyServices,
            [FromQuery] Guid companyId,
            [FromQuery] PaggedRequest pagged
            )
        {
            var result = await companyServices.GetCompanyAddressesAsync(mapper.MapBasic(companyId, pagged));
            return HandleResult(result);
        }

        [EndpointSummary("Get company contacts")]
        [EndpointDescription("Show all company contacts. " +
            "This endpoint return only first name, last name, job title and if contact is primary")]
        [ProducesResponseType(typeof(Result<PagedResult<CompanyContactResponse>>), StatusCodes.Status200OK)]
        [HttpGet("contacts")]
        [Authorize(Roles = "Manager,User")]
        public async Task<IActionResult> GetCompanyContactsAsync(
            [FromServices] CompanyMapper mapper,
            [FromServices] IContactServices contactServices,
            [FromQuery] Guid companyId,
            [FromQuery] PaggedRequest pagged
            )
        {
            var result = await contactServices.GetCompanyContactsAsync(mapper.MapBasic(companyId, pagged));
            return HandleResult(result);
        }

        [EndpointSummary("Get company sales")]
        [EndpointDescription("Show all company sales. " +
            "This endpoint return only name, value, close date and status")]
        [ProducesResponseType(typeof(Result<PagedResult<CompanyDealsResponse>>), StatusCodes.Status200OK)]
        [HttpGet("sales")]
        public async Task<IActionResult> GetComapanyDealsAsync(
            [FromServices] CompanyMapper mapper,
            [FromServices] IDealServices salesServices,
            [FromQuery] Guid companyId,
            [FromQuery] PaggedRequest pagged
        )
        {
            var result = await salesServices.GetComapanyDealsAsync(mapper.MapBasic(companyId, pagged));
            return HandleResult(result);
        }

        [EndpointSummary("Get company debt summary")]
        [EndpointDescription("Show total unpaid amount grouped by currency for a specific company.")]
        [ProducesResponseType(typeof(Result<List<CompanyDebtSummaryResponse>>), StatusCodes.Status200OK)]
        [HttpGet("debts/summary")]
        [Authorize(Roles = "Manager,User")]
        public async Task<IActionResult> GetCompanyDebtSummaryAsync(
            [FromServices] IInvoiceService debtServices,
            [FromQuery] Guid companyId)
        {
            var result = await debtServices.GetCompanyDebtSummaryAsync(companyId);
            return HandleResult(result);
        }

        [EndpointSummary("Get company debts details")]
        [EndpointDescription("Show all unpaid invoices for a specific company with pagination.")]
        [ProducesResponseType(typeof(Result<PagedResult<CompanyDebtDetailResponse>>), StatusCodes.Status200OK)]
        [HttpGet("debts")]
        [Authorize(Roles = "Manager,User")]
        public async Task<IActionResult> GetCompanyDebts(
            [FromServices] IInvoiceService debtServices,
            [FromQuery] Guid companyId,
            [FromQuery] PaggedRequest pagged,
            [FromServices] CompanyMapper mapper
        )
        {
            var result = await debtServices.GetCompanyDebtsAsync(mapper.MapBasic(companyId, pagged));
            return HandleResult(result);
        }

        [EndpointSummary("Get paginated list of companies")]
        [EndpointDescription("Show a paginated list of companies with optional filtering, sorting, and search term. " +
            "Returns basic company details along with the headquarters address and the date of the last deal.")]
        [ProducesResponseType(typeof(Result<PagedResult<CompanyResponse>>), StatusCodes.Status200OK)]
        [HttpGet("list")]
        [Authorize(Roles = "Manager,User")]
        public async Task<IActionResult> GetCompanyListAsync(
           [FromServices] CompanyMapper mapper,
           [FromServices] ICompanyServices companyServices,
           [FromQuery] PaggedRequest pagged,
           [FromQuery] CompanyFilterRequest filer,
           [FromQuery] SortingRequest sorting,
           [FromQuery] SearchRequest search
            )
        {
            var result = await companyServices.GetCompanyListAsync(mapper.MapList(
                CurrentUserId,
                pagged,
                filer,
                sorting,
                search
            ));
            return HandleResult(result);
        }

        [EndpointSummary("Get simple list of companies")]
        [EndpointDescription("Show a simple list of companies with only ID and Name.")]
        [ProducesResponseType(typeof(Result<List<CompanySimpleListResponse>>), StatusCodes.Status200OK)]
        [HttpGet("simple-list")]
        public async Task<IActionResult> GetCompanySimpleListAsync(
            [FromServices] ICompanyServices companyServices
        )
        {
            var result = await companyServices.GetCompanySimpleListAsync();
            return HandleResult(result);
        }

        [EndpointSummary("Add a new company")]
        [EndpointDescription("Add a new company with its details.")]
        [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status201Created)]
        [HttpPost]
        [Authorize(Roles = "Manager,User")]
        public async Task<IActionResult> AddCompanyAsync(
            [FromServices] ICompanyServices company,
            [FromServices] CompanyMapper mapper,
            [FromBody] AddCompanyRequest request
            )
        {
            var result = await company.AddCompanyAsync(mapper.MapAdd(request), CurrentUserId);
            return HandleResult(result);
        }

        [EndpointSummary("Edit an existing company")]
        [EndpointDescription("Edit an existing company with its details.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [HttpPatch]
        [Authorize(Roles = "Manager,User")]
        public async Task<IActionResult> EditCompanyAsync(
            [FromServices] ICompanyServices company,
            [FromServices] CompanyMapper mapper,
            [FromBody] EditCompanyRequest request
            )
        {
            var result = await company.EditCompanyAsync(mapper.MapEdit(request), CurrentUserId);
            return HandleResult(result);
        }

        [EndpointSummary("Edit an existing company address")]
        [EndpointDescription("Edit an existing company address with its details.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [HttpPatch("address")]
        [Authorize(Roles = "Manager,User")]
        public async Task<IActionResult> EditCompanyAddressAsync(
            [FromServices] ICompanyServices company,
            [FromServices] CompanyMapper mapper,
            [FromBody] EditCompanyAdressRequest request
            )
        {
            var result = await company.EditCompanyAddressAsync(mapper.MapEditAddress(request), CurrentUserId);
            return HandleResult(result);
        }

        [EndpointSummary("Add a new company address")]
        [EndpointDescription("Add a new company address with its details.")]
        [ProducesResponseType(typeof(Result<Guid>), StatusCodes.Status201Created)]
        [HttpPost("address/{companyId:guid}")]
        [Authorize(Roles = "Manager,User")]
        public async Task<IActionResult> AddCompanyAddressAsync(
            [FromServices] ICompanyServices company,
            [FromServices] CompanyMapper mapper,
            [FromBody] AddCompanyAdressRequest request,
            [FromRoute] Guid companyId
            )
        {
            var result = await company.AddCompanyAddressAsync(mapper.MapAddAddress(request), CurrentUserId, companyId);
            return HandleResult(result);
        }

        [EndpointSummary("Delete an existing company")]
        [EndpointDescription("Delete an existing company by its ID.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [HttpDelete("{companyId:guid}")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> DeleteCompanyAsync(
            [FromServices] ICompanyServices company,
            [FromRoute] Guid companyId
            )
        {
            var result = await company.DeleteCompanyAsync(companyId, CurrentUserId);
            return HandleResult(result);
        }

        [EndpointSummary("Delete an existing company address")]
        [EndpointDescription("Delete an existing company address by its ID.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [HttpDelete("address/{addressId:guid}")]
        [Authorize(Roles = "Manager,User")]
        public async Task<IActionResult> DeleteCompanyAddressAsync(
            [FromServices] ICompanyServices company,
            [FromRoute] Guid addressId
            )
        {
            var result = await company.DeleteCompanyAddressAsync(addressId, CurrentUserId);
            return HandleResult(result);
        }

        [EndpointSummary("Change company owner")]
        [EndpointDescription("Reassign company ownership to another user.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [HttpPatch("change-owner")]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<IActionResult> ChangeCompanyOwnerAsync(
            [FromServices] ICompanyServices company,
            [FromServices] CompanyMapper mapper,
            [FromBody] ChangeCompanyOwnerRequest request
            )
        {
            var result = await company.ChangeCompanyOwnerAsync(mapper.MapChangeOwner(request));
            return HandleResult(result);
        }

        [EndpointSummary("Get address type list")]
        [EndpointDescription("Get address type list")]
        [ProducesResponseType(typeof(Result<List<string>>), StatusCodes.Status200OK)]
        [HttpGet("address/types")]
        [Authorize(Roles = "Manager,User")]
        public async Task<IActionResult> GetCompanyAddressTypes(
            [FromServices] ICompanyServices companyServices
            )
        {
            var result = companyServices.GetCompanyAddressTypes();
            return HandleResult(result);
        }

        [EndpointSummary("Get company edit details")]
        [EndpointDescription("Get company details (ID, Name, NIP) required for the edit form.")]
        [ProducesResponseType(typeof(Result<EditCompanyDetailResponse>), StatusCodes.Status200OK)]
        [HttpGet("edit-detail/{id:guid}")]
        [Authorize(Roles = "Manager,User")]
        public async Task<IActionResult> GetEditCompanyDetailAsync(
            [FromServices] ICompanyServices companyServices,
            [FromRoute] Guid id
            )
        {
            var result = await companyServices.GetEditCompanyDetailAsync(id);
            return HandleResult(result);
        }
    }
}
