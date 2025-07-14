using System.Text.Json;
using Devpro.TerraformBackend.Domain.Models;
using Devpro.TerraformBackend.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Devpro.TerraformBackend.WebApi.Controllers;

[Authorize]
[ApiController]
[Route("state")]
public class StateController(IStateRepository stateRepository, IStateLockRepository stateLockRepository) : ControllerBase
{
    /// <summary>
    /// Get Terraform state value.
    /// GET /state/:name?ID=:lockId
    /// </summary>
    /// <param name="name">The name of the Terraform state</param>
    /// <returns>Raw string</returns>
    [HttpGet("{name:regex([[a-zA-Z]]+)}", Name = "GetState")]
    [Produces("text/plain")]
    [ProducesResponseType(200)]
    [ProducesResponseType(204)]
    public async Task<IActionResult> FindOne(string name)
    {
        var state = await stateRepository.FindOneAsync(name);
        if (string.IsNullOrEmpty(state))
        {
            return NoContent();
        }

        return Ok(state);
    }

    /// <summary>
    /// Get Terraform state value.
    /// POST /state/:name?ID=:lockId
    /// </summary>
    /// <param name="name">The name of the Terraform state</param>
    /// <param name="lockId">Terraform state lock ID</param>
    /// <returns></returns>
    [HttpPost("{name:regex([[a-zA-Z]]+)}", Name = "CreateState")]
    [Consumes("application/json", "text/json")]
    [ProducesResponseType(200)]
    [ProducesResponseType(409)]
    [ProducesResponseType(423)]
    public async Task<IActionResult> Create(string name, [FromBody] object input, [FromQuery(Name = "ID")] string? lockId = "")
    {
        if (await CheckLock(name, lockId) is { } lockResult) return lockResult;

        var jsonInput = JsonSerializer.Serialize(input);
        await stateRepository.CreateAsync(name, jsonInput);
        return Ok();
    }

    /// <summary>
    /// DELETE /state/:name?ID=:lockId
    /// </summary>
    /// <param name="name"></param>
    /// <param name="lockId">Terraform state lock ID</param>
    /// <returns></returns>
    [HttpDelete("{name:regex([[a-zA-Z]]+)}", Name = "DeleteState")]
    [ProducesResponseType(200)]
    [ProducesResponseType(409)]
    [ProducesResponseType(423)]
    public async Task<IActionResult> Delete(string name, [FromQuery(Name = "ID")] string? lockId = "")
    {
        if (await CheckLock(name, lockId) is { } lockResult) return lockResult;

        await stateRepository.DeleteAsync(name);
        return Ok();
    }

    /// <summary>
    /// POST /state/:name/lock
    /// </summary>
    /// <param name="name"></param>
    /// <param name="input"></param>
    /// <returns></returns>
    [HttpPost("{name:regex([[a-zA-Z]]+)}/lock", Name = "CreateStateLock")]
    [Consumes("application/json", "text/json")]
    [Produces("application/json")]
    [ProducesResponseType(200)]
    [ProducesResponseType(409)]
    [ProducesResponseType(423)]
    public async Task<IActionResult> Lock(string name, StateLockModel input)
    {
        if (await CheckLock(name, input.Id) is { } lockResult) return lockResult;

        input.Name = name;
        var entry = await stateLockRepository.CreateAsync(input);
        return Ok(entry);
    }

    /// <summary>
    /// DELETE /state/:name/lock
    /// </summary>
    /// <param name="name"></param>
    /// <param name="input"></param>
    /// <returns></returns>
    [HttpDelete("{name:regex([[a-zA-Z]]+)}/lock", Name = "DeleteStateLock")]
    [ProducesResponseType(200)]
    [Consumes("application/json", "text/json")]
    [Produces("application/json")]
    public async Task<IActionResult> Unlock(string name, [FromBody] StateLockModel input)
    {
        input.Name = name;
        await stateLockRepository.DeleteAsync(input);
        return Ok();
    }

    private async Task<ObjectResult?> CheckLock(string name, string? lockId = "")
    {
        var existingLock = await stateLockRepository.FindOneAsync(name);
        if (existingLock != null)
        {
            if (string.IsNullOrEmpty(lockId))
            {
                return StatusCode(423, new { Message = "The state is locked." });
            }

            if (existingLock.Id != lockId)
            {
                return Conflict("LockId doesn't match with the existing lock");
            }
        }
        return null;
    }
}
