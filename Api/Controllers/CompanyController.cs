using Api.Controllers.Base;
using Domain.Common;
using DTO.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace Api.Controllers
{
    [Route("api/company")]
    [ApiController]
    public class CompanyController : AuthControllerBase
    {
        private readonly ICompanyServices _companyServices;
        private readonly IContactServices _contactServices;
        private readonly ISalesServices _salesServices;
        private readonly IDebtService _debtServices;

        public CompanyController(
            ICompanyServices companyServices,
            IContactServices contactServices,
            ISalesServices salesServices,
            IDebtService debtService)
        {
            _companyServices = companyServices;
            _contactServices = contactServices;
            _salesServices = salesServices;
            _debtServices = debtService;
        }

        [EndpointSummary("Get data to global map")]
        [EndpointDescription("Show data of every company on the global map.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status500InternalServerError)]
        [HttpGet("map")]
        [Authorize(Roles = "Manager,User")]
        public async Task<IActionResult> Map(string? searchTerm = null)
        {
            var result = await _companyServices.Map(searchTerm);
            return HandleResult(result);
        }

        [EndpointSummary("Get detail about company")]
        [EndpointDescription("Show detail about company. This endpoint return onliy name, Nip and data to map")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status500InternalServerError)]
        [HttpGet("")]
        [Authorize(Roles = "Manager,User")]
        public async Task<IActionResult> Details([FromQuery] string id)
        {
            var result = await _companyServices.Details(id, CurrentUserId);
            return HandleResult(result);
        }

        [EndpointSummary("Get all company adresses")]
        [EndpointDescription("Show all company adresses. This endpoint return only city, street, zip-code, lat and log")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status500InternalServerError)]
        [HttpGet("addresses")]
        [Authorize(Roles = "Manager,User")]
        public async Task<IActionResult> GetCompanyAddresses(
            [FromQuery] Guid companyId,
            [FromQuery] PaggedRequest pagged
            )
        {
            var result = await _companyServices.GetCompanyAddresses(companyId, pagged);
            return HandleResult(result);
        }

        [EndpointSummary("Get company contacts")]
        [EndpointDescription("Show all company contacts. This endpoint return only first name, last name, job title and if contact is primary")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status500InternalServerError)]
        [HttpGet("contacts")]
        [Authorize(Roles = "Manager,User")]
        public async Task<IActionResult> GetCompanyContacts(
            [FromQuery] Guid companyId,
            [FromQuery] PaggedRequest pagged
            )
        {
            var result = await _contactServices.GetCompanyContactsAsync(companyId, pagged);
            return HandleResult(result);
        }

        [EndpointSummary("Get company sales")]
        [EndpointDescription("Show all company sales. This endpoint return only name, value, close date and status")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status500InternalServerError)]
        [HttpGet("sales")]
        public async Task<IActionResult> GetComapanySalesAsync(
            [FromQuery] Guid companyId,
            [FromQuery] PaggedRequest pagged
            )
        {
            var result = await _salesServices.GetComapanySalesAsync(companyId, pagged);
            return HandleResult(result);
        }

        [EndpointSummary("Get company debt summary")]
        [EndpointDescription("Show total unpaid amount grouped by currency for a specific company.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status500InternalServerError)]
        [HttpGet("debts/summary")]
        [Authorize(Roles = "Manager,User")]
        public async Task<IActionResult> GetCompanyDebtSummaryAsync([FromQuery] Guid comapnyId)
        {
            var result = await _debtServices.GetCompanyDebtSummaryAsync(comapnyId);
            return HandleResult(result);
        }

    }
}
