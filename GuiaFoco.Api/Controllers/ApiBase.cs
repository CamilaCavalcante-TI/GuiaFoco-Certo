using System.Security.Claims;
using GuiaFoco.Api.Data;
using GuiaFoco.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GuiaFoco.Api.Controllers;

[ApiController]
[Authorize]
public abstract class ApiBase : ControllerBase
{
    protected readonly AppDbContext Db;
    protected ApiBase(AppDbContext db) => Db = db;

    protected int ResponsavelId =>
        int.Parse(User.FindFirstValue("sub") ?? throw new UnauthorizedAccessException());

    /// <summary>Busca a criança SOMENTE se pertencer ao responsável autenticado.</summary>
    protected Task<Crianca?> BuscarCrianca(int id) =>
        Db.Criancas.Include(c => c.Configuracao)
            .FirstOrDefaultAsync(c => c.Id == id && c.ResponsavelId == ResponsavelId);

    protected ActionResult NaoEncontrado(string? msg = null) =>
        NotFound(new ProblemDetails { Title = msg ?? "Recurso não encontrado.", Status = 404 });
}
