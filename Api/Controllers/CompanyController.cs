using Api.Controllers.Base;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace Api.Controllers
{
    [Route("api/company")]
    [ApiController]
    [Authorize(Roles = "Manager,User")]
    public class CompanyController : BaseController
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
        public async Task<IActionResult> Map(string? searchTerm = null)
        {
            var result = await _companyServices.Map(searchTerm);
            return HandleResult(result);
        }
    }
}
