using Aonik.Application;
using Aonik.Infrastructure;
using FastEndpoints;
using FastEndpoints.Swagger;

var builder = WebApplication.CreateBuilder(args);

// Add Application and Infrastructure layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Add FastEndpoints
builder.Services.AddFastEndpoints();

// Add Swagger
builder.Services.SwaggerDocument();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwaggerGen();
}

app.UseHttpsRedirection();

// Use FastEndpoints
app.UseFastEndpoints();

app.Run();

// Make the Program class accessible for testing
public partial class Program { }
