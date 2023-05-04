using System.Text.Json;
using Devpro.TerraformBackend.Domain.Models;
using Devpro.TerraformBackend.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Devpro.TerraformBackend.WebApi.Controllers
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

        /// <summary>
        /// Get Terraform state value.
        /// </summary>
        /// <param name="name">The name of the Terraform state</param>
        /// <param name="lockId">Terraform state lock ID</param>
        /// <returns>Raw string</returns>
        [HttpGet("{name}", Name = "GetState")]
        [ProducesResponseType(200)]
        public async Task<string> FindOne(string name, [FromQuery(Name = "ID")] string? lockId = "")
        {
            //TODO: check lock
            return await _stateRepository.FindOneAsync(name);
        }

        /// <summary>
        /// Get Terraform state value.
        /// </summary>
        /// <param name="name">The name of the Terraform state</param>
        /// <param name="lockId">Terraform state lock ID</param>
        /// <returns></returns>
        [HttpPost("{name}", Name = "CreateState")]
        [ProducesResponseType(201)]
        [Consumes("application/json", "text/json")]
        public async Task Create(string name, [FromBody] object input, [FromQuery(Name = "ID")] string? lockId = "")
        {
            //TODO: check lock
            var jsonInput = JsonSerializer.Serialize(input);
            await _stateRepository.CreateAsync(name, jsonInput);
        }

        [HttpGet("/locks", Name = "GetStateLocks")]
        [ProducesResponseType(200)]
        public async Task<List<StateLockModel>> FindAllLocks([FromQuery] string? name = "")
        {
            //TODO: only for admins
            return await _stateLockRepository.FindAllAsync();
        }

        [HttpPost("{name}/lock", Name = "CreateStateLock")]
        [ProducesResponseType(201)]
        [Consumes("application/json", "text/json")]
        [Produces("application/json")]
        public async Task Lock(string name, StateLockModel input)
        {
            input.Name = name;
            await _stateLockRepository.CreateAsync(input);
        }

        [HttpDelete("{name}/lock", Name = "DeleteStateLock")]
        [ProducesResponseType(204)]
        [Consumes("application/json", "text/json")]
        [Produces("application/json")]
        public async Task Unlock(string name, [FromBody] StateLockModel input)
        {
            input.Name = name;
            await _stateLockRepository.DeleteAsync(input);
        }
    }
}
