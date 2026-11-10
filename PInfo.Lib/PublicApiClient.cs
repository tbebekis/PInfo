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

    // ● internal
    /// <summary>
    /// Estimates the remaining time for one stage from its average byte processing rate.
    /// Waits five seconds before estimating; callers must treat the result as approximate.
    /// </summary>
    /// <param name="Completed">The processed byte count.</param>
    /// <param name="Total">The expected byte count, or zero when unknown.</param>
    /// <param name="ElapsedMilliseconds">The elapsed stage time in milliseconds.</param>
    /// <returns>A remaining-time suffix, or an empty string until an estimate is available.</returns>
    static internal string EstimateRemainingTime(long Completed, long Total, long ElapsedMilliseconds)
    {
        if (Completed <= 0 || Total <= 0 || ElapsedMilliseconds < 5000)
            return string.Empty;
        double Seconds = Math.Max(0, Total - Completed) / (double)Completed * ElapsedMilliseconds / 1000.0;
        if (Seconds < 1)
            return "\nFinishing this stage...";
        return Seconds < 60
            ? $"\nAbout {Math.Ceiling(Seconds):N0} seconds remaining for this stage."
            : $"\nAbout {Math.Ceiling(Seconds / 60):N0} minutes remaining for this stage.";
    }

    // ● public
    /// <summary>
    /// Streams a response to a new file without buffering the entire body in memory.
    /// The caller owns the destination and must remove it after failure or cancellation.
    /// </summary>
    /// <param name="ActionUrl">The absolute URL to download.</param>
    /// <param name="FilePath">A destination file that must not already exist.</param>
    /// <param name="CancellationToken">Cancels the complete download.</param>
    /// <param name="Progress">Receives download status at most twice per second, plus the final byte count.</param>
    /// <returns>The request outcome; the response JSON property is unused.</returns>
    static public async Task<ApiClientResult> DownloadFileAsync(string ActionUrl, string FilePath,
        CancellationToken CancellationToken = default, IProgress<string> Progress = null)
    {
        ApiClientResult Result = new(ActionUrl);
        // ResponseHeadersRead requires a separate deadline for reading the response body.
        using CancellationTokenSource DownloadCancellation = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
        DownloadCancellation.CancelAfter(TimeSpan.FromMinutes(10));
        try
        {
            using HttpResponseMessage Response = await ApiClient.GetAsync(ActionUrl,
                HttpCompletionOption.ResponseHeadersRead, DownloadCancellation.Token).ConfigureAwait(false);
            Result.StatusCode = Response.StatusCode;
            Result.ReasonPhrase = Response.ReasonPhrase;
            if (!Response.IsSuccessStatusCode)
                return Result;
            await using Stream Input = await Response.Content.ReadAsStreamAsync(DownloadCancellation.Token).ConfigureAwait(false);
            await using FileStream Output = new(FilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
            byte[] Buffer = new byte[81920];
            long Received = 0;
            long Started = Environment.TickCount64;
            long LastReport = Started;
            long Total = Response.Content.Headers.ContentLength.GetValueOrDefault();
            int Read;
            while ((Read = await Input.ReadAsync(Buffer.AsMemory(), DownloadCancellation.Token).ConfigureAwait(false)) != 0)
            {
                await Output.WriteAsync(Buffer.AsMemory(0, Read), DownloadCancellation.Token).ConfigureAwait(false);
                Received += Read;
                if (Progress != null && Environment.TickCount64 - LastReport >= 500)
                {
                    string Message = Total > 0
                        ? $"Downloading ZIP file... {Received / 1048576.0:N1} / {Total / 1048576.0:N1} MB"
                        : $"Downloading ZIP file... {Received / 1048576.0:N1} MB";
                    Progress.Report(Message + EstimateRemainingTime(Received, Total, Environment.TickCount64 - Started));
                    LastReport = Environment.TickCount64;
                }
            }
            Progress?.Report($"Downloading ZIP file... {Received / 1048576.0:N1} MB");
            await Output.FlushAsync(DownloadCancellation.Token).ConfigureAwait(false);
            DownloadCancellation.Token.ThrowIfCancellationRequested();
            Result.ResultStatus = ApiClientResultStatus.Success;
        }
        catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested)
        {
            Result.ResultStatus = ApiClientResultStatus.Cancelled;
            Result.ReasonPhrase = "The download was cancelled.";
        }
        catch (OperationCanceledException)
        {
            Result.ResultStatus = ApiClientResultStatus.Error;
            Result.ReasonPhrase = "The download timed out.";
        }
        catch (Exception Ex) when (Ex is HttpRequestException || Ex is IOException || Ex is UnauthorizedAccessException)
        {
            Result.ResultStatus = ApiClientResultStatus.Error;
            Result.ReasonPhrase = Ex.Message;
        }
        return Result;
    }
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
