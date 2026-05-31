using System.Net;
using Refit;

namespace CardGames.Poker.Web.Services.TableActions;

/// <summary>
/// A normalized result for a table action API call, hiding the differences
/// between Refit <see cref="IApiResponse"/> results and
/// <see cref="RouterResponse{T}"/> results behind a single shape.
/// </summary>
public class TableActionResult
{
    protected TableActionResult(bool isSuccess, string? error, HttpStatusCode statusCode)
    {
        IsSuccess = isSuccess;
        Error = error;
        StatusCode = statusCode;
    }

    /// <summary>Whether the action completed successfully.</summary>
    public bool IsSuccess { get; }

    /// <summary>A normalized, user-facing error message when the action failed.</summary>
    public string? Error { get; }

    /// <summary>The HTTP status code returned by the API, when known.</summary>
    public HttpStatusCode StatusCode { get; }

    public static TableActionResult Ok(HttpStatusCode statusCode = HttpStatusCode.OK)
        => new(true, null, statusCode);

    public static TableActionResult Fail(string? error, HttpStatusCode statusCode = HttpStatusCode.BadRequest)
        => new(false, error, statusCode);

    /// <summary>Normalizes a Refit response (without inspecting its content).</summary>
    public static TableActionResult From(IApiResponse response)
        => response.IsSuccessStatusCode
            ? Ok(response.StatusCode)
            : Fail(ResolveError(response), response.StatusCode);

    /// <summary>Normalizes a <see cref="RouterResponse{T}"/>.</summary>
    public static TableActionResult From<T>(RouterResponse<T> response)
        => response.IsSuccess
            ? Ok(response.StatusCode)
            : Fail(TableActionError.FromBody(response.Error) ?? response.Error, response.StatusCode);

    private protected static string? ResolveError(IApiResponse response)
        => TableActionError.FromException(response.Error, fallback: "An error occurred.");
}

/// <summary>
/// A <see cref="TableActionResult"/> that also carries the deserialized response
/// content for handlers that need it on the success path.
/// </summary>
/// <typeparam name="T">The response content type.</typeparam>
public sealed class TableActionResult<T> : TableActionResult
{
    private TableActionResult(bool isSuccess, T? content, string? error, HttpStatusCode statusCode)
        : base(isSuccess, error, statusCode)
    {
        Content = content;
    }

    /// <summary>The response content when the action succeeded.</summary>
    public T? Content { get; }

    public static TableActionResult<T> Ok(T? content, HttpStatusCode statusCode = HttpStatusCode.OK)
        => new(true, content, null, statusCode);

    public static new TableActionResult<T> Fail(string? error, HttpStatusCode statusCode = HttpStatusCode.BadRequest)
        => new(false, default, error, statusCode);

    /// <summary>Normalizes a Refit response, preserving its content.</summary>
    public static TableActionResult<T> From(IApiResponse<T> response)
        => response.IsSuccessStatusCode
            ? Ok(response.Content, response.StatusCode)
            : Fail(ResolveError(response), response.StatusCode);

    /// <summary>Normalizes a <see cref="RouterResponse{T}"/>, preserving its content.</summary>
    public static TableActionResult<T> From(RouterResponse<T> response)
        => response.IsSuccess
            ? Ok(response.Content, response.StatusCode)
            : Fail(TableActionError.FromBody(response.Error) ?? response.Error, response.StatusCode);
}
