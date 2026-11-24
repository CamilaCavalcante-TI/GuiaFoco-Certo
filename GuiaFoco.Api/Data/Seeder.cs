using GuiaFoco.Api.Models;
using GuiaFoco.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace GuiaFoco.Api.Data;

public static class Seeder
{
    public const string DemoEmail = "demo@guiafoco.com";
    public const string DemoSenha = "Demo@1234";

    public static async Task RunAsync(AppDbContext db, IConfiguration cfg, RelogioLocal relogio)
    {
        if (!await db.Desafios.AnyAsync())
        {
            db.Desafios.AddRange(CriarDesafios());
            await db.SaveChangesAsync();
        }

        if (cfg.GetValue<bool>("App:EnableDemo"))
            await CriarDemoAsync(db, relogio);
    }

    // ---------------- Desafios ----------------

    private static List<Desafio> CriarDesafios()
    {
        var palavras = new Desafio
        {
            Tipo = "Palavras", Titulo = "Palavras", Emoji = "🔤", Ordem = 1, Disponivel = true,
            Descricao = "Reconheça a palavra que começa com a letra indicada.",
            Perguntas =
            {
                Q("B", ("BOLA", "⚽"), ("GATO", "🐱"), ("CASA", "🏠"), ("SAPO", "🐸")),
                Q("G", ("GATO", "🐱"), ("BOLA", "⚽"), ("CASA", "🏠"), ("SAPO", "🐸")),
                Q("C", ("CASA", "🏠"), ("BOLA", "⚽"), ("GATO", "🐱"), ("SAPO", "🐸")),
                Q("S", ("SAPO", "🐸"), ("BOLA", "⚽"), ("GATO", "🐱"), ("CASA", "🏠")),
                Q("A", ("ABELHA", "🐝"), ("UVA", "🍇"), ("LUA", "🌙"), ("FLOR", "🌸")),
                Q("U", ("UVA", "🍇"), ("ABELHA", "🐝"), ("PATO", "🦆"), ("VACA", "🐮")),
                Q("L", ("LUA", "🌙"), ("MACACO", "🐵"), ("DADO", "🎲"), ("FLOR", "🌸")),
                Q("P", ("PATO", "🦆"), ("LUA", "🌙"), ("VACA", "🐮"), ("UVA", "🍇")),
                Q("V", ("VACA", "🐮"), ("DADO", "🎲"), ("PATO", "🦆"), ("FLOR", "🌸")),
                Q("F", ("FLOR", "🌸"), ("ABELHA", "🐝"), ("MACACO", "🐵"), ("LUA", "🌙")),
                Q("M", ("MACACO", "🐵"), ("FLOR", "🌸"), ("DADO", "🎲"), ("UVA", "🍇")),
            }
        };

        var memoria = new Desafio
        {
            Tipo = "Memoria", Titulo = "Memória", Emoji = "🧩", Ordem = 2, Disponivel = false,
            Descricao = "Encontre pares e exercite a associação."
        };

        var atencao = new Desafio
        {
            Tipo = "Atencao", Titulo = "Atenção", Emoji = "👀", Ordem = 3, Disponivel = false,
            Descricao = "Observe, compare e escolha a resposta correta."
        };

        return new List<Desafio> { palavras, memoria, atencao };
    }

    /// <summary>Pergunta "Qual palavra começa com a letra X?" — a primeira opção é a correta.</summary>
    private static Pergunta Q(string letra, (string T, string E) correta, params (string T, string E)[] erradas)
    {
        var p = new Pergunta { Enunciado = $"Qual palavra começa com a letra {letra}?", Pontos = 10 };
        p.Opcoes.Add(new OpcaoResposta { Texto = correta.T, Emoji = correta.E, Correta = true });
        foreach (var e in erradas)
            p.Opcoes.Add(new OpcaoResposta { Texto = e.T, Emoji = e.E, Correta = false });
        return p;
    }

    // ---------------- Conta de demonstração ----------------

    private static async Task CriarDemoAsync(AppDbContext db, RelogioLocal relogio)
    {
        if (await db.Responsaveis.AnyAsync(r => r.Email == DemoEmail)) return;

        var resp = new Responsavel
        {
            Nome = "Responsável Demo",
            Email = DemoEmail,
            SenhaHash = BCrypt.Net.BCrypt.HashPassword(DemoSenha)
        };
        var ana = new Crianca
        {
            Nome = "Ana", Idade = 7, Serie = "2º ano do Ensino Fundamental", TempoTelaEstimadoHoras = 4.5m,
            Configuracao = new ConfiguracaoCrianca
            {
                LimiteDiarioMin = 150, InicioPermitido = "18:00", FimPermitido = "21:00",
                Preferencias = "Memória,Puzzle", IntervencaoAdaptativa = true,
                IntervirAposMin = 20, BloqueioSimulado = true
            }
        };
        resp.Criancas.Add(ana);
        db.Responsaveis.Add(resp);
        await db.SaveChangesAsync();

        // Histórico fictício dos últimos 14 dias para o painel não ficar vazio.
        var perguntas = await db.Perguntas.Include(p => p.Opcoes).ToListAsync();
        if (perguntas.Count == 0) return;

        var rnd = new Random(42);
        var pontos = 0;
        var agoraUtc = DateTime.UtcNow;

        for (var dia = 13; dia >= 0; dia--)
        {
            var diaLocal = relogio.AgoraLocal.Date.AddDays(-dia);
            var qtd = rnd.Next(2, 4);
            var precisao = 0.65 + (13 - dia) * 0.0125;

            for (var k = 0; k < qtd; k++)
            {
                var inicioLocal = diaLocal.AddHours(18 + k).AddMinutes(rnd.Next(0, 30));
                var inicioUtc = relogio.LocalParaUtc(inicioLocal);
                var duracao = rnd.Next(12, 26);
                if (inicioUtc.AddMinutes(duracao) > agoraUtc) continue; // não cria sessões no futuro

                var sessao = new SessaoUso
                {
                    CriancaId = ana.Id,
                    InicioEm = inicioUtc,
                    FimEm = inicioUtc.AddMinutes(duracao),
                    Abandonada = rnd.NextDouble() < 0.25
                };
                db.Sessoes.Add(sessao);

                var n = rnd.Next(3, 7);
                for (var i = 0; i < n; i++)
                {
                    var p = perguntas[rnd.Next(perguntas.Count)];
                    var acertou = rnd.NextDouble() < precisao;
                    var opcao = acertou
                        ? p.Opcoes.First(o => o.Correta)
                        : p.Opcoes.Where(o => !o.Correta).OrderBy(_ => rnd.Next()).First();
                    var ganhos = acertou ? p.Pontos : 0;
                    pontos += ganhos;

                    db.Respostas.Add(new RespostaCrianca
                    {
                        CriancaId = ana.Id,
                        PerguntaId = p.Id,
                        OpcaoId = opcao.Id,
                        Sessao = sessao,
                        Acertou = acertou,
                        Tentativa = 1,
                        TempoRespostaMs = rnd.Next(1800, 3200),
                        PontosGanhos = ganhos,
                        RespondidoEm = inicioUtc.AddMinutes(i * 2 + 1)
                    });
                }
            }
        }

        ana.Pontos = pontos;
        await db.SaveChangesAsync();
    }
}
