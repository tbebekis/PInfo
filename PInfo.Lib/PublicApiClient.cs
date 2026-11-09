/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo;

/// <summary>
/// The outcome of a public API request.
/// </summary>
public enum ApiClientResultStatus
{
    /// <summary>
    /// The request failed or timed out, or the server returned an unsuccessful HTTP status code.
    /// </summary>
    Error,
    /// <summary>
    /// The caller requested cancellation.
    /// </summary>
    Cancelled,
    /// <summary>
    /// The server returned a successful HTTP status code and the response body was read.
    /// </summary>
    Success
}

/// <summary>
/// Contains the outcome and response details of a public API request.
/// </summary>
public class ApiClientResult
{

    // ● constructor
    /// <summary>
    /// Creates a result for the specified request URL.
    /// </summary>
    /// <param name="ActionUrl">The requested URL.</param>
    public ApiClientResult(string ActionUrl)
    {
        this.ActionUrl = ActionUrl;
    }
    
    // ● properties  
    /// <summary>
    /// The requested URL.
    /// </summary>
    public string ActionUrl { get; }

    /// <summary>
    /// The success, cancellation, or error outcome of the request.
    /// </summary>
    public ApiClientResultStatus ResultStatus { get; set; }
    /// <summary>
    /// The HTTP status code, or its default value when no response was received.
    /// </summary>
    public HttpStatusCode StatusCode { get; set; }
    /// <summary>
    /// The HTTP reason phrase, or a cancellation or error message.
    /// </summary>
    public string ReasonPhrase { get; set; }
    /// <summary>
    /// The response body, including the body of an unsuccessful HTTP response.
    /// </summary>
    public string ResponseJsonText { get; set; }
}

/// <summary>
/// Shared HttpClient
/// </summary>
static public class PublicApiClient
{
    // ● private
    static readonly HttpClient ApiClient = new HttpClient();
    //static readonly System.Threading.Lock syncLock = new();

    // ● public
    /// <summary>
    /// Sends a GET request and reads the response body, including HTTP error responses.
    /// Caller cancellation returns Cancelled; network failures and timeouts return Error.
    /// </summary>
    /// <param name="ActionUrl">The absolute URL to request.</param>
    /// <param name="CancellationToken">The token used to cancel this request.</param>
    /// <returns>The request outcome and any available HTTP response details.</returns>
    static public async Task<ApiClientResult> GetAsync(string ActionUrl, CancellationToken CancellationToken = default)
    {
        ApiClientResult Result = new(ActionUrl);

        try
        {
            CancellationToken.ThrowIfCancellationRequested();
            using (HttpResponseMessage Response = await ApiClient.GetAsync(ActionUrl, CancellationToken).ConfigureAwait(false))
            {
                Result.StatusCode = Response.StatusCode;
                Result.ReasonPhrase = Response.ReasonPhrase;
                Result.ResponseJsonText = await Response.Content.ReadAsStringAsync(CancellationToken).ConfigureAwait(false);
                CancellationToken.ThrowIfCancellationRequested();
                Result.ResultStatus = Response.IsSuccessStatusCode ? ApiClientResultStatus.Success : ApiClientResultStatus.Error;
            }
        }
        catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested)
        {
            Result.ReasonPhrase = "Cancellation Requested";
            Result.ResultStatus = ApiClientResultStatus.Cancelled;
        }
        catch (OperationCanceledException Ex)
        {
            Result.ReasonPhrase = Ex.Message;
            Result.ResultStatus = ApiClientResultStatus.Error;
        }
        catch (HttpRequestException Ex)
        {
            Result.ReasonPhrase = Ex.Message;
            Result.ResultStatus = ApiClientResultStatus.Error;
        }
        return Result;
    }
}
