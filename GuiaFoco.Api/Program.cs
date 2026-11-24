using System.Text;
using GuiaFoco.Api.Data;
using GuiaFoco.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

// ---------- Banco de dados (MySQL) ----------
var connectionString = cfg.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Configure ConnectionStrings:Default.");
var serverVersion = ServerVersion.Parse(cfg["Database:ServerVersion"] ?? "8.0.36-mysql");

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseMySql(connectionString, serverVersion).UseSnakeCaseNamingConvention());

// ---------- Serviços ----------
builder.Services.AddSingleton<RelogioLocal>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddScoped<MetricasService>();

// ---------- JWT ----------
var jwtKey = cfg["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
    throw new InvalidOperationException("Configure Jwt:Key com pelo menos 32 caracteres.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false; // mantém o claim "sub" com esse nome
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = cfg["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = cfg["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization();

// ---------- CORS (front no GitHub Pages) ----------
var origens = cfg.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(o => o.AddPolicy("Front", p =>
    p.WithOrigins(origens).AllowAnyHeader().AllowAnyMethod()));

// ---------- MVC + Swagger ----------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "GuiaFoco API", Version = "v1" });
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Cole apenas o token JWT retornado em /api/auth/login."
    });
    o.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// ---------- Criação do banco + seed (com retry, útil quando o MySQL ainda está subindo) ----------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var log = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    for (var tentativa = 1; ; tentativa++)
    {
        try
        {
            if (cfg.GetValue("Database:AutoCreate", true))
                await db.Database.EnsureCreatedAsync();
            await Seeder.RunAsync(db, cfg, scope.ServiceProvider.GetRequiredService<RelogioLocal>());
            break;
        }
        catch (Exception ex) when (tentativa < 10)
        {
            log.LogWarning("Banco indisponível (tentativa {T}/10): {Msg}", tentativa, ex.Message);
            await Task.Delay(3000);
        }
    }
}

if (app.Environment.IsDevelopment() || cfg.GetValue<bool>("App:Swagger"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Site do GuiaFoco (pasta wwwroot): abre em http://localhost:5080
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCors("Front");
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok", hora = DateTime.UtcNow }));
app.MapControllers();

app.Run();
