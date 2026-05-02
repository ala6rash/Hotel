using Hangfire;
using Hangfire.InMemory;
using HotelBookingSaaS.API.Adapters.Implementations;
using HotelBookingSaaS.API.Data;
using HotelBookingSaaS.API.Hubs;
using HotelBookingSaaS.API.Jobs;
using HotelBookingSaaS.API.Middleware;
using HotelBookingSaaS.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ─── Serilog ──────────────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/hotelsaas-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// ─── Database — SQLite on mobile/dev, SQL Server in production ────────────────
var useSqlite = builder.Configuration.GetValue<bool>("UseSqlite")
    || !OperatingSystem.IsWindows();   // auto-detect: Windows → SQL Server, others → SQLite

if (useSqlite)
{
    var dbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "hotelsaas.db");
    Log.Information("Using SQLite at {Path}", dbPath);
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlite($"Data Source={dbPath}"));
}
else
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(
            builder.Configuration.GetConnectionString("DefaultConnection"),
            sql => sql.EnableRetryOnFailure(3)));
}

// ─── Authentication & Authorization ───────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key not configured");

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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };

        // Allow JWT in SignalR query string
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) &&
                    ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ─── Services ─────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IEncryptionService, EncryptionService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IBookingSyncService, BookingSyncService>();
builder.Services.AddScoped<IPmsAdapterFactory, PmsAdapterFactory>();

// PMS Adapters (transient — each request gets a fresh instance)
builder.Services.AddTransient<SkyHorizonAdapter>();
builder.Services.AddTransient<OperaAdapter>();
builder.Services.AddTransient<MewsAdapter>();
builder.Services.AddTransient<ChannexAdapter>();

// Hangfire — InMemory (works everywhere, no SQL Server needed)
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseInMemoryStorage());

builder.Services.AddHangfireServer();
builder.Services.AddTransient<SyncHangfireJob>();

// SignalR
builder.Services.AddSignalR();

// HTTP clients
builder.Services.AddHttpClient("SkyHorizon");
builder.Services.AddHttpClient("Oracle Opera");
builder.Services.AddHttpClient("Mews");
builder.Services.AddHttpClient("Channex");

// CORS — allow React dev server on any local address
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)   // allow localhost on any port for dev
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Controllers, Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Hotel Booking SaaS API",
        Version = "v1",
        Description = "Multi-hotel booking management system for Jordan"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header. Example: 'Bearer {token}'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                    { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// Health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>();

var app = builder.Build();

// ─── Pipeline ─────────────────────────────────────────────────────────────────
// Always show Swagger (useful for mobile testing)
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Hotel SaaS v1"));

// Skip HTTPS redirect on mobile/dev — no certificate
if (!useSqlite)
    app.UseHttpsRedirection();

app.UseCors("FrontendPolicy");
app.UseSerilogRequestLogging();
app.UseMiddleware<ExceptionMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthFilter() }
});

app.MapControllers();
app.MapHub<BookingHub>("/hubs/booking");
app.MapHealthChecks("/health");

// ─── Hangfire Recurring Jobs ──────────────────────────────────────────────────
RecurringJob.AddOrUpdate<SyncHangfireJob>(
    "hourly-sync-all-hotels",
    job => job.RunHourlySync(),
    Cron.Hourly);

// ─── Database initialisation ──────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    if (useSqlite)
    {
        // EnsureCreated builds the schema directly from the model — no migrations needed.
        // Fast and works perfectly on mobile / CI.
        await db.Database.EnsureCreatedAsync();
    }
    else
    {
        // Production: run EF migrations normally
        await db.Database.MigrateAsync();
    }

    Log.Information("Database ready ({Mode})", useSqlite ? "SQLite/EnsureCreated" : "SqlServer/Migrate");
}

Log.Information("Hotel Booking SaaS API started on {Mode}", useSqlite ? "SQLite" : "SQL Server");
app.Run();
