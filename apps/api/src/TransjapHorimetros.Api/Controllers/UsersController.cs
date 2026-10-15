using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransjapHorimetros.Application.Abstractions;
using TransjapHorimetros.Application.Security;

namespace TransjapHorimetros.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize(Policy = Policies.CanManageUsers)]
public sealed class UsersController(IUserService userService) : ControllerBase
{
    /// <summary>Lista todos os usuários corporativos cadastrados.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public Task<IReadOnlyList<UserDto>> GetAll(CancellationToken cancellationToken) =>
        userService.GetAllAsync(cancellationToken);

    /// <summary>Obtém os detalhes de um usuário por identificador.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public Task<UserDto> GetById(Guid id, CancellationToken cancellationToken) =>
        userService.GetByIdAsync(id, cancellationToken);

    /// <summary>Cadastra um novo usuário corporativo atribuindo seus papéis.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserDto>> Create(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var user = await userService.CreateAsync(
            request,
            HttpContext.TraceIdentifier,
            cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
    }

    /// <summary>Atualiza os dados cadastrais do usuário.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public Task<UserDto> Update(
        Guid id,
        [FromBody] UpdateUserRequest request,
        CancellationToken cancellationToken) =>
        userService.UpdateAsync(id, request, HttpContext.TraceIdentifier, cancellationToken);

    /// <summary>Altera os papéis atribuídos ao usuário.</summary>
    [HttpPost("{id:guid}/roles")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public Task<UserDto> UpdateRoles(
        Guid id,
        [FromBody] UpdateUserRolesRequest request,
        CancellationToken cancellationToken) =>
        userService.UpdateRolesAsync(id, request, HttpContext.TraceIdentifier, cancellationToken);

    /// <summary>Ativa um usuário corporativo previamente desativado.</summary>
    [HttpPost("{id:guid}/activate")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public Task<UserDto> Activate(Guid id, CancellationToken cancellationToken) =>
        userService.ActivateAsync(id, HttpContext.TraceIdentifier, cancellationToken);

    /// <summary>Desativa um usuário corporativo e revoga suas sessões ativas sem apagar histórico.</summary>
    [HttpPost("{id:guid}/deactivate")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public Task<UserDto> Deactivate(Guid id, CancellationToken cancellationToken) =>
        userService.DeactivateAsync(id, HttpContext.TraceIdentifier, cancellationToken);
}
