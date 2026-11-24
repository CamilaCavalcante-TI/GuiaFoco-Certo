using System.Globalization;
using GuiaFoco.Api.Data;
using GuiaFoco.Api.Dtos;
using GuiaFoco.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GuiaFoco.Api.Services;

/// <summary>
/// Calcula os indicadores exibidos no painel do responsável a partir das
/// sessões e respostas gravadas. A "recomendação adaptativa" aqui é baseada
/// em regras; no futuro pode ser substituída por um modelo de IA.
/// </summary>
public class MetricasService
{
    private const int MaxSessaoAbertaMin = 180;
    private static readonly CultureInfo Pt = new("pt-BR");

    private readonly AppDbContext _db;
    private readonly RelogioLocal _relogio;

    public MetricasService(AppDbContext db, RelogioLocal relogio)
    {
        _db = db;
        _relogio = relogio;
    }

    private async Task<ConfiguracaoCrianca> Cfg(Crianca c)
    {
        if (c.Configuracao != null) return c.Configuracao;
        return await _db.Configuracoes.AsNoTracking().FirstAsync(x => x.CriancaId == c.Id);
    }

    // ------------------------------------------------------------------
    // Tempo de tela
    // ------------------------------------------------------------------
    public async Task<UsoHojeDto> UsoHojeAsync(Crianca c)
    {
        var cfg = await Cfg(c);
        var agora = DateTime.UtcNow;
        var inicioDia = _relogio.InicioDoDiaUtc();

        var sessoes = await _db.Sessoes.AsNoTracking()
            .Where(s => s.CriancaId == c.Id && (s.FimEm == null || s.FimEm >= inicioDia))
            .ToListAsync();

        double usado = 0, atual = 0;
        int? abertaId = null;
        var hoje = 0;

        foreach (var s in sessoes)
        {
            var emAberto = s.FimEm == null;
            if (emAberto && (agora - s.InicioEm).TotalMinutes > MaxSessaoAbertaMin) continue; // sessão esquecida

            var ini = s.InicioEm < inicioDia ? inicioDia : s.InicioEm;
            var fim = s.FimEm ?? agora;
            if (fim > ini) usado += (fim - ini).TotalMinutes;
            if (s.InicioEm >= inicioDia) hoje++;
            if (emAberto)
            {
                atual = (agora - s.InicioEm).TotalMinutes;
                abertaId = s.Id;
            }
        }

        var usadoMin = (int)Math.Round(usado);
        var limite = cfg.LimiteDiarioMin;
        var dentro = DentroDoHorario(cfg);
        var excedeu = usadoMin >= limite;

        string? motivo = null;
        if (abertaId != null)
        {
            if (excedeu) motivo = "limite_diario";
            else if (!dentro) motivo = "fora_do_horario";
            else if (cfg.IntervencaoAdaptativa && atual >= cfg.IntervirAposMin) motivo = "tempo_sessao";
        }

        return new UsoHojeDto(
            UsadoMin: usadoMin,
            LimiteMin: limite,
            PercentualUsado: limite > 0 ? Math.Min(100, (int)Math.Round(usado * 100.0 / limite)) : 0,
            RestanteMin: Math.Max(0, limite - usadoMin),
            SessoesHoje: hoje,
            SessaoAtualMin: (int)atual,
            SessaoAtualSegundos: (int)(atual * 60),
            SessaoAbertaId: abertaId,
            DentroDoHorario: dentro,
            LimiteExcedido: excedeu,
            DeveIntervir: motivo != null,
            MotivoIntervencao: motivo);
    }

    private bool DentroDoHorario(ConfiguracaoCrianca cfg)
    {
        var agora = TimeOnly.FromDateTime(_relogio.AgoraLocal);
        var ini = TimeOnly.Parse(cfg.InicioPermitido, CultureInfo.InvariantCulture);
        var fim = TimeOnly.Parse(cfg.FimPermitido, CultureInfo.InvariantCulture);
        return ini <= fim ? agora >= ini && agora <= fim : agora >= ini || agora <= fim;
    }

    // ------------------------------------------------------------------
    // Trilha e recomendação
    // ------------------------------------------------------------------
    public async Task<int> ProgressoTrilhaAsync(int criancaId)
    {
        var total = await _db.Perguntas.CountAsync(p => p.Desafio.Disponivel);
        if (total == 0) return 0;
        var acertadas = await _db.Respostas
            .Where(r => r.CriancaId == criancaId && r.Acertou)
            .Select(r => r.PerguntaId).Distinct().CountAsync();
        return Math.Min(100, (int)Math.Round(acertadas * 100.0 / total));
    }

