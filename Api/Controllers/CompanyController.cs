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

        public CompanyController(ICompanyServices companyServices)
        {
            _companyServices = companyServices;
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
    }
}
