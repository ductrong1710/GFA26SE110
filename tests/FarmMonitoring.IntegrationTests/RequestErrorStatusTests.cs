using System.Text.Json;
using FarmMonitoring.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace FarmMonitoring.IntegrationTests;

public class RequestErrorStatusTests
{
    [Theory]
    [InlineData(StatusCodes.Status400BadRequest)]
    [InlineData(StatusCodes.Status413PayloadTooLarge)]
    public async Task Request_errors_preserve_http_status_without_exposing_details(int status)
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        await using var output = new MemoryStream();
        context.Response.Body = output;
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new BadHttpRequestException("private-request-detail", status),
            NullLogger<ExceptionHandlingMiddleware>.Instance);
        await middleware.InvokeAsync(context);
        Assert.Equal(status, context.Response.StatusCode);
        output.Position = 0;
        var body = await JsonSerializer.DeserializeAsync<JsonElement>(output);
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.DoesNotContain("private-request-detail", body.ToString());
    }
}