    public async Task<RecomendacaoDto> RecomendacaoAsync(Crianca c)
    {
        var ult = await _db.Respostas.AsNoTracking()
            .Where(r => r.CriancaId == c.Id)
            .OrderByDescending(r => r.RespondidoEm)
            .Take(20).ToListAsync();

        if (ult.Count < 5)
            return new RecomendacaoDto("Inicial", "Começar com atividades curtas",
                "Ainda há poucos dados de desempenho. Comece com atividades curtas de letras e palavras para calibrar o nível da criança.");

        var taxa = ult.Count(r => r.Acertou) * 100.0 / ult.Count;
        var tempo = ult.Average(r => r.TempoRespostaMs) / 1000.0;
        var maxErros = Sequencias(ult.OrderBy(r => r.RespondidoEm)).DefaultIfEmpty(0).Max();

        var t = taxa.ToString("0", Pt);
        var s = tempo.ToString("0.0", Pt);

        if (taxa < 50 || maxErros >= 3)
            return new RecomendacaoDto("Inicial", "Reduzir a dificuldade",
                $"O desempenho recente mostra {t}% de acertos e até {maxErros} erros seguidos. Priorize atividades curtas de letras e vogais, com reforço positivo, antes de avançar.");

        if (taxa >= 90 && tempo <= 2.5)
            return new RecomendacaoDto("Avançado", "Aumentar a dificuldade",
                $"Com {t}% de acertos e resposta em {s} s, a criança está pronta para palavras mais longas e novos tipos de atividade.");

        return new RecomendacaoDto("Intermediário", "Manter dificuldade intermediária",
            $"O desempenho atual apresenta {t}% de acertos e tempo médio de resposta de {s} s. Para a próxima sessão, priorizar atividades curtas de palavras e memória, monitorando erros consecutivos.");
    }

