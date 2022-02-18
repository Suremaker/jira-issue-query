using System.Net.Http.Headers;
using System.Text;
using JiraMetrics.Api;
using JiraMetrics.Api.Clients;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
// Add services to the container.

builder.Services.AddControllers();
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
builder.Services.AddSingleton<IJiraClient, JiraClient>();

builder.Services.Configure<Config>(builder.Configuration.GetSection("Config"));
builder.Services.AddHttpClient(nameof(IJiraClient), (sp, client) =>
{
	var cfg = sp.GetRequiredService<IOptions<Config>>().Value;
	client.BaseAddress = new Uri(cfg.JiraUri);
	client.DefaultRequestHeaders.Add("ContentType", "application/json");
})
	.AddHeaderPropagation();
builder.Services.AddHeaderPropagation(options => { options.Headers.Add("Authorization"); });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();
app.UseHeaderPropagation();

app.MapControllers();

app.Run();
