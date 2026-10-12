using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using TaskManagement.Api.Middleware;

namespace TaskManagement.UnitTests.Api;

public class ExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task SetsStatus499_WhenRequestAbortedByTheClient()
    {
        var context = new DefaultHttpContext();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        context.RequestAborted = cts.Token;

        async Task Next(HttpContext _) =>
            await Task.FromException(new OperationCanceledException(cts.Token));

        var middleware = new ExceptionHandlingMiddleware(Next, NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(499);
    }

    [Fact]
    public async Task ReturnsInternalServerError_WhenOperationCanceledWithoutClientAbort()
    {
        var context = new DefaultHttpContext();
        using var cts = new CancellationTokenSource();
        context.RequestAborted = cts.Token;

        async Task Next(HttpContext _) =>
            await Task.FromException(new OperationCanceledException(cts.Token));

        var middleware = new ExceptionHandlingMiddleware(Next, NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }
}