#:sdk Microsoft.NET.Sdk.Web

// Lab 6 MA6-01 helper (manual check only, not part of the application or the test suite).
// Waits N seconds, then forwards each request unchanged to the real API.
// Usage: dotnet run scripts/DelayProxy.cs -- <delaySeconds> [targetUrl]
var delaySeconds = args.Length > 0 ? int.Parse(args[0]) : 6;
var target = args.Length > 1 ? args[1] : "http://localhost:5080";

var builder = WebApplication.CreateBuilder(new[] { "--urls", "http://localhost:5081" });
var app = builder.Build();
var http = new HttpClient { BaseAddress = new Uri(target) };

app.Run(async context =>
{
    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), context.RequestAborted);

    var forward = new HttpRequestMessage(
        new HttpMethod(context.Request.Method),
        context.Request.Path + context.Request.QueryString);

    if (context.Request.ContentLength > 0)
    {
        forward.Content = new StreamContent(context.Request.Body);
        if (context.Request.ContentType is not null)
            forward.Content.Headers.TryAddWithoutValidation("Content-Type", context.Request.ContentType);
    }

    using var response = await http.SendAsync(forward, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);

    context.Response.StatusCode = (int)response.StatusCode;
    if (response.Content.Headers.ContentType is not null)
        context.Response.ContentType = response.Content.Headers.ContentType.ToString();
    if (response.Headers.Location is not null)
        context.Response.Headers.Location = response.Headers.Location.ToString();

    await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
});

app.Run();
