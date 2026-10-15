using AgroGuia.Aplicacion.Servicio;
using AgroGuia.Aplicacion.ServicioImpl;
using AgroGuia.Dominio.Modelo.Abstracciones;
using AgroGuia.Dominio.Modelo.Entidades;
using AgroGuia.Infraestructura.AccesoDatos.Repositorio;
using AgroGuia.Infraestructura.ServicioExterno.DependencyInjection;
using AgroGuia.Infraestructura.ServicioExterno.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

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
        // Timeout alto: la 1ª carga del índice lee todos los vectores
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

// ==================== REPOSITORIOS ====================
builder.Services.AddScoped<IUsuarioRepositorio, UsuarioRepositorioImpl>();
builder.Services.AddScoped<IConversacionRepositorio, ConversacionRepositorioImpl>();
builder.Services.AddScoped<IMensajeRepositorio, MensajeRepositorioImpl>();
builder.Services.AddScoped<IEmbeddingRepositorio, EmbeddingRepositorioImpl>();

// ==================== SERVICIOS ====================
builder.Services.AddScoped<IAuthServicio, AuthServicioImpl>();
builder.Services.AddScoped<IUsuarioServicio, UsuarioServicioImpl>();
builder.Services.AddScoped<IConversacionServicio, ConversacionServicioImpl>();
builder.Services.AddScoped<IChatServicio, ChatServicioImpl>();
builder.Services.AddScoped<IRAGServicio, RAGServicioImpl>();
builder.Services.AddScoped<IJwtService, JwtServiceImpl>();

// ==================== OLLAMA / RAG / EMBEDDINGS ====================
builder.Services.AddServicioExterno(builder.Configuration);

// ==================== APP ====================
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Precarga del índice vectorial (no bloquea el arranque del HTTP)
_ = Task.Run(async () =>
{
    try
    {
        await Task.Delay(2000); // espera a que el host esté listo
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

app.Run();