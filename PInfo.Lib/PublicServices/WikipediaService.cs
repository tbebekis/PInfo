/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo;

/// <summary>
/// Reads English Wikipedia searches and introductory text using the shared HTTP client.
/// </summary>
static public class WikipediaService
{
    // ● private
    /// <summary>
    /// Sends an identified request, validates API errors, and keeps a configurable offline response cache.
    /// </summary>
    /// <param name="Parameters">The escaped query parameters after the common API parameters.</param>
    /// <param name="CancellationToken">Cancels network and file operations.</param>
    /// <returns>The JSON response or an error with an optional cached fallback.</returns>
    static async Task<ApiClientResult> LoadAsync(string Parameters, CancellationToken CancellationToken)
    {
        string Url = "https://en.wikipedia.org/w/api.php?action=query&format=json&formatversion=2&" + Parameters;
        ApiClientResult Result = new(Url);
        string Key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(Url)));
        string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PInfo", "Data", "Wikipedia", Key + ".json");
        string Cached = null;
        try
        {
            if (File.Exists(FilePath))
            {
                Cached = await File.ReadAllTextAsync(FilePath, CancellationToken).ConfigureAwait(false);
                using JsonDocument Document = JsonDocument.Parse(Cached);
                if (!Document.RootElement.TryGetProperty("query", out _) || Document.RootElement.TryGetProperty("error", out _))
                    Cached = null;
                if (Cached != null && File.GetLastWriteTimeUtc(FilePath) > DateTime.UtcNow.AddDays(-Lib.Settings.WikipediaRefreshDays))
                {
                    Result.ResponseJsonText = Cached;
                    Result.ResultStatus = ApiClientResultStatus.Success;
                    return Result;
                }
            }
        }
        catch (Exception Ex) when (Ex is IOException || Ex is UnauthorizedAccessException || Ex is JsonException)
        {
            Cached = null;
        }
        try
        {
            using HttpRequestMessage Request = new(HttpMethod.Get, Url);
            Request.Headers.UserAgent.ParseAdd("PInfo/1.0 (+https://github.com/tbebekis/PInfo)");
            using HttpResponseMessage Response = await PublicApiClient.SendAsync(Request, CancellationToken).ConfigureAwait(false);
            Result.StatusCode = Response.StatusCode;
            Response.EnsureSuccessStatusCode();
            string Json = await Response.Content.ReadAsStringAsync(CancellationToken).ConfigureAwait(false);
            using JsonDocument Document = JsonDocument.Parse(Json);
            if (Document.RootElement.TryGetProperty("error", out _)
                || !Document.RootElement.TryGetProperty("query", out _))
                throw new JsonException("Wikipedia did not return a valid query response.");
            Result.ResponseJsonText = Json;
            Result.ResultStatus = ApiClientResultStatus.Success;
            string TemporaryPath = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                await File.WriteAllTextAsync(TemporaryPath, Json, CancellationToken).ConfigureAwait(false);
                File.Move(TemporaryPath, FilePath, true);
            }
            catch (Exception Ex) when (Ex is IOException || Ex is UnauthorizedAccessException)
            {
                Result.ReasonPhrase = "Wikipedia content could not be saved for offline use.";
            }
            finally
            {
                try { File.Delete(TemporaryPath); }
                catch (Exception Ex) when (Ex is IOException || Ex is UnauthorizedAccessException) { }
            }
        }
        catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception Ex) when (Ex is HttpRequestException || Ex is IOException || Ex is JsonException || Ex is OperationCanceledException)
        {
            Result.ResponseJsonText = Cached;
            Result.ResultStatus = Cached == null ? ApiClientResultStatus.Error : ApiClientResultStatus.Success;
            Result.ReasonPhrase = Cached == null ? "Wikipedia could not be loaded. Check your connection and try again."
                : "Wikipedia could not be refreshed. Previously downloaded content is shown.";
        }
        return Result;
    }

    // ● public
    /// <summary>
    /// Searches article titles and text in English Wikipedia, retaining server-ranked results and continuation.
    /// </summary>
    /// <param name="Search">The user's editable search expression.</param>
    /// <param name="Offset">The search continuation offset, initially zero.</param>
    /// <param name="Limit">The requested page size from 10 to 100.</param>
    /// <param name="CancellationToken">Cancels the request.</param>
    /// <returns>The raw search response, including titles, snippets, total hits and continuation.</returns>
    static public Task<ApiClientResult> SearchAsync(string Search, int Offset, int Limit, CancellationToken CancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Search);
        ArgumentOutOfRangeException.ThrowIfNegative(Offset);
        ArgumentOutOfRangeException.ThrowIfLessThan(Limit, 10);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(Limit, 100);
        return LoadAsync("list=search&srnamespace=0&srprop=snippet&srinfo=totalhits&srsearch="
            + Uri.EscapeDataString(Search.Trim()) + "&sroffset=" + Offset.ToString(CultureInfo.InvariantCulture)
            + "&srlimit=" + Limit.ToString(CultureInfo.InvariantCulture), CancellationToken);
    }
    /// <summary>
    /// Reads an introductory plain-text extract and canonical article URL for a selected page identifier.
    /// </summary>
    /// <param name="PageId">The selected Wikipedia page identifier.</param>
    /// <param name="CancellationToken">Cancels the request.</param>
    /// <returns>The article JSON, preserving missing-page and disambiguation properties.</returns>
    static public Task<ApiClientResult> GetArticleAsync(long PageId, CancellationToken CancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(PageId);
        return LoadAsync("prop=extracts%7Cinfo%7Cpageprops&exintro=1&explaintext=1&exchars=1200&inprop=url&redirects=1&pageids="
            + PageId.ToString(CultureInfo.InvariantCulture), CancellationToken);
    }
}
