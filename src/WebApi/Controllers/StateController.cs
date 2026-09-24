using System.Text.Json;
using Devpro.TerraformBackend.Domain.Exceptions;
using Devpro.TerraformBackend.Domain.Models;
using Devpro.TerraformBackend.Domain.Repositories;
using Devpro.TerraformBackend.WebApi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Devpro.TerraformBackend.WebApi.Controllers;

[Authorize]
[ApiController]
[Route("{tenant}/state/{name}")]
[TypeFilter(typeof(TenantAuthorizationFilter))]
public class StateController(IStateRepository stateRepository, IStateLockRepository stateLockRepository)
    : ControllerBase
{
    private const string MessageStateIsLocked = "The state is locked.";

    /// <summary>
    /// GET /:tenant/state/:name, answering the raw state JSON.
    /// </summary>
    [HttpGet("", Name = "GetState")]
    [Produces("text/plain")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> FindOne(string tenant, string name, CancellationToken cancellationToken)
    {
        var state = await stateRepository.FindOneAsync(tenant, name, cancellationToken);
        if (string.IsNullOrEmpty(state))
        {
            return NotFound();
        }

        return Ok(state);
    }

    /// <summary>
    /// POST /:tenant/state/:name?ID=:lockId, an upsert.
    /// </summary>
    [HttpPost("", Name = "CreateState")]
    [Consumes("application/json", "text/json")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    [ProducesResponseType(413)]
    [ProducesResponseType(423)]
    public async Task<IActionResult> Create(string tenant, string name, [FromBody] object input, CancellationToken cancellationToken, [FromQuery(Name = "ID")] string? lockId = "")
    {
        var existingLock = await stateLockRepository.FindOneAsync(tenant, name, cancellationToken);
        if (existingLock != null && string.IsNullOrEmpty(lockId)) return StatusCode(423, new { Message = MessageStateIsLocked });
        if (existingLock != null && existingLock.Id != lockId) return Conflict(existingLock);

        var jsonInput = JsonSerializer.Serialize(input);

        try
        {
            await stateRepository.CreateAsync(tenant, name, jsonInput, cancellationToken);
        }
        catch (StateTooLargeException exception)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge, new { Message = exception.Message });
        }
        catch (JsonException exception)
        {
            return BadRequest(new { Message = exception.Message });
        }

        return Ok();
    }

    /// <summary>
    /// DELETE /:tenant/state/:name?ID=:lockId
    /// </summary>
    [HttpDelete("", Name = "DeleteState")]
    [ProducesResponseType(200)]
    [ProducesResponseType(409)]
    [ProducesResponseType(423)]
    public async Task<IActionResult> Delete(string tenant, string name, CancellationToken cancellationToken, [FromQuery(Name = "ID")] string? lockId = "")
    {
        var existingLock = await stateLockRepository.FindOneAsync(tenant, name, cancellationToken);
        if (existingLock != null && string.IsNullOrEmpty(lockId)) return StatusCode(423, new { Message = MessageStateIsLocked });
        if (existingLock != null && existingLock.Id != lockId) return Conflict(existingLock);

        await stateRepository.DeleteAsync(tenant, name, cancellationToken);
        return Ok();
    }

    /// <summary>
    /// POST /:tenant/state/:name/lock
    /// </summary>
    [HttpPost("lock", Name = "CreateStateLock")]
    [Consumes("application/json", "text/json")]
    [Produces("application/json")]
    [ProducesResponseType(200)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Lock(string tenant, string name, StateLockModel input, CancellationToken cancellationToken)
    {
        var existingLock = await stateLockRepository.FindOneAsync(tenant, name, cancellationToken);
        if (existingLock != null && existingLock.Id != input.Id) return Conflict(existingLock);
        if (existingLock != null) return Ok(existingLock);

        input.Tenant = tenant;
        input.Name = name;
        var entry = await stateLockRepository.CreateAsync(input, cancellationToken);
        if (entry == null)
        {
            // another run acquired the lock between the check above and the insert
            var concurrentLock = await stateLockRepository.FindOneAsync(tenant, name, cancellationToken);
            return Conflict(concurrentLock ?? input);
        }

        return Ok(entry);
    }

    /// <summary>
    /// DELETE /:tenant/state/:name/lock
    /// </summary>
    [HttpDelete("lock", Name = "DeleteStateLock")]
    [Consumes("application/json", "text/json")]
    [Produces("application/json")]
    [ProducesResponseType(200)]
    [ProducesResponseType(409)]
    [ProducesResponseType(423)]
    public async Task<IActionResult> Unlock(string tenant, string name, [FromBody] StateLockModel input, CancellationToken cancellationToken)
    {
        var existingLock = await stateLockRepository.FindOneAsync(tenant, name, cancellationToken);
        if (existingLock == null) return Ok();
        if (string.IsNullOrEmpty(input.Id)) return StatusCode(423, new { Message = MessageStateIsLocked });
        if (existingLock.Id != input.Id) return Conflict(existingLock);

        input.Tenant = tenant;
        input.Name = name;
        await stateLockRepository.DeleteAsync(input, cancellationToken);
        return Ok();
    }
}
