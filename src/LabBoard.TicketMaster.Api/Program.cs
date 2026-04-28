using LabBoard.TicketMaster.Api.Middleware;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

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
