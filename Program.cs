using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StoreApiLR11.Data;
using StoreApiLR11.Filters;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options =>
{
    options.Filters.Add<DatabaseExceptionFilter>();
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });

    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:8000", "https://myshop-frontend.com")
            .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
            .WithHeaders("Content-Type", "Authorization", "X-Requested-With")
            .AllowCredentials();
    });
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<StoreContext>(options =>
    options.UseMySql(
        connectionString,
        ServerVersion.AutoDetect(connectionString),
        mysql => mysql.EnableRetryOnFailure()));

var app = builder.Build();

app.UseRouting();

if (app.Environment.IsDevelopment())
{
    app.UseCors("AllowAll");
    app.UseExceptionHandler("/error-development");
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseCors("AllowFrontend");
    app.UseExceptionHandler("/error");
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Map("/error", async (HttpContext context) =>
{
    var exception = context.Features.Get<IExceptionHandlerPathFeature>()?.Error;
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    context.Response.ContentType = "application/problem+json";

    await context.Response.WriteAsJsonAsync(new ProblemDetails
    {
        Status = StatusCodes.Status500InternalServerError,
        Title = "Internal server error",
        Detail = "An unexpected error occurred.",
        Instance = context.Features.Get<IExceptionHandlerPathFeature>()?.Path
    });
});

app.Map("/error-development", async (HttpContext context) =>
{
    var feature = context.Features.Get<IExceptionHandlerPathFeature>();
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    context.Response.ContentType = "application/problem+json";

    await context.Response.WriteAsJsonAsync(new ProblemDetails
    {
        Status = StatusCodes.Status500InternalServerError,
        Title = "Internal server error",
        Detail = feature?.Error.Message,
        Instance = feature?.Path
    });
});

app.MapGet("/api/test-crash", () =>
{
    throw new InvalidOperationException("Test unhandled exception");
});

app.Run();
