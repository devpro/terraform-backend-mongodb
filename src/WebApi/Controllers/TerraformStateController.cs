using Kalosyni.TerraformBackend.Domain.Models;
using Kalosyni.TerraformBackend.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Kalosyni.TerraformBackend.WebApi.Controllers
{
    [ApiController]
    [Route("")]
    public class TerraformStateController : ControllerBase
    {
        private readonly IStateRepository _stateRepository;

        public TerraformStateController(IStateRepository stateRepository)
        {
            _stateRepository = stateRepository;
        }

        [HttpGet(Name = "GetState")]
        public Task<List<StateModel>> Get()
        {
            return _stateRepository.GetAllAsync();
        }
    }
}
