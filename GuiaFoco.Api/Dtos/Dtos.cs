using System.ComponentModel.DataAnnotations;

namespace GuiaFoco.Api.Dtos;

public static class Regras
{
    public const string Horario = @"^([01]\d|2[0-3]):[0-5]\d$";
}

// ---------- Autenticação / cadastro ----------

/// <summary>Espelha o formulário "Cadastro do responsável" do front.</summary>
public class RegisterRequest
{
    [Required, StringLength(120, MinimumLength = 2)] public string NomeResponsavel { get; set; } = "";
    [Required, EmailAddress, StringLength(180)] public string Email { get; set; } = "";
    [Required, StringLength(100, MinimumLength = 6)] public string Senha { get; set; } = "";

    [Required, StringLength(120, MinimumLength = 2)] public string NomeCrianca { get; set; } = "";
    [Range(3, 12)] public int Idade { get; set; }
    [Required, StringLength(60)] public string Serie { get; set; } = "";
    [Range(0, 24)] public decimal TempoTelaEstimadoHoras { get; set; }

    [Range(5, 720)] public int LimiteDiarioMin { get; set; } = 150;
    [RegularExpression(Regras.Horario, ErrorMessage = "Use o formato HH:mm.")] public string InicioPermitido { get; set; } = "18:00";
    [RegularExpression(Regras.Horario, ErrorMessage = "Use o formato HH:mm.")] public string FimPermitido { get; set; } = "21:00";
    public List<string> Preferencias { get; set; } = new();
    public bool IntervencaoAdaptativa { get; set; } = true;
    [Range(1, 180)] public int IntervirAposMin { get; set; } = 20;
    public bool BloqueioSimulado { get; set; }
}

public class LoginRequest
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required] public string Senha { get; set; } = "";
}

public record ResponsavelDto(int Id, string Nome, string Email);
public record AuthResponse(string Token, DateTime ExpiraEm, ResponsavelDto Responsavel, CriancaDto? Crianca, List<CriancaDto> Criancas);

// ---------- Criança / configurações ----------

public class ConfiguracaoDto
{
    [Range(5, 720)] public int LimiteDiarioMin { get; set; } = 150;
    [RegularExpression(Regras.Horario, ErrorMessage = "Use o formato HH:mm.")] public string InicioPermitido { get; set; } = "18:00";
    [RegularExpression(Regras.Horario, ErrorMessage = "Use o formato HH:mm.")] public string FimPermitido { get; set; } = "21:00";
    public List<string> Preferencias { get; set; } = new();
    public bool IntervencaoAdaptativa { get; set; } = true;
    [Range(1, 180)] public int IntervirAposMin { get; set; } = 20;
    public bool BloqueioSimulado { get; set; }
    public bool AltoContraste { get; set; }
    public bool ModoEscuro { get; set; }
    [Range(80, 200)] public int FontePercentual { get; set; } = 100;
}

public class UpdateCriancaRequest
{
    [Required, StringLength(120, MinimumLength = 2)] public string Nome { get; set; } = "";
    [Range(3, 12)] public int Idade { get; set; }
    [Required, StringLength(60)] public string Serie { get; set; } = "";
    [Range(0, 24)] public decimal TempoTelaEstimadoHoras { get; set; }
}

public class NovaCriancaRequest : UpdateCriancaRequest
{
    public ConfiguracaoDto Configuracao { get; set; } = new();
}

public record CriancaDto(int Id, string Nome, int Idade, string Serie, decimal TempoTelaEstimadoHoras, int Pontos, ConfiguracaoDto Configuracao);

// ---------- Sessões / tempo de tela ----------

public class IniciarSessaoRequest
{
    [Range(1, int.MaxValue)] public int CriancaId { get; set; }
}

public class EncerrarSessaoRequest
{
    public bool Abandonada { get; set; }
}

public record SessaoDto(int Id, int CriancaId, DateTime InicioEm, DateTime? FimEm, double DuracaoMin, bool Abandonada, int IntervencoesExibidas);

public record UsoHojeDto(
    int UsadoMin, int LimiteMin, int PercentualUsado, int RestanteMin, int SessoesHoje,
    int SessaoAtualMin, int SessaoAtualSegundos, int? SessaoAbertaId,
    bool DentroDoHorario, bool LimiteExcedido, bool DeveIntervir, string? MotivoIntervencao);

// ---------- Desafios ----------

public record DesafioDto(int Id, string Tipo, string Titulo, string Descricao, string Emoji, bool Disponivel);
public record OpcaoDto(int Id, string Texto, string? Emoji);
public record PerguntaDto(int Id, int DesafioId, string Enunciado, int Pontos, List<OpcaoDto> Opcoes);

public class ResponderRequest
{
    [Range(1, int.MaxValue)] public int CriancaId { get; set; }
    [Range(1, int.MaxValue)] public int PerguntaId { get; set; }
    [Range(1, int.MaxValue)] public int OpcaoId { get; set; }
    public int? SessaoId { get; set; }
    [Range(0, 600000)] public int TempoRespostaMs { get; set; }
}

public record RespostaResultadoDto(bool Acertou, int OpcaoCorretaId, int PontosGanhos, int PontosTotal, int Tentativa);

// ---------- Painel / desempenho ----------

public record TrilhaDto(string Nivel, int ProgressoPercentual);
public record RecomendacaoDto(string Nivel, string Titulo, string Texto);

public record UsoDispositivoDto(double DuracaoMediaSessaoMin, double SessoesPorDia, string HorarioSessoes, double TempoPorMinigameMin);
public record IndicadoresDesempenhoDto(int TaxaConclusao, int TaxaAbandono, double TentativasMedias, int Acertos, double ErrosConsecutivos, double TempoRespostaSeg);
public record ComportamentoDto(string TrocasFrequentes, int RetornoAposEncerramento, string RespeitoAoLimite, int EvolucaoSemanal, string Preferencias);

public record DesempenhoDto(
    UsoDispositivoDto Uso, IndicadoresDesempenhoDto Desempenho, ComportamentoDto Comportamento,
    int AcertosUltimos7Dias, int VariacaoAcertosSemana, RecomendacaoDto Recomendacao);

public record ResumoDto(
    string Crianca, int TaxaAcertos, int VariacaoAcertosSemana, double SessaoMediaMin, int SessoesHoje,
    int EvolucaoSemanal, int Conclusao, int Abandono, TrilhaDto Trilha, RecomendacaoDto Recomendacao, UsoHojeDto UsoHoje);
