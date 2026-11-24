using GuiaFoco.Api.Models;

namespace GuiaFoco.Api.Dtos;

public static class Mapeamento
{
    public static List<string> ParsePreferencias(string? s) =>
        string.IsNullOrWhiteSpace(s)
            ? new List<string>()
            : s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    public static string JoinPreferencias(IEnumerable<string>? lista) =>
        string.Join(",",
            (lista ?? Enumerable.Empty<string>())
                .Select(x => x.Trim().Replace(",", " "))
                .Where(x => x.Length > 0)
                .Distinct()
                .Take(10));

    public static ConfiguracaoDto ToDto(this ConfiguracaoCrianca c) => new()
    {
        LimiteDiarioMin = c.LimiteDiarioMin,
        InicioPermitido = c.InicioPermitido,
        FimPermitido = c.FimPermitido,
        Preferencias = ParsePreferencias(c.Preferencias),
        IntervencaoAdaptativa = c.IntervencaoAdaptativa,
        IntervirAposMin = c.IntervirAposMin,
        BloqueioSimulado = c.BloqueioSimulado,
        AltoContraste = c.AltoContraste,
        ModoEscuro = c.ModoEscuro,
        FontePercentual = c.FontePercentual
    };

    public static void Aplicar(this ConfiguracaoCrianca c, ConfiguracaoDto d)
    {
        c.LimiteDiarioMin = d.LimiteDiarioMin;
        c.InicioPermitido = d.InicioPermitido;
        c.FimPermitido = d.FimPermitido;
        c.Preferencias = JoinPreferencias(d.Preferencias);
        c.IntervencaoAdaptativa = d.IntervencaoAdaptativa;
        c.IntervirAposMin = d.IntervirAposMin;
        c.BloqueioSimulado = d.BloqueioSimulado;
        c.AltoContraste = d.AltoContraste;
        c.ModoEscuro = d.ModoEscuro;
        c.FontePercentual = d.FontePercentual;
    }

    public static CriancaDto ToDto(this Crianca c) =>
        new(c.Id, c.Nome, c.Idade, c.Serie, c.TempoTelaEstimadoHoras, c.Pontos, c.Configuracao.ToDto());

    public static SessaoDto ToDto(this SessaoUso s) =>
        new(s.Id, s.CriancaId, s.InicioEm, s.FimEm,
            Math.Round(((s.FimEm ?? DateTime.UtcNow) - s.InicioEm).TotalMinutes, 1),
            s.Abandonada, s.IntervencoesExibidas);
}
