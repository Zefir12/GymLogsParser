using System.Text.Json.Serialization;
using GymLogsParser.Extensions;
using GymLogsParser.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConfigurationRegistration(builder.Configuration);
builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddDatabase(builder.Configuration);
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter()
        );
    });
builder.Services.AddSwaggerGen();
builder.Services.AddCorsPolicies(builder.Configuration);
builder.Services.AddOidcAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddAppAuthorization(builder.Environment);


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseForwardedHeaders();
app.UseCors("DevCors");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();