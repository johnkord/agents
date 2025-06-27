using ApprovalService;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add approval store
builder.Services.AddSingleton<IApprovalStore, SqliteApprovalStore>();

// Add session management with shared database path
builder.Services.AddSingleton<AgentAlpha.Interfaces.ISessionManager>(serviceProvider =>
{
    var logger = serviceProvider.GetRequiredService<ILogger<AgentAlpha.Services.SessionManager>>();
    // Use the same environment variable and default path as other components
    var sharedDbPath = Environment.GetEnvironmentVariable("AGENT_SESSION_DB_PATH") ?? "./data/agent_sessions.db";
    return new AgentAlpha.Services.SessionManager(logger, sharedDbPath);
});

// Add CORS for development
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthorization();
app.MapControllers();

// Add a simple web UI for approvals
app.UseDefaultFiles();
app.UseStaticFiles();

Console.WriteLine("Approval Service starting on http://localhost:5000");
Console.WriteLine("Swagger UI available at http://localhost:5000/swagger");

app.Run("http://localhost:5000");
