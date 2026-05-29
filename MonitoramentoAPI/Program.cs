using ApiMonitoramentoAPI.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models; // Certifique-se que este está aqui
using Monitoramento.Shared.Data;
using Stripe.Checkout;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
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

var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrEmpty(jwtKey))
{
    // fail fast with clear message so deploy shows helpful error
    throw new InvalidOperationException("JWT configuration is missing: set Jwt:Key in configuration or environment variables.");
}

var key = Encoding.UTF8.GetBytes(jwtKey);

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
    // Return 401 with JSON (instead of HTML) when token validation fails
    options.Events = new JwtBearerEvents
    {
        OnChallenge = context =>
        {
            // skip the default logic
            context.HandleResponse();
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            var payload = System.Text.Json.JsonSerializer.Serialize(new { message = "Unauthorized - Token validation failed" });
            return context.Response.WriteAsync(payload);
        }
    };
});

builder.Services.AddAuthorization();

builder.Services.AddScoped<TokenService>();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Monitoramento API",
        Version = "v1"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Insira apenas o token JWT (sem o prefixo 'Bearer')"
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
            Array.Empty<string>()
        }
    });
});
var app = builder.Build();
// resolve logger early so middleware and lifetime handlers can use it
var logger = app.Services.GetRequiredService<ILogger<Program>>();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}
app.UseCors("AllowFrontend");

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();

// Middleware to handle Authorization header properly
app.Use(async (context, next) =>
{
    try
    {
        var auth = context.Request.Headers["Authorization"].ToString();

        if (!string.IsNullOrEmpty(auth))
        {
            // Remove duplicate "Bearer Bearer" if it exists
            if (auth.StartsWith("Bearer Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                var cleanToken = auth["Bearer Bearer ".Length..];
                context.Request.Headers["Authorization"] = $"Bearer {cleanToken}";
                logger.LogWarning("Fixed duplicate 'Bearer Bearer' in Authorization header");
            }
        }

        logger.LogInformation(
            "Incoming request {Method} {Path} - Authorization: {Auth}",
            context.Request.Method,
            context.Request.Path,
            string.IsNullOrEmpty(auth) ? "(none)" : auth[..Math.Min(50, auth.Length)] + "..."
        );
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Failed to process Authorization header");
    }

    await next();
});

app.UseAuthorization();

// Middleware to log authentication results
app.Use(async (context, next) =>
{
    await next();

    if (context.Response.StatusCode == 401)
    {
        var auth = context.Request.Headers["Authorization"].ToString();
        logger.LogWarning(
            "401 Unauthorized - Path: {Path}, Method: {Method}, Auth: {Auth}",
            context.Request.Path,
            context.Request.Method,
            string.IsNullOrEmpty(auth) ? "(none)" : auth[..Math.Min(50, auth.Length)]
        );
    }
});

app.MapControllers();

// Health check and root redirect to Swagger to satisfy platforms (e.g. Render) expecting 200 on '/'
// root returns simple JSON OK so platforms' health checks get 200
app.MapGet("/", () => Results.Ok(new { status = "ok" }));
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// log lifetime events to help diagnose unexpected shutdowns
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

