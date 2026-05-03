using MonitoramentoWorker;
using Microsoft.EntityFrameworkCore;
using Monitoramento.Shared.Data;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(
        connectionString,
        ServerVersion.AutoDetect(connectionString)));

builder.Services.AddHttpClient();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();