using LabBoard.TicketMaster.Api.Configuration;
using LabBoard.TicketMaster.Api.Middleware;
using LabBoard.TicketMaster.Api.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AuthApiOptions>(builder.Configuration.GetSection("AuthApi"));
builder.Services.Configure<UserManagementApiOptions>(builder.Configuration.GetSection("UserManagementApi"));

builder.Services.AddHttpClient();
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// ServiceTokenService is a singleton so it can cache the token across requests
builder.Services.AddSingleton<IServiceTokenService, ServiceTokenService>();
builder.Services.AddScoped<IUserDetailsService, UserDetailsService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
        options.WithTitle("LabBoard TicketMaster API"));
}

app.UseMiddleware<RequestTraceMiddleware>();
app.MapControllers();
app.Run();
