using GuiaFoco.Api.Data;
using GuiaFoco.Api.Dtos;
using GuiaFoco.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GuiaFoco.Api.Controllers;

[Route("api/sessoes")]
public class SessoesController : ApiBase
{
    private const int MaxSessaoAbertaMin = 180;

    public SessoesController(AppDbContext db) : base(db) { }

    /// <summary>
    /// Inicia uma sessão de uso. Se já existir uma sessão aberta recente, ela é reaproveitada
    /// (idempotente); sessões abertas "esquecidas" são encerradas como abandonadas.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<SessaoDto>> Iniciar(IniciarSessaoRequest req)
    {
        var crianca = await BuscarCrianca(req.CriancaId);
        if (crianca == null) return NaoEncontrado();

        var agora = DateTime.UtcNow;
        var abertas = await Db.Sessoes.Where(s => s.CriancaId == crianca.Id && s.FimEm == null).ToListAsync();
        var vigente = abertas.FirstOrDefault(s => (agora - s.InicioEm).TotalMinutes <= MaxSessaoAbertaMin);

        foreach (var antiga in abertas.Where(s => s != vigente))
        {
            antiga.FimEm = antiga.InicioEm.AddMinutes(Math.Min(30, (agora - antiga.InicioEm).TotalMinutes));
            antiga.Abandonada = true;
        }

        if (vigente == null)
        {
            vigente = new SessaoUso { CriancaId = crianca.Id, InicioEm = agora };
            Db.Sessoes.Add(vigente);
        }

        await Db.SaveChangesAsync();
        return vigente.ToDto();
    }

    /// <summary>Botão "Encerrar sessão" (abandonada = true se a criança saiu sem concluir).</summary>
    [HttpPatch("{id:int}/encerrar")]
    public async Task<ActionResult<SessaoDto>> Encerrar(int id, EncerrarSessaoRequest? req)
    {
        var s = await BuscarSessao(id);
        if (s == null) return NaoEncontrado();

        if (s.FimEm == null)
        {
            s.FimEm = DateTime.UtcNow;
            s.Abandonada = req?.Abandonada ?? false;
            await Db.SaveChangesAsync();
        }
        return s.ToDto();
    }

    /// <summary>Registra que o aviso "Hora de uma pausa!" foi exibido.</summary>
    [HttpPost("{id:int}/intervencao")]
    public async Task<ActionResult<SessaoDto>> RegistrarIntervencao(int id)
    {
        var s = await BuscarSessao(id);
        if (s == null) return NaoEncontrado();

        s.IntervencoesExibidas++;
        await Db.SaveChangesAsync();
        return s.ToDto();
    }

    private Task<SessaoUso?> BuscarSessao(int id) =>
        Db.Sessoes.Include(s => s.Crianca)
            .FirstOrDefaultAsync(s => s.Id == id && s.Crianca.ResponsavelId == ResponsavelId);
}
