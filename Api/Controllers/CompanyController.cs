using Api.Controllers.Base;
using Api.Mappers;
using Api.Request;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;
using Services.Response;

namespace Api.Controllers
{
    [Route("api/company")]
    [ApiController]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status500InternalServerError)]
    public class CompanyController : AuthControllerBase
    {
        [EndpointSummary("Get data to global map")]
        [EndpointDescription("Show data of every company on the global map.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [HttpGet("map")]
        [Authorize(Roles = "Manager,User")]
        public async Task<IActionResult> Map(
            [FromServices] ICompanyServices companyServices,
            string? searchTerm = null
            )
        {
            var result = await companyServices.Map(searchTerm);
            return HandleResult(result);
        }

        [EndpointSummary("Get detail about company")]
        [EndpointDescription("Show detail about company. This endpoint return onliy name, Nip and data to map")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [HttpGet("")]
        [Authorize(Roles = "Manager,User")]
        public async Task<IActionResult> Details(
            [FromServices] ICompanyServices companyServices,
            [FromQuery] Guid companyId
            )
        {
            var result = await companyServices.Details(companyId, CurrentUserId);
            return HandleResult(result);
        }

        [EndpointSummary("Get all company adresses")]
        [EndpointDescription("Show all company adresses. This endpoint return only city, street, zip-code, lat and log")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [HttpGet("addresses")]
        [Authorize(Roles = "Manager,User")]
        public async Task<IActionResult> GetCompanyAddresses(
            [FromServices] CompanyMapper mapper,
            [FromServices] ICompanyServices companyServices,
            [FromQuery] Guid companyId, 
            [FromQuery] PaggedRequest pagged
            )
        {
            var result = await companyServices.GetCompanyAddresses(mapper.MapBasic(companyId, pagged));
            return HandleResult(result);
        }

        [EndpointSummary("Get company contacts")]
        [EndpointDescription("Show all company contacts. " +
            "This endpoint return only first name, last name, job title and if contact is primary")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [HttpGet("contacts")]
        [Authorize(Roles = "Manager,User")]
        public async Task<IActionResult> GetCompanyContacts(
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
        [EndpointDescription("Show all company sales. This endpoint return only name, value, close date and status")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [HttpGet("sales")]
        public async Task<IActionResult> GetComapanySalesAsync(
            [FromServices] CompanyMapper mapper,
            [FromServices] ISalesServices salesServices,
            [FromQuery] Guid companyId,
            [FromQuery] PaggedRequest pagged
        )
        {
            var result = await salesServices.GetComapanySalesAsync(mapper.MapBasic(companyId, pagged));
            return HandleResult(result);
        }

        [EndpointSummary("Get company debt summary")]
        [EndpointDescription("Show total unpaid amount grouped by currency for a specific company.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [HttpGet("debts/summary")]
        [Authorize(Roles = "Manager,User")]
        public async Task<IActionResult> GetCompanyDebtSummaryAsync(
            [FromServices] IDebtService debtServices,
            [FromQuery] Guid companyId)
        {
            var result = await debtServices.GetCompanyDebtSummaryAsync(companyId);
            return HandleResult(result);
        }

        [EndpointSummary("Get company debts details")]
        [EndpointDescription("Show all unpaid invoices for a specific company with pagination.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [HttpGet("debts")]
        [Authorize(Roles = "Manager,User")]
        public async Task<IActionResult> GetCompanyDebts(
            [FromServices] IDebtService debtServices,
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
    }
}
