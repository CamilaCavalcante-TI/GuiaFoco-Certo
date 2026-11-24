namespace GuiaFoco.Api.Services;

/// <summary>
/// O banco guarda tudo em UTC. Este serviço converte para o fuso do usuário
/// (padrão America/Manaus) para calcular "hoje", horário permitido etc.
/// </summary>
public class RelogioLocal
{
    private readonly TimeZoneInfo _tz;

    public RelogioLocal(IConfiguration cfg)
    {
        var id = cfg["App:TimeZone"] ?? "America/Manaus";
        try { _tz = TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch { _tz = TimeZoneInfo.Local; }
    }

    public DateTime AgoraLocal => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _tz);

    public DateTime ParaLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _tz);

    public DateTime LocalParaUtc(DateTime local) =>
        DateTime.SpecifyKind(
            TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), _tz),
            DateTimeKind.Utc);

    public DateTime InicioDoDiaUtc() => LocalParaUtc(AgoraLocal.Date);
}
