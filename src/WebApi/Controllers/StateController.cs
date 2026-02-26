using System.Text.Json;
using Devpro.TerraformBackend.Domain.Models;
using Devpro.TerraformBackend.Domain.Repositories;
using Devpro.TerraformBackend.WebApi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Devpro.TerraformBackend.WebApi.Controllers;

[Authorize]
[ApiController]
[Route("{tenant}/state/{name:regex([[a-zA-Z]]+)}")]
[TypeFilter(typeof(TenantAuthorizationFilter))]
public class StateController(IStateRepository stateRepository, IStateLockRepository stateLockRepository)
    : ControllerBase
{
    /// <summary>
    /// Get Terraform state value.
    /// GET /:tenant/state/:name?ID=:lockId
    /// </summary>
    /// <param name="tenant"></param>
    /// <param name="name">The name of the Terraform state</param>
    /// <returns>Raw string</returns>
    [HttpGet("", Name = "GetState")]
    [Produces("text/plain")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> FindOne(string tenant, string name)
    {
        var state = await stateRepository.FindOneAsync(tenant, name);
        if (string.IsNullOrEmpty(state))
        {
            return NotFound();
        }

        return Ok(state);
    }

    /// <summary>
    /// Create Terraform state.
    /// POST /:tenant/state/:name?ID=:lockId
    /// </summary>
    /// <param name="tenant"></param>
    /// <param name="name">The name of the Terraform state</param>
    /// <param name="input"></param>
    /// <param name="lockId">Terraform state lock ID</param>
    /// <returns></returns>
    [HttpPost("", Name = "CreateState")]
    [Consumes("application/json", "text/json")]
    [ProducesResponseType(200)]
    [ProducesResponseType(409)]
    [ProducesResponseType(423)]
    public async Task<IActionResult> Create(string tenant, string name, [FromBody] object input, [FromQuery(Name = "ID")] string? lockId = "")
    {
        var existingLock = await stateLockRepository.FindOneAsync(tenant, name);
        if (existingLock != null && string.IsNullOrEmpty(lockId)) return StatusCode(423, new { Message = "The state is locked." });
        if (existingLock != null && existingLock.Id != lockId) return Conflict(existingLock);

        var jsonInput = JsonSerializer.Serialize(input);
        await stateRepository.CreateAsync(tenant, name, jsonInput);
        return Ok();
    }

    /// <summary>
    /// DELETE /:tenant/state/:name?ID=:lockId
    /// </summary>
    /// <param name="tenant"></param>
    /// <param name="name"></param>
    /// <param name="lockId">Terraform state lock ID</param>
    /// <returns></returns>
    [HttpDelete("", Name = "DeleteState")]
    [ProducesResponseType(200)]
    [ProducesResponseType(409)]
    [ProducesResponseType(423)]
    public async Task<IActionResult> Delete(string tenant, string name, [FromQuery(Name = "ID")] string? lockId = "")
    {
        var existingLock = await stateLockRepository.FindOneAsync(tenant, name);
        if (existingLock != null && string.IsNullOrEmpty(lockId)) return StatusCode(423, new { Message = "The state is locked." });
        if (existingLock != null && existingLock.Id != lockId) return Conflict(existingLock);

        await stateRepository.DeleteAsync(tenant, name);
        return Ok();
    }

    /// <summary>
    /// POST /:tenant/state/:name/lock
    /// </summary>
    /// <param name="tenant"></param>
    /// <param name="name"></param>
    /// <param name="input"></param>
    /// <returns></returns>
    [HttpPost("lock", Name = "CreateStateLock")]
    [Consumes("application/json", "text/json")]
    [Produces("application/json")]
    [ProducesResponseType(200)]
    [ProducesResponseType(409)]
    [ProducesResponseType(423)]
    public async Task<IActionResult> Lock(string tenant, string name, StateLockModel input)
    {
        var existingLock = await stateLockRepository.FindOneAsync(tenant, name);
        if (existingLock != null && string.IsNullOrEmpty(input.Id)) return StatusCode(423, new { Message = "The state is locked." });
        if (existingLock != null && existingLock.Id != input.Id) return Conflict(existingLock);
        if (existingLock != null) return Ok(existingLock);

        input.Tenant = tenant;
        input.Name = name;
        var entry = await stateLockRepository.CreateAsync(input);
        return Ok(entry);
    }

    /// <summary>
    /// DELETE /:tenant/state/:name/lock
    /// </summary>
    /// <param name="tenant"></param>
    /// <param name="name"></param>
    /// <param name="input"></param>
    /// <returns></returns>
    [HttpDelete("lock", Name = "DeleteStateLock")]
    [Consumes("application/json", "text/json")]
    [Produces("application/json")]
    [ProducesResponseType(200)]
    [ProducesResponseType(409)]
    [ProducesResponseType(423)]
    public async Task<IActionResult> Unlock(string tenant, string name, [FromBody] StateLockModel input)
    {
        var existingLock = await stateLockRepository.FindOneAsync(tenant, name);
        if (existingLock == null) return Ok();
        if (string.IsNullOrEmpty(input.Id)) return StatusCode(423, new { Message = "The state is locked." });
        if (existingLock.Id != input.Id) return Conflict(existingLock);

        input.Tenant = tenant;
        input.Name = name;
        await stateLockRepository.DeleteAsync(input);
        return Ok();
    }
}
