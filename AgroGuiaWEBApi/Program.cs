using AgroGuia.Aplicacion.Servicio;
using AgroGuia.Aplicacion.ServicioImpl;
using AgroGuia.Dominio.Modelo.Abstracciones;
using AgroGuia.Dominio.Modelo.Entidades;
using AgroGuia.Infraestructura.AccesoDatos.Repositorio;
using AgroGuia.Infraestructura.ServicioExterno.DependencyInjection;
using AgroGuia.Infraestructura.ServicioExterno.DocumentLoader;
using AgroGuia.Infraestructura.ServicioExterno.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PdfSharp.Charting;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Misma idea que copiar el PDF a la carpeta: el límite de Kestrel (30 MB) ya no corta manuales grandes.
const long MaxPdfBytes = 524_288_000;
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = MaxPdfBytes);
builder.Services.Configure<KestrelServerOptions>(k => k.Limits.MaxRequestBodySize = MaxPdfBytes);
builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = MaxPdfBytes;
    o.ValueLengthLimit = int.MaxValue;
    o.MultipartHeadersLengthLimit = int.MaxValue;
});

// ==================== SERVICIOS ====================
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins(
                "http://localhost:8081",
                "http://localhost:19006",
                "http://127.0.0.1:8081")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

// ==================== BASE DE DATOS ====================
var connectionString = builder.Configuration.GetConnectionString("ConexionDBAgroGuia");
builder.Services.AddDbContext<AgroGuiaIA_DBContext>(
    options => options.UseSqlServer(connectionString, sql =>
    {
        sql.CommandTimeout(180);
    }),
    ServiceLifetime.Scoped);

// ==================== JWT AUTHENTICATION ====================
var jwtKey = builder.Configuration["Jwt:SecretKey"]
    ?? throw new InvalidOperationException("Jwt:SecretKey no configurado");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// ==================== SWAGGER CON JWT ====================
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AgroGuia API",
        Version = "v1"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Ingrese el token JWT: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
builder.Services.AddScoped<IUsuarioRepositorio, UsuarioRepositorioImpl>();
builder.Services.AddScoped<IConversacionRepositorio, ConversacionRepositorioImpl>();
builder.Services.AddScoped<IEmbeddingRepositorio, EmbeddingRepositorioImpl>();
builder.Services.AddScoped<IVisionIncidenciaRepositorio, VisionIncidenciaRepositorioImpl>();
builder.Services.AddScoped<ICatalogoTerminosRepositorio, CatalogoTerminosRepositorioImpl>();
builder.Services.AddScoped<IAuthServicio, AuthServicioImpl>();
builder.Services.AddScoped<IUsuarioServicio, UsuarioServicioImpl>();
builder.Services.AddScoped<IConversacionServicio, ConversacionServicioImpl>();
builder.Services.AddScoped<IChatServicio, ChatServicioImpl>();
builder.Services.AddScoped<IRAGServicio, RAGServicioImpl>();
builder.Services.AddScoped<IJwtService, JwtServiceImpl>();
builder.Services.AddScoped<IDiagnosticoServicio, DiagnosticoServicioImpl>();
builder.Services.AddSingleton<ICatalogoAgronomico, CatalogoAgronomico>();

builder.Services.AddServicioExterno(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

_ = Task.Run(async () =>
{
    try
    {
        await Task.Delay(2000);
        using var scope = app.Services.CreateScope();
        var index = scope.ServiceProvider.GetRequiredService<IVectorIndex>();
        await index.ObtenerAsync();
        Console.WriteLine("[RAG] Índice vectorial precargado OK.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[RAG] Precarga falló: {ex.Message}");
    }
});

_ = Task.Run(() =>
{
    try
    {
        using var scope = app.Services.CreateScope();
        var vision = scope.ServiceProvider.GetRequiredService<IAnalizadorVision>();
        var estado = vision.ObtenerEstado();
        Console.WriteLine(estado.ModeloDisponible
            ? $"[Vision] Modelo '{estado.Modelo}' cargado ({estado.NumeroEtiquetas} clases)."
            : $"[Vision] Sin modelo de reconocimiento: {estado.Detalle}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Vision] Precarga falló (la API sigue): {ex.Message}");
    }
});

app.Run();
