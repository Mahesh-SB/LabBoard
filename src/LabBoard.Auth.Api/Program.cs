using LabBoard.Auth.Api.Configuration;
using LabBoard.Auth.Api.Middleware;
using LabBoard.Auth.Api.Services.User;
using LabBoard.Auth.Api.Services.Client;
using LabBoard.Auth.Api.Services.OAuth;
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
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));

builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IClientAppService, ClientAppService>();
builder.Services.AddScoped<IAuthCodeService, AuthCodeService>();
builder.Services.AddSingleton<ITokenService, TokenService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
        options.WithTitle("LabBoard Auth API"));
}

app.UseMiddleware<RequestTraceMiddleware>();
app.UseCors("AllowAngularDev");
app.UseHttpsRedirection();
app.MapControllers();
app.Run();
