using Kalosyni.TerraformBackend.Domain.Models;
using Kalosyni.TerraformBackend.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Kalosyni.TerraformBackend.WebApi.Controllers
{
    [ApiController]
    [Route("state")]
    public class StateController : ControllerBase
    {
        private readonly IStateRepository _stateRepository;

        private readonly IStateLockRepository _stateLockRepository;

        public StateController(IStateRepository stateRepository, IStateLockRepository stateLockRepository)
        {
            _stateRepository = stateRepository;
            _stateLockRepository = stateLockRepository;
        }

        [HttpGet("{name}", Name = "GetState")]
        [ProducesResponseType(200)]
        public async Task<string> FindOne(string name)
        {
            return await _stateRepository.FindOneAsync(name);
        }

        [HttpPost("{name}", Name = "CreateState")]
        [ProducesResponseType(201)]
        public async Task Create(string name, [FromQuery(Name = "ID")] string? lockId, [FromBody] string input)
        {
            //TODO: check lock
            await _stateRepository.CreateAsync(name, input);
        }

        [HttpPost("{name}/lock", Name = "CreateStateLock")]
        [ProducesResponseType(201)]
        public async Task Lock(string name, [FromBody] StateLockModel input)
        {
            //FIXME
            await _stateLockRepository.CreateAsync(input);
        }

        [HttpDelete("{name}/lock", Name = "DeleteStateLock")]
        [ProducesResponseType(204)]
        public async Task Unlock(string name)
        {
            //FIXME
            await _stateLockRepository.DeleteAsync(null);
        }
    }
}
