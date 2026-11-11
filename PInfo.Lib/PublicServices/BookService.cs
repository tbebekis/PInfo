/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo;

/// <summary>
/// Provides human-triggered Open Library work searches and details through the shared HTTP client.
/// </summary>
public class BookService : PublicService
{
    // ● private
    static readonly SemaphoreSlim fGate = new(1, 1);
    static long fNextRequest;
    static DateTimeOffset fRetryAfter;

    /// <summary>
    /// Checks the response shape before displaying or caching provider data.
    /// </summary>
    static void Validate(string Json, string WorkKey)
    {
        using JsonDocument Document = JsonDocument.Parse(Json);
        JsonElement Root = Document.RootElement;
        if (Root.ValueKind != JsonValueKind.Object || Root.TryGetProperty("error", out _))
            throw new JsonException("Invalid Open Library response.");
        if (WorkKey != null)
        {
            if (Root.GetProperty("key").GetString() != WorkKey
                || string.IsNullOrWhiteSpace(Root.GetProperty("title").GetString()))
                throw new JsonException("The selected work was not returned.");
        }
        else
        {
            if (Root.GetProperty("numFound").GetInt64() < 0
                || Root.GetProperty("docs").ValueKind != JsonValueKind.Array)
                throw new JsonException("Invalid Open Library search.");
            foreach (JsonElement Item in Root.GetProperty("docs").EnumerateArray())
            {
                if (!IsWorkKey(Item.GetProperty("key").GetString())
                    || string.IsNullOrWhiteSpace(Item.GetProperty("title").GetString()))
                    throw new JsonException("Invalid work in search results.");
                if (Item.TryGetProperty("author_name", out JsonElement Authors)
                    && (Authors.ValueKind != JsonValueKind.Array
                        || Authors.EnumerateArray().Any(Author => Author.ValueKind != JsonValueKind.String)))
                    throw new JsonException("Invalid authors in search results.");
                if (Item.TryGetProperty("first_publish_year", out JsonElement Year) && !Year.TryGetInt32(out _))
                    throw new JsonException("Invalid publication year.");
                if (Item.TryGetProperty("edition_count", out JsonElement Editions) && (!Editions.TryGetInt32(out int Count) || Count < 0))
                    throw new JsonException("Invalid edition count.");
            }
        }
    }
    /// <summary>
    /// Reads validated cached JSON and serializes all Open Library requests at one request per second.
    /// </summary>
    async Task<ApiClientResult> LoadAsync(string PathAndQuery, string WorkKey, CancellationToken CancellationToken)
    {
        string Url = "https://openlibrary.org" + PathAndQuery;
        ApiClientResult Result = new(Url);
        string Key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(Url)));
        string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PInfo", "Data", "Books", Key + ".json");
        await fGate.WaitAsync(CancellationToken).ConfigureAwait(false);
        try
        {
            string Cached = null;
            try
            {
                if (File.Exists(FilePath))
                {
                    Cached = await File.ReadAllTextAsync(FilePath, CancellationToken).ConfigureAwait(false);
                    Validate(Cached, WorkKey);
                    DateTime Written = File.GetLastWriteTimeUtc(FilePath);
                    if (Written <= DateTime.UtcNow && Written > DateTime.UtcNow.AddDays(-(WorkKey == null ? Lib.Settings.BookSearchRefreshDays : Lib.Settings.BookDetailsRefreshDays)))
                    {
                        Result.ResponseJsonText = Cached;
                        Result.ResultStatus = ApiClientResultStatus.Success;
                        return Result;
                    }
                }
            }
            catch (Exception Ex) when (Ex is IOException || Ex is UnauthorizedAccessException || Ex is JsonException
                || Ex is KeyNotFoundException || Ex is InvalidOperationException || Ex is FormatException)
            {
                Cached = null;
            }
            try
            {
                if (fRetryAfter > DateTimeOffset.UtcNow)
                    throw new HttpRequestException("Open Library is temporarily limiting requests. Please try again later.");
                long Delay = fNextRequest - Environment.TickCount64;
                if (Delay > 0)
                    await Task.Delay(TimeSpan.FromMilliseconds(Delay), CancellationToken).ConfigureAwait(false);
                using CancellationTokenSource Deadline = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
                Deadline.CancelAfter(TimeSpan.FromSeconds(30));
                using HttpRequestMessage Request = new(HttpMethod.Get, Url);
                Request.Headers.UserAgent.ParseAdd("PInfo/1.0 (+https://github.com/tbebekis/PInfo)");
                fNextRequest = Environment.TickCount64 + 1000;
                using HttpResponseMessage Response = await PublicApiClient.SendAsync(Request, Deadline.Token).ConfigureAwait(false);
                Result.StatusCode = Response.StatusCode;
                if (Response.StatusCode == HttpStatusCode.TooManyRequests || Response.StatusCode == HttpStatusCode.ServiceUnavailable)
                {
                    DateTimeOffset Until = Response.Headers.RetryAfter?.Date
                        ?? DateTimeOffset.UtcNow.Add(Response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(30));
                    fRetryAfter = Until > DateTimeOffset.UtcNow ? Until : DateTimeOffset.UtcNow.AddSeconds(1);
                }
                Response.EnsureSuccessStatusCode();
                string Json = await Response.Content.ReadAsStringAsync(Deadline.Token).ConfigureAwait(false);
                Validate(Json, WorkKey);
                CancellationToken.ThrowIfCancellationRequested();
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
                    Result.ReasonPhrase = "Book data could not be saved for offline use.";
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
            catch (Exception Ex) when (Ex is HttpRequestException || Ex is IOException || Ex is JsonException
                || Ex is OperationCanceledException || Ex is KeyNotFoundException || Ex is InvalidOperationException || Ex is FormatException)
            {
                Result.ResponseJsonText = Cached;
                Result.ResultStatus = Cached == null ? ApiClientResultStatus.Error : ApiClientResultStatus.Success;
                Result.ReasonPhrase = fRetryAfter > DateTimeOffset.UtcNow
                    ? "Open Library is temporarily limiting requests. Please try again later."
                    : "Open Library could not be loaded. Check your connection and try again.";
                if (Cached != null)
                    Result.ReasonPhrase += " Previously downloaded content is shown.";
            }
            return Result;
        }
        finally
        {
            fGate.Release();
        }
    }

    // ● constructor
    /// <summary>
    /// Creates the Books service without performing any network requests.
    /// </summary>
    public BookService() : base("Books")
    {
    }

    // ● public
    /// <summary>
    /// Accepts only canonical Open Library work keys, never arbitrary paths or addresses.
    /// </summary>
    static public bool IsWorkKey(string Key)
    {
        const string Prefix = "/works/OL";
        return Key != null && Key.StartsWith(Prefix, StringComparison.Ordinal)
            && Key.Length > Prefix.Length + 1 && Key.EndsWith("W", StringComparison.Ordinal)
            && Key.Substring(Prefix.Length, Key.Length - Prefix.Length - 1).All(Character => Character >= '0' && Character <= '9');
    }
    /// <summary>
    /// Searches work titles, authors or ISBNs with server-ranked, offset-based paging.
    /// </summary>
    public Task<ApiClientResult> SearchAsync(string Search, int Offset, int Limit, CancellationToken CancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Search);
        ArgumentOutOfRangeException.ThrowIfNegative(Offset);
        ArgumentOutOfRangeException.ThrowIfLessThan(Limit, 10);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(Limit, 100);
        return LoadAsync("/search.json?q=" + Uri.EscapeDataString(Search.Trim())
            + "&fields=key,title,author_name,first_publish_year,edition_count&limit=" + Limit.ToString(CultureInfo.InvariantCulture)
            + "&offset=" + Offset.ToString(CultureInfo.InvariantCulture), null, CancellationToken);
    }
    /// <summary>
    /// Reads the selected work without conflating it with an individual edition.
    /// </summary>
    public Task<ApiClientResult> GetWorkAsync(string WorkKey, CancellationToken CancellationToken = default)
    {
        if (!IsWorkKey(WorkKey))
            throw new ArgumentException("A canonical Open Library work key is required.", nameof(WorkKey));
        return LoadAsync(WorkKey + ".json", WorkKey, CancellationToken);
    }
}
