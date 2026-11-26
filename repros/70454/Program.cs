using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;

Console.WriteLine($"DISPATCH ASSEMBLY: {typeof(RequestDelegateFactory).Assembly.Location}");
var app = WebApplication.Create(args);
app.Urls.Add("http://127.0.0.1:0");

app.MapGet("/union/{ok:bool}", (bool ok) => ok
    ? (Outcome)TypedResults.Ok(new UserDto(1, "ann"))
    : TypedResults.Problem(statusCode: 404));

app.MapGet("/results/{ok:bool}", Results<Ok<UserDto>, ProblemHttpResult> (bool ok) => ok
    ? TypedResults.Ok(new UserDto(1, "ann"))
    : TypedResults.Problem(statusCode: 404));

await app.StartAsync();
try
{
    var expectFixed = args.Contains("--expect-fixed");
    if (expectFixed)
    {
        var endpoint = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>().Single(endpoint => endpoint.RoutePattern.RawText == "/union/{ok:bool}");
        var metadata = endpoint.Metadata.OfType<IProducesResponseTypeMetadata>().ToArray();
        if (metadata.Length != 1 || metadata[0].StatusCode != 200 || metadata[0].Type != typeof(UserDto))
        {
            throw new InvalidOperationException("Union case metadata does not match Results<>.");
        }

        Console.WriteLine("VERIFIED METADATA: 200 response describes UserDto, not the union wrapper.");
    }

    if (args.Contains("--serve"))
    {
        await app.WaitForShutdownAsync();
        return;
    }

    using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
    foreach (var path in new[] { "/results/false", "/union/false", "/results/true", "/union/true" })
    {
        using var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        Console.WriteLine($"OBSERVED {path}: {(int)response.StatusCode} {mediaType} {body}");
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        var assertionPath = expectFixed ? path.Replace("/union/", "/results/", StringComparison.Ordinal) : path;
        var matches = assertionPath switch
        {
            "/results/false" => (int)response.StatusCode == 404
                && mediaType == "application/problem+json"
                && root.GetProperty("status").GetInt32() == 404,
            "/union/false" => (int)response.StatusCode == 200
                && mediaType == "application/json"
                && root.GetProperty("problemDetails").GetProperty("status").GetInt32() == 404
                && root.GetProperty("statusCode").GetInt32() == 404,
            "/results/true" => (int)response.StatusCode == 200
                && mediaType == "application/json"
                && root.GetProperty("id").GetInt32() == 1
                && root.GetProperty("name").GetString() == "ann"
                && !root.TryGetProperty("value", out _),
            "/union/true" => (int)response.StatusCode == 200
                && mediaType == "application/json"
                && root.GetProperty("value").GetProperty("id").GetInt32() == 1
                && root.GetProperty("value").GetProperty("name").GetString() == "ann"
                && root.GetProperty("statusCode").GetInt32() == 200,
            _ => false
        };
        if (!matches)
        {
            throw new InvalidOperationException($"Response did not match issue #70454: {path}");
        }
    }

    Console.WriteLine(expectFixed
        ? "VERIFIED: union responses match Results<> for all four HTTP requests."
        : "REPRODUCED: all four HTTP responses match issue #70454.");
}
finally
{
    await app.StopAsync();
    await app.DisposeAsync();
}

public record UserDto(int Id, string Name);
public union Outcome(Ok<UserDto>, ProblemHttpResult);
