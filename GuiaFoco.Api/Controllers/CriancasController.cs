using GuiaFoco.Api.Data;
using GuiaFoco.Api.Dtos;
using GuiaFoco.Api.Models;
using GuiaFoco.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GuiaFoco.Api.Controllers;

[Route("api/criancas")]
public class CriancasController : ApiBase
{
    private readonly MetricasService _metricas;

    public CriancasController(AppDbContext db, MetricasService metricas) : base(db) => _metricas = metricas;

    [HttpGet]
    public async Task<ActionResult<List<CriancaDto>>> Listar()
    {
        var lista = await Db.Criancas.Include(c => c.Configuracao)
            .Where(c => c.ResponsavelId == ResponsavelId).OrderBy(c => c.Id).ToListAsync();
        return lista.Select(c => c.ToDto()).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<CriancaDto>> Criar(NovaCriancaRequest req)
    {
        var c = new Crianca
        {
            ResponsavelId = ResponsavelId,
            Nome = req.Nome.Trim(),
            Idade = req.Idade,
            Serie = req.Serie.Trim(),
            TempoTelaEstimadoHoras = req.TempoTelaEstimadoHoras,
            Configuracao = new ConfiguracaoCrianca()
        };
        c.Configuracao.Aplicar(req.Configuracao);

        Db.Criancas.Add(c);
        await Db.SaveChangesAsync();
        return CreatedAtAction(nameof(Obter), new { id = c.Id }, c.ToDto());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CriancaDto>> Obter(int id)
    {
        var c = await BuscarCrianca(id);
        return c == null ? NaoEncontrado() : c.ToDto();
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CriancaDto>> Atualizar(int id, UpdateCriancaRequest req)
    {
        var c = await BuscarCrianca(id);
        if (c == null) return NaoEncontrado();

        c.Nome = req.Nome.Trim();
        c.Idade = req.Idade;
        c.Serie = req.Serie.Trim();
        c.TempoTelaEstimadoHoras = req.TempoTelaEstimadoHoras;
        await Db.SaveChangesAsync();
        return c.ToDto();
    }

    /// <summary>Tela Tempo de tela + Configurações (acessibilidade, intervenção, limites).</summary>
    [HttpPut("{id:int}/configuracoes")]
    public async Task<ActionResult<ConfiguracaoDto>> AtualizarConfiguracoes(int id, ConfiguracaoDto req)
    {
        var c = await BuscarCrianca(id);
        if (c == null) return NaoEncontrado();

        c.Configuracao.Aplicar(req);
        await Db.SaveChangesAsync();
        return c.Configuracao.ToDto();
    }

    /// <summary>Tela Início.</summary>
    [HttpGet("{id:int}/resumo")]
    public async Task<ActionResult<ResumoDto>> Resumo(int id)
    {
        var c = await BuscarCrianca(id);
        return c == null ? NaoEncontrado() : await _metricas.ResumoAsync(c);
    }

    /// <summary>Tela Desempenho (uso, desempenho, comportamento e recomendação).</summary>
    [HttpGet("{id:int}/desempenho")]
    public async Task<ActionResult<DesempenhoDto>> Desempenho(int id)
    {
        var c = await BuscarCrianca(id);
        return c == null ? NaoEncontrado() : await _metricas.DesempenhoAsync(c);
    }

    /// <summary>Tela Tempo de tela: uso do dia, limite e se deve exibir o aviso de pausa.</summary>
    [HttpGet("{id:int}/uso-hoje")]
    public async Task<ActionResult<UsoHojeDto>> UsoHoje(int id)
    {
        var c = await BuscarCrianca(id);
        return c == null ? NaoEncontrado() : await _metricas.UsoHojeAsync(c);
    }

    [HttpGet("{id:int}/recomendacao")]
    public async Task<ActionResult<RecomendacaoDto>> Recomendacao(int id)
    {
        var c = await BuscarCrianca(id);
        return c == null ? NaoEncontrado() : await _metricas.RecomendacaoAsync(c);
    }
}
