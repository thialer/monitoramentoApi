using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Monitoramento.Shared.Data;
using Microsoft.OpenApi.Models;
using ApiMonitoramentoAPI.Services;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using Stripe.Checkout;

JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

var builder = WebApplication.CreateBuilder(args);
var portEnv = Environment.GetEnvironmentVariable("PORT");
// Only override URLs if the hosting platform provided a PORT env var. Do not force a default port
// because platforms like Render expect the process to listen on the port they provide.
if (!string.IsNullOrEmpty(portEnv))
{
    builder.WebHost.UseUrls($"http://*:{portEnv}");
}
Stripe.StripeConfiguration.ApiKey =
    builder.Configuration["Stripe:SecretKey"];
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            policy
                .WithOrigins("https://front-monitoramento-de-sistemas-mtk-taupe.vercel.app", "http://localhost:5173")
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});
//ajuste para aceitar requisições do frontend
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("DefaultConnection"))
    ));

var key = Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ClockSkew = TimeSpan.Zero 
    };
});

builder.Services.AddAuthorization();

builder.Services.AddScoped<TokenService>();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "ApiMonitoramento API", Version = "v1" });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Digite o token no formato: Bearer {seu_token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
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
            new string[] { }
        }
    });
});

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}
app.UseCors("AllowFrontend");

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Health check and root redirect to Swagger to satisfy platforms (e.g. Render) expecting 200 on '/'
// root returns simple JSON OK so platforms' health checks get 200
app.MapGet("/", () => Results.Ok(new { status = "ok" }));
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// log lifetime events to help diagnose unexpected shutdowns
var logger = app.Services.GetRequiredService<ILogger<Program>>();
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        logger.LogInformation("Application started (lifetime event). Starting heartbeat task.");

        // background heartbeat to help diagnose unexpected shutdowns in hosting platforms
        _ = Task.Run(async () =>
        {
            try
            {
                while (!app.Lifetime.ApplicationStopping.IsCancellationRequested)
                {
                    logger.LogInformation("heartbeat: application alive at {Time}", DateTime.UtcNow);
                    await Task.Delay(TimeSpan.FromSeconds(30));
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Heartbeat task failed");
            }
        });
    });
app.Lifetime.ApplicationStopping.Register(() => logger.LogWarning("Application stopping (lifetime event)."));
app.Lifetime.ApplicationStopped.Register(() => logger.LogWarning("Application stopped (lifetime event)."));

// Log environment for troubleshooting and register global exception handlers
try
{
    logger.LogInformation("Startup info: PORT={Port}, Environment={Env}, DefaultConnectionExists={HasConn}",
        Environment.GetEnvironmentVariable("PORT") ?? "(none)",
        app.Environment.EnvironmentName,
        !string.IsNullOrEmpty(builder.Configuration.GetConnectionString("DefaultConnection"))
    );

    AppDomain.CurrentDomain.UnhandledException += (s, e) =>
    {
        logger.LogError(e.ExceptionObject as Exception, "Unhandled exception detected");
    };

    TaskScheduler.UnobservedTaskException += (s, e) =>
    {
        logger.LogError(e.Exception, "Unobserved task exception detected");
        e.SetObserved();
    };
}
catch (Exception ex)
{
    // ensure any startup logging issues do not prevent app from running
    logger.LogError(ex, "Error while registering diagnostics handlers");
}




app.Run();

