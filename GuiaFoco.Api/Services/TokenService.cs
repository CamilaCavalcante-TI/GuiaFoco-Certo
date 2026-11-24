using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GuiaFoco.Api.Models;
using Microsoft.IdentityModel.Tokens;

namespace GuiaFoco.Api.Services;

public class TokenService
{
    private readonly IConfiguration _cfg;
    public TokenService(IConfiguration cfg) => _cfg = cfg;

    public (string Token, DateTime ExpiraEm) Gerar(Responsavel r)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_cfg["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expira = DateTime.UtcNow.AddMinutes(_cfg.GetValue("Jwt:ExpiraMinutos", 480));

        var claims = new[]
        {
            new Claim("sub", r.Id.ToString()),
            new Claim("email", r.Email),
            new Claim("name", r.Nome)
        };

        var jwt = new JwtSecurityToken(
            issuer: _cfg["Jwt:Issuer"],
            audience: _cfg["Jwt:Audience"],
            claims: claims,
            expires: expira,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(jwt), expira);
    }
}
