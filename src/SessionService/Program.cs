using Common.Interfaces.Session;
using SessionService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add session management
builder.Services.AddSingleton<ISessionManager, SessionManager>();

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

// Add a simple web UI for session management
app.UseDefaultFiles();
app.UseStaticFiles();

Console.WriteLine("Session Service starting on http://localhost:5001");
Console.WriteLine("Swagger UI available at http://localhost:5001/swagger");
Console.WriteLine("Session Management UI available at http://localhost:5001");

app.Run("http://localhost:5001");