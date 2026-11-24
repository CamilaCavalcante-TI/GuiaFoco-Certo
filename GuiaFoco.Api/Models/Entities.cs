namespace GuiaFoco.Api.Models;

public class Responsavel
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public string Email { get; set; } = "";
    public string SenhaHash { get; set; } = "";
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public List<Crianca> Criancas { get; set; } = new();
}

public class Crianca
{
    public int Id { get; set; }
    public int ResponsavelId { get; set; }
    public string Nome { get; set; } = "";
    public int Idade { get; set; }
    public string Serie { get; set; } = "";
    public decimal TempoTelaEstimadoHoras { get; set; }
    public int Pontos { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public Responsavel Responsavel { get; set; } = null!;
    public ConfiguracaoCrianca Configuracao { get; set; } = null!;
    public List<SessaoUso> Sessoes { get; set; } = new();
    public List<RespostaCrianca> Respostas { get; set; } = new();
}

public class ConfiguracaoCrianca
{
    public int CriancaId { get; set; } // PK e FK (1:1)
    public int LimiteDiarioMin { get; set; } = 150;
    public string InicioPermitido { get; set; } = "18:00"; // HH:mm
    public string FimPermitido { get; set; } = "21:00";    // HH:mm
    public string Preferencias { get; set; } = "";         // "Memória,Puzzle"
    public bool IntervencaoAdaptativa { get; set; } = true;
    public int IntervirAposMin { get; set; } = 20;
    public bool BloqueioSimulado { get; set; }

    // Acessibilidade (tela Configurações)
    public bool AltoContraste { get; set; }
    public bool ModoEscuro { get; set; }
    public int FontePercentual { get; set; } = 100;

    public Crianca Crianca { get; set; } = null!;
}

public class SessaoUso
{
    public int Id { get; set; }
    public int CriancaId { get; set; }
    public DateTime InicioEm { get; set; } = DateTime.UtcNow;
    public DateTime? FimEm { get; set; }
    public bool Abandonada { get; set; }
    public int IntervencoesExibidas { get; set; }

    public Crianca Crianca { get; set; } = null!;
}

public class Desafio
{
    public int Id { get; set; }
    public string Tipo { get; set; } = "";      // Palavras | Memoria | Atencao
    public string Titulo { get; set; } = "";
    public string Descricao { get; set; } = "";
    public string Emoji { get; set; } = "";
    public bool Disponivel { get; set; }        // false => "Em breve"
    public int Ordem { get; set; }

    public List<Pergunta> Perguntas { get; set; } = new();
}

public class Pergunta
{
    public int Id { get; set; }
    public int DesafioId { get; set; }
    public string Enunciado { get; set; } = "";
    public int Pontos { get; set; } = 10;

    public Desafio Desafio { get; set; } = null!;
    public List<OpcaoResposta> Opcoes { get; set; } = new();
}

public class OpcaoResposta
{
    public int Id { get; set; }
    public int PerguntaId { get; set; }
    public string Texto { get; set; } = "";
    public string? Emoji { get; set; }
    public bool Correta { get; set; }

    public Pergunta Pergunta { get; set; } = null!;
}

public class RespostaCrianca
{
    public int Id { get; set; }
    public int CriancaId { get; set; }
    public int PerguntaId { get; set; }
    public int OpcaoId { get; set; }
    public int? SessaoId { get; set; }
    public bool Acertou { get; set; }
    public int Tentativa { get; set; } = 1;
    public int TempoRespostaMs { get; set; }
    public int PontosGanhos { get; set; }
    public DateTime RespondidoEm { get; set; } = DateTime.UtcNow;

    public Crianca Crianca { get; set; } = null!;
    public Pergunta Pergunta { get; set; } = null!;
    public OpcaoResposta Opcao { get; set; } = null!;
    public SessaoUso? Sessao { get; set; }
}
