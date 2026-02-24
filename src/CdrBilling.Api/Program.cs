using CdrBilling.Api.Endpoints;
using CdrBilling.Application.Abstractions;
using CdrBilling.Application.UseCases;
using CdrBilling.Infrastructure.Parsing;
using CdrBilling.Infrastructure.Persistence;
using CdrBilling.Infrastructure.Persistence.Repositories;
using CdrBilling.Infrastructure.Realtime;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ─── Database ─────────────────────────────────────────────────────────────────
var connStr = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Connection string 'Postgres' is not configured.");

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(connStr)
     .UseSnakeCaseNamingConvention());

// NpgsqlDataSource for low-level binary COPY operations (not EF managed)
builder.Services.AddSingleton<NpgsqlDataSource>(_ =>
    NpgsqlDataSource.Create(connStr));

// ─── CQRS ─────────────────────────────────────────────────────────────────────
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(RunTarificationHandler).Assembly));

// ─── Repositories ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<IBillingSessionRepository, BillingSessionRepository>();
builder.Services.AddScoped<ICallRecordRepository, CallRecordRepository>();
builder.Services.AddScoped<ITariffRepository, TariffRepository>();
builder.Services.AddScoped<ISubscriberRepository, SubscriberRepository>();

// ─── Progress (SSE) ───────────────────────────────────────────────────────────
builder.Services.AddSingleton<SseProgressHub>();
builder.Services.AddScoped<ISessionProgressReporter, SseProgressReporter>();

// ─── File parsers ─────────────────────────────────────────────────────────────
builder.Services.AddTransient<ICdrFileParser, CdrFileParser>();
builder.Services.AddTransient<ITariffFileParser, TariffFileParser>();
builder.Services.AddTransient<ISubscriberFileParser, SubscriberFileParser>();

// ─── API ──────────────────────────────────────────────────────────────────────
builder.Services.AddOpenApi();

// Increase multipart body size limit for large CDR files (default 30 MB → 500 MB)
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 500 * 1024 * 1024; // 500 MB
});
builder.WebHost.ConfigureKestrel(o =>
{
    o.Limits.MaxRequestBodySize = 500 * 1024 * 1024;
});

// ─── CORS (allow all for development) ────────────────────────────────────────
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

// ─── Migrations ───────────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

// ─── Middleware ───────────────────────────────────────────────────────────────
app.UseCors();

app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.Title = "CdrBilling API";
    options.Theme = ScalarTheme.Purple;
});

// ─── Endpoints ────────────────────────────────────────────────────────────────
app.MapSessionEndpoints();
app.MapUploadEndpoints();
app.MapBillingEndpoints();
app.MapProgressEndpoints();

app.Run();