    // ------------------------------------------------------------------
    // Desempenho e comportamento (últimos 30 dias)
    // ------------------------------------------------------------------
    public async Task<DesempenhoDto> DesempenhoAsync(Crianca c)
    {
        var cfg = await Cfg(c);
        var agora = DateTime.UtcNow;
        var desde30 = agora.AddDays(-30);
        var desde14 = agora.AddDays(-14);
        var desde7 = agora.AddDays(-7);

        var sessoes = await _db.Sessoes.AsNoTracking()
            .Where(s => s.CriancaId == c.Id && s.InicioEm >= desde30)
            .OrderBy(s => s.InicioEm).ToListAsync();

        var respostas = await _db.Respostas.AsNoTracking()
            .Include(r => r.Pergunta)
            .Where(r => r.CriancaId == c.Id && r.RespondidoEm >= desde30)
            .OrderBy(r => r.RespondidoEm).ToListAsync();

        var fechadas = sessoes.Where(s => s.FimEm != null).ToList();

        // Uso do dispositivo
        var duracoes = fechadas.Select(s => (s.FimEm!.Value - s.InicioEm).TotalMinutes).ToList();
        var duracaoMedia = duracoes.Count > 0 ? duracoes.Average() : 0;

        var diasComSessao = sessoes.Select(s => _relogio.ParaLocal(s.InicioEm).Date).Distinct().Count();
        var sessoesPorDia = diasComSessao > 0 ? (double)sessoes.Count / diasComSessao : 0;

        var horario = "—";
        if (sessoes.Count > 0)
        {
            var horas = sessoes.Select(s => _relogio.ParaLocal(s.InicioEm).Hour).ToList();
            horario = $"{horas.Min()}h–{horas.Max() + 1}h";
        }

        var porJogo = respostas
            .GroupBy(r => (r.SessaoId, r.Pergunta.DesafioId))
            .Select(g => g.Sum(x => x.TempoRespostaMs) / 60000.0).ToList();
        var tempoMinigame = porJogo.Count > 0 ? porJogo.Average() : 0;

        // Desempenho
        var conclusao = fechadas.Count > 0
            ? (int)Math.Round(fechadas.Count(s => !s.Abandonada) * 100.0 / fechadas.Count) : 0;
        var abandono = fechadas.Count > 0 ? 100 - conclusao : 0;
        var tentativas = respostas.Count > 0 ? respostas.Average(r => r.Tentativa) : 0;
        var acertosGeral = Taxa(respostas);
        var sequencias = Sequencias(respostas);
        var errosConsec = sequencias.Count > 0 ? sequencias.Average() : 0;
        var tempoResp = respostas.Count > 0 ? respostas.Average(r => r.TempoRespostaMs) / 1000.0 : 0;

        // Semana atual x anterior
        var sem = respostas.Where(r => r.RespondidoEm >= desde7).ToList();
        var ant = respostas.Where(r => r.RespondidoEm >= desde14 && r.RespondidoEm < desde7).ToList();
        var acertos7 = Taxa(sem);
        var variacao = ant.Count > 0 && sem.Count > 0 ? acertos7 - Taxa(ant) : 0;
        var corretasAnt = ant.Count(r => r.Acertou);
        var evolucao = corretasAnt > 0
            ? Math.Clamp((int)Math.Round((sem.Count(r => r.Acertou) - corretasAnt) * 100.0 / corretasAnt), -999, 999)
            : 0;

        // Comportamento
        var desafiosPorSessao = respostas.Where(r => r.SessaoId != null)
            .GroupBy(r => r.SessaoId)
            .Select(g => g.Select(x => x.Pergunta.DesafioId).Distinct().Count()).ToList();
        var mediaTrocas = desafiosPorSessao.Count > 0 ? desafiosPorSessao.Average() : 0;
        var trocas = mediaTrocas >= 3 ? "Alta" : mediaTrocas >= 2 ? "Média" : "Baixa";

        var recentes = sessoes.Where(s => s.InicioEm >= desde7).ToList();
        var retornos = 0;
        for (var i = 1; i < recentes.Count; i++)
        {
            var prev = recentes[i - 1];
            if (prev.FimEm != null && (recentes[i].InicioEm - prev.FimEm.Value).TotalMinutes <= 10) retornos++;
        }

        var porDia = fechadas.Where(s => s.InicioEm >= desde7)
            .GroupBy(s => _relogio.ParaLocal(s.InicioEm).Date)
            .Select(g => g.Sum(s => (s.FimEm!.Value - s.InicioEm).TotalMinutes)).ToList();
        var respeito = "Sem dados";
        if (porDia.Count > 0)
        {
            var ok = porDia.Count(m => m <= cfg.LimiteDiarioMin) * 100.0 / porDia.Count;
            respeito = ok >= 90 ? "Alto" : ok >= 60 ? "Médio" : "Baixo";
        }

        var prefs = Mapeamento.ParsePreferencias(cfg.Preferencias);
        var prefsTxt = prefs.Count > 0 ? string.Join(" > ", prefs) : "—";

        var rec = await RecomendacaoAsync(c);

        return new DesempenhoDto(
            new UsoDispositivoDto(Math.Round(duracaoMedia, 1), Math.Round(sessoesPorDia, 1), horario, Math.Round(tempoMinigame, 1)),
            new IndicadoresDesempenhoDto(conclusao, abandono, Math.Round(tentativas, 1), acertosGeral, Math.Round(errosConsec, 1), Math.Round(tempoResp, 1)),
            new ComportamentoDto(trocas, retornos, respeito, evolucao, prefsTxt),
            acertos7, variacao, rec);
    }

    public async Task<ResumoDto> ResumoAsync(Crianca c)
    {
        var d = await DesempenhoAsync(c);
        var uso = await UsoHojeAsync(c);
        var progresso = await ProgressoTrilhaAsync(c.Id);

        return new ResumoDto(
            Crianca: c.Nome,
            TaxaAcertos: d.AcertosUltimos7Dias,
            VariacaoAcertosSemana: d.VariacaoAcertosSemana,
            SessaoMediaMin: d.Uso.DuracaoMediaSessaoMin,
            SessoesHoje: uso.SessoesHoje,
            EvolucaoSemanal: d.Comportamento.EvolucaoSemanal,
            Conclusao: d.Desempenho.TaxaConclusao,
            Abandono: d.Desempenho.TaxaAbandono,
            Trilha: new TrilhaDto(d.Recomendacao.Nivel, progresso),
            Recomendacao: d.Recomendacao,
            UsoHoje: uso);
    }

    // ------------------------------------------------------------------
    // Auxiliares
    // ------------------------------------------------------------------
    private static int Taxa(IReadOnlyCollection<RespostaCrianca> lista) =>
        lista.Count == 0 ? 0 : (int)Math.Round(lista.Count(r => r.Acertou) * 100.0 / lista.Count);

    /// <summary>Tamanho de cada sequência de erros consecutivos (lista em ordem cronológica).</summary>
    private static List<int> Sequencias(IEnumerable<RespostaCrianca> ordenadas)
    {
        var lista = new List<int>();
        var n = 0;
        foreach (var r in ordenadas)
        {
            if (!r.Acertou) n++;
            else
            {
                if (n > 0) lista.Add(n);
                n = 0;
            }
        }
        if (n > 0) lista.Add(n);
        return lista;
    }
}
