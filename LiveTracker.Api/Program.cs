using LiveTracker.Api.Workers;
using LiveTracker.Api.WebSockets;
using LiveTracker.Application;
using LiveTracker.Application.Options;
using LiveTracker.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddHostedService<IngestionWorker>();
builder.Services.AddHostedService<RetentionWorker>();
builder.Services
    .AddOptions<IngestionOptions>()
    .Bind(builder.Configuration.GetSection(IngestionOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();   // hatalý yapýlandýrmada uygulama baþtan kalkmaz

builder.Services
    .AddOptions<RetentionOptions>()
    .Bind(builder.Configuration.GetSection(RetentionOptions.SectionName))
    .ValidateOnStart();
builder.Services
    .AddOptions<StreamingOptions>()
    .Bind(builder.Configuration.GetSection(StreamingOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<PositionSocketHandler>();

builder.Services.AddControllers();
builder.Services.AddHealthChecks(); 
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LiveTracker.Infrastructure.Persistence.LiveTrackerDbContext>();
    await db.Database.MigrateAsync();
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(30) });
app.MapGet("/ws/positions", (HttpContext ctx, PositionSocketHandler handler) => handler.HandleAsync(ctx));
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();