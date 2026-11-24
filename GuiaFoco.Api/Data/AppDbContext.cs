using GuiaFoco.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GuiaFoco.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Responsavel> Responsaveis => Set<Responsavel>();
    public DbSet<Crianca> Criancas => Set<Crianca>();
    public DbSet<ConfiguracaoCrianca> Configuracoes => Set<ConfiguracaoCrianca>();
    public DbSet<SessaoUso> Sessoes => Set<SessaoUso>();
    public DbSet<Desafio> Desafios => Set<Desafio>();
    public DbSet<Pergunta> Perguntas => Set<Pergunta>();
    public DbSet<OpcaoResposta> Opcoes => Set<OpcaoResposta>();
    public DbSet<RespostaCrianca> Respostas => Set<RespostaCrianca>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Todas as datas são gravadas e lidas como UTC (a API serializa com "Z").
        builder.Properties<DateTime>().HaveConversion<UtcConverter>();
        builder.Properties<DateTime?>().HaveConversion<UtcNullableConverter>();
    }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Responsavel>(e =>
        {
            e.ToTable("responsaveis");
            e.Property(x => x.Nome).HasMaxLength(120).IsRequired();
            e.Property(x => x.Email).HasMaxLength(180).IsRequired();
            e.Property(x => x.SenhaHash).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
        });

        mb.Entity<Crianca>(e =>
        {
            e.ToTable("criancas");
            e.Property(x => x.Nome).HasMaxLength(120).IsRequired();
            e.Property(x => x.Serie).HasMaxLength(60).IsRequired();
            e.Property(x => x.TempoTelaEstimadoHoras).HasPrecision(4, 1);
            e.HasOne(x => x.Responsavel).WithMany(r => r.Criancas)
                .HasForeignKey(x => x.ResponsavelId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Configuracao).WithOne(c => c.Crianca)
                .HasForeignKey<ConfiguracaoCrianca>(c => c.CriancaId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<ConfiguracaoCrianca>(e =>
        {
            e.ToTable("configuracoes_crianca");
            e.HasKey(x => x.CriancaId);
            e.Property(x => x.InicioPermitido).HasMaxLength(5).IsRequired();
            e.Property(x => x.FimPermitido).HasMaxLength(5).IsRequired();
            e.Property(x => x.Preferencias).HasMaxLength(200).IsRequired();
        });

        mb.Entity<SessaoUso>(e =>
        {
            e.ToTable("sessoes_uso");
            e.HasIndex(x => new { x.CriancaId, x.InicioEm });
            e.HasOne(x => x.Crianca).WithMany(c => c.Sessoes)
                .HasForeignKey(x => x.CriancaId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<Desafio>(e =>
        {
            e.ToTable("desafios");
            e.Property(x => x.Tipo).HasMaxLength(30).IsRequired();
            e.Property(x => x.Titulo).HasMaxLength(80).IsRequired();
            e.Property(x => x.Descricao).HasMaxLength(250).IsRequired();
            e.Property(x => x.Emoji).HasMaxLength(16).IsRequired();
        });

        mb.Entity<Pergunta>(e =>
        {
            e.ToTable("perguntas");
            e.Property(x => x.Enunciado).HasMaxLength(250).IsRequired();
            e.HasOne(x => x.Desafio).WithMany(d => d.Perguntas)
                .HasForeignKey(x => x.DesafioId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<OpcaoResposta>(e =>
        {
            e.ToTable("opcoes_resposta");
            e.Property(x => x.Texto).HasMaxLength(80).IsRequired();
            e.Property(x => x.Emoji).HasMaxLength(16);
            e.HasOne(x => x.Pergunta).WithMany(p => p.Opcoes)
                .HasForeignKey(x => x.PerguntaId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<RespostaCrianca>(e =>
        {
            e.ToTable("respostas_crianca");
            e.HasIndex(x => new { x.CriancaId, x.RespondidoEm });
            e.HasOne(x => x.Crianca).WithMany(c => c.Respostas)
                .HasForeignKey(x => x.CriancaId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Pergunta).WithMany()
                .HasForeignKey(x => x.PerguntaId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Opcao).WithMany()
                .HasForeignKey(x => x.OpcaoId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Sessao).WithMany()
                .HasForeignKey(x => x.SessaoId).OnDelete(DeleteBehavior.SetNull);
        });
    }
}

public class UtcConverter : ValueConverter<DateTime, DateTime>
{
    public UtcConverter() : base(
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc)) { }
}

public class UtcNullableConverter : ValueConverter<DateTime?, DateTime?>
{
    public UtcNullableConverter() : base(
        v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v,
        v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v) { }
}
