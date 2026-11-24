using GuiaFoco.Api.Data;
using GuiaFoco.Api.Dtos;
using GuiaFoco.Api.Models;
using GuiaFoco.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GuiaFoco.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ApiBase
{
    private readonly TokenService _tokens;
    private readonly IConfiguration _cfg;

    public AuthController(AppDbContext db, TokenService tokens, IConfiguration cfg) : base(db)
    {
        _tokens = tokens;
        _cfg = cfg;
    }

    /// <summary>Cadastro do responsável + primeira criança (tela "Vamos criar seu perfil").</summary>
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        if (await Db.Responsaveis.AnyAsync(r => r.Email == email))
            return Problem(statusCode: 409, title: "Este e-mail já está cadastrado.");

        var config = new ConfiguracaoDto
        {
            LimiteDiarioMin = req.LimiteDiarioMin,
            InicioPermitido = req.InicioPermitido,
            FimPermitido = req.FimPermitido,
            Preferencias = req.Preferencias,
            IntervencaoAdaptativa = req.IntervencaoAdaptativa,
            IntervirAposMin = req.IntervirAposMin,
            BloqueioSimulado = req.BloqueioSimulado
        };

        var resp = new Responsavel
        {
            Nome = req.NomeResponsavel.Trim(),
            Email = email,
            SenhaHash = BCrypt.Net.BCrypt.HashPassword(req.Senha)
        };
        var crianca = new Crianca
        {
            Nome = req.NomeCrianca.Trim(),
            Idade = req.Idade,
            Serie = req.Serie.Trim(),
            TempoTelaEstimadoHoras = req.TempoTelaEstimadoHoras,
            Configuracao = new ConfiguracaoCrianca()
        };
        crianca.Configuracao.Aplicar(config);
        resp.Criancas.Add(crianca);

        Db.Responsaveis.Add(resp);
        await Db.SaveChangesAsync();

        return Ok(Montar(resp, new List<Crianca> { crianca }));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var resp = await Db.Responsaveis.FirstOrDefaultAsync(r => r.Email == email);

        if (resp == null || !BCrypt.Net.BCrypt.Verify(req.Senha, resp.SenhaHash))
            return Problem(statusCode: 401, title: "E-mail ou senha inválidos.");

        return Ok(await MontarComCriancas(resp));
    }

    /// <summary>Botão "Entrar na demonstração".</summary>
    [AllowAnonymous]
    [HttpPost("demo")]
    public async Task<ActionResult<AuthResponse>> Demo()
    {
        if (!_cfg.GetValue<bool>("App:EnableDemo")) return NaoEncontrado();

        var resp = await Db.Responsaveis.FirstOrDefaultAsync(r => r.Email == Seeder.DemoEmail);
        if (resp == null) return NaoEncontrado("Conta de demonstração indisponível.");

        return Ok(await MontarComCriancas(resp));
    }

    [HttpGet("me")]
    public async Task<ActionResult<AuthResponse>> Me()
    {
        var resp = await Db.Responsaveis.FindAsync(ResponsavelId);
        if (resp == null) return Unauthorized();
        return Ok(await MontarComCriancas(resp));
    }

    // ----------------------------------------------------------------

    private async Task<AuthResponse> MontarComCriancas(Responsavel r)
    {
        var criancas = await Db.Criancas.Include(c => c.Configuracao)
            .Where(c => c.ResponsavelId == r.Id).OrderBy(c => c.Id).ToListAsync();
        return Montar(r, criancas);
    }

    private AuthResponse Montar(Responsavel r, List<Crianca> criancas)
    {
        var (token, expira) = _tokens.Gerar(r);
        var dtos = criancas.Select(c => c.ToDto()).ToList();
        return new AuthResponse(token, expira, new ResponsavelDto(r.Id, r.Nome, r.Email), dtos.FirstOrDefault(), dtos);
    }
}
