using LiveTracker.Application.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<IngestionOptions>()
    .Bind(builder.Configuration.GetSection(IngestionOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();   // hatalý yapýlandýrmada uygulama baþtan kalkmaz

builder.Services
    .AddOptions<RetentionOptions>()
    .Bind(builder.Configuration.GetSection(RetentionOptions.SectionName))
    .ValidateOnStart();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();

app.Run();