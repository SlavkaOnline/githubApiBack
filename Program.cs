using System.Buffers;
using System.Net.Http.Headers;
using System.Text;
using GithubApiProxy;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(o =>
	o.AddDefaultPolicy(p => 
		p
		.AllowAnyHeader()
		.AllowAnyMethod()
		.AllowAnyOrigin()));

var app = builder.Build();
var github = new HttpClient();
github.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", app.Configuration["GithubToken"]);
github.DefaultRequestHeaders.Add("User-Agent",
	"Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/535.19 (KHTML, like Gecko) Chrome/18.0.1025.168 Safari/535.19"); 

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors();

app.MapPost("/api/github", async ([FromBody] GitHubRequest request, HttpContext ctx) =>
{
	var (url, method, payload) = request;
	var req = new HttpRequestMessage(method.ToUpper() switch
	{
		"GET" => HttpMethod.Get,
		"POST" => HttpMethod.Post,
		_ => throw new InvalidOperationException()
	}, url)
	{
		Content = new StringContent(payload, Encoding.UTF8, "application/json")
	};
	var result = await github.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
	var buffer = ArrayPool<byte>.Shared.Rent(1024);
	try
	{
		ctx.Response.StatusCode = 200;
		ctx.Response.ContentType = result.Content.Headers.ContentType?.MediaType;
		await using var sw = new StreamWriter(ctx.Response.Body);
		await using var stream = await result.Content.ReadAsStreamAsync();
		while (true)
		{
			var count = await stream.ReadAsync(buffer);
			if (count == 0)
				break;
			await sw.BaseStream.WriteAsync(buffer.AsMemory(0, count));
		}
		await sw.FlushAsync();	
	}
	finally
	{
		ArrayPool<byte>.Shared.Return(buffer);
	}
});


app.Run();