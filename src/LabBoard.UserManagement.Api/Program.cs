using LabBoard.UserManagement.Api.Middleware;
using LabBoard.UserManagement.Api.Services.User;
using Scalar.AspNetCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularDev", policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
        options.WithTitle("LabBoard UserManagement API"));
}

app.UseMiddleware<RequestTraceMiddleware>();
app.UseCors("AllowAngularDev");
app.UseHttpsRedirection();
app.MapControllers();
app.Run();
