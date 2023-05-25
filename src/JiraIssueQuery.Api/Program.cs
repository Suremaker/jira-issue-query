using System.Text.Json.Serialization;
using JiraIssueQuery.Api;
using JiraIssueQuery.Api.Aggregators;
using JiraIssueQuery.Api.Clients;
using JiraIssueQuery.Api.Mappers;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
// Add services to the container.

builder.Services.AddControllers().AddJsonOptions(options =>
	options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
	c.AddSecurityDefinition("basic",
		new OpenApiSecurityScheme
		{
			Name = "Authorization",
			Type = SecuritySchemeType.Http,
			Scheme = "basic",
			In = ParameterLocation.Header,
			Description = "Basic Authorization header using the Bearer scheme."
		});
	c.AddSecurityRequirement(new OpenApiSecurityRequirement
	{
		{
			new OpenApiSecurityScheme
			{
				Reference = new OpenApiReference
				{
					Type = ReferenceType.SecurityScheme,
					Id = "basic"
				}
			},
			Array.Empty<string>()
		}
	});
});
builder.Services.AddResponseCaching();
builder.Services.AddSingleton<IJiraClient, JiraClient>();
builder.Services.AddSingleton<IReferenceDataMapper, ReferenceDataMapper>();
builder.Services.AddSingleton<JiraIssueMapper>();
builder.Services.AddSingleton<IssueAggregator>();

builder.Services.Configure<Config>(builder.Configuration.GetSection("Config"));
builder.Services.Configure<MappingConfig>(builder.Configuration.GetSection("Mappings"));
builder.Services.AddHttpClient(nameof(IJiraClient), (sp, client) =>
	{
		var cfg = sp.GetRequiredService<IOptions<Config>>().Value;
		client.BaseAddress = new Uri(cfg.JiraUri);
		client.DefaultRequestHeaders.Add("ContentType", "application/json");
	})
	.AddHeaderPropagation();
builder.Services.AddHeaderPropagation(options => { options.Headers.Add("Authorization"); });

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseResponseCaching();
app.UseAuthorization();
app.UseHeaderPropagation();

app.MapControllers();

app.Run();
