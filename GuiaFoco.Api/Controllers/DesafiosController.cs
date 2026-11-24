using GuiaFoco.Api.Data;
using GuiaFoco.Api.Dtos;
using GuiaFoco.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GuiaFoco.Api.Controllers;

[Route("api/desafios")]
public class DesafiosController : ApiBase
{
    public DesafiosController(AppDbContext db) : base(db) { }

    /// <summary>Cards da área da criança (Palavras, Memória "Em breve", Atenção "Em breve").</summary>
    [HttpGet]
    public async Task<ActionResult<List<DesafioDto>>> Listar() =>
        await Db.Desafios.AsNoTracking().OrderBy(d => d.Ordem)
            .Select(d => new DesafioDto(d.Id, d.Tipo, d.Titulo, d.Descricao, d.Emoji, d.Disponivel))
            .ToListAsync();

    /// <summary>
    /// Perguntas do desafio, em ordem aleatória, com as opções embaralhadas.
    /// O campo "correta" NUNCA é enviado ao front.
    /// </summary>
    [HttpGet("{id:int}/perguntas")]
    public async Task<ActionResult<List<PerguntaDto>>> Perguntas(int id, [FromQuery] int? quantidade)
    {
        var desafio = await Db.Desafios.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);
        if (desafio == null) return NaoEncontrado();
        if (!desafio.Disponivel) return Problem(statusCode: 409, title: "Este desafio estará disponível em breve.");

        var perguntas = await Db.Perguntas.AsNoTracking().Include(p => p.Opcoes)
            .Where(p => p.DesafioId == id).ToListAsync();

        var sorteadas = perguntas.OrderBy(_ => Random.Shared.Next());
        if (quantidade is > 0) sorteadas = sorteadas.Take(quantidade.Value).OrderBy(_ => Random.Shared.Next());

        return sorteadas.Select(p => new PerguntaDto(
            p.Id, p.DesafioId, p.Enunciado, p.Pontos,
            p.Opcoes.OrderBy(_ => Random.Shared.Next())
                .Select(o => new OpcaoDto(o.Id, o.Texto, o.Emoji)).ToList()
        )).ToList();
    }

    /// <summary>Registra a resposta da criança, calcula pontos e devolve se acertou.</summary>
    [HttpPost("/api/respostas")]
    public async Task<ActionResult<RespostaResultadoDto>> Responder(ResponderRequest req)
    {
        var crianca = await BuscarCrianca(req.CriancaId);
        if (crianca == null) return NaoEncontrado("Criança não encontrada.");

        var pergunta = await Db.Perguntas.Include(p => p.Opcoes).FirstOrDefaultAsync(p => p.Id == req.PerguntaId);
        if (pergunta == null) return NaoEncontrado("Pergunta não encontrada.");

        var opcao = pergunta.Opcoes.FirstOrDefault(o => o.Id == req.OpcaoId);
        if (opcao == null) return BadRequest(new ProblemDetails { Title = "A opção não pertence a esta pergunta.", Status = 400 });

        int? sessaoId = null;
        if (req.SessaoId != null)
        {
            var valida = await Db.Sessoes.AnyAsync(s => s.Id == req.SessaoId && s.CriancaId == crianca.Id);
            if (!valida) return BadRequest(new ProblemDetails { Title = "Sessão inválida para esta criança.", Status = 400 });
            sessaoId = req.SessaoId;
        }

        var tentativa = await Db.Respostas.CountAsync(r => r.CriancaId == crianca.Id && r.PerguntaId == pergunta.Id) + 1;
        var acertou = opcao.Correta;
        var pontos = acertou ? (tentativa == 1 ? pergunta.Pontos : Math.Max(1, pergunta.Pontos / 2)) : 0;

        Db.Respostas.Add(new RespostaCrianca
        {
            CriancaId = crianca.Id,
            PerguntaId = pergunta.Id,
            OpcaoId = opcao.Id,
            SessaoId = sessaoId,
            Acertou = acertou,
            Tentativa = tentativa,
            TempoRespostaMs = req.TempoRespostaMs,
            PontosGanhos = pontos,
            RespondidoEm = DateTime.UtcNow
        });
        crianca.Pontos += pontos;
        await Db.SaveChangesAsync();

        var correta = pergunta.Opcoes.First(o => o.Correta);
        return new RespostaResultadoDto(acertou, correta.Id, pontos, crianca.Pontos, tentativa);
    }
}
