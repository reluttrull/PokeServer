using DotNetEnv;
using PokeServer;

Env.Load(); // load environment variables from .env file

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowClient", policy =>
        policy.WithOrigins("https://reluttrull.github.io", "http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddMemoryCache(); // inject in-memory caching service
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapOpenApi();
app.MapHealthChecks("/health");

app.UseCors("AllowClient");
app.UseHttpsRedirection();

app.UseAuthorization();

app.MapHub<NotificationHub>("/notifications");
app.MapControllers();

app.Run();
