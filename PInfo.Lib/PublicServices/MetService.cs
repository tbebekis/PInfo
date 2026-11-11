/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo;

/// <summary>
/// Provides browsable Met collections, artwork metadata and public-domain images through the shared HTTP client.
/// </summary>
public class MetService : PublicService
{
    // ● private
    static readonly SemaphoreSlim fGate = new(1, 1);
    static long fNextRequest;
    static DateTimeOffset fRetryAfter;

    /// <summary>
    /// Checks catalogue, search and object identities before publishing or saving JSON.
    /// </summary>
    static void Validate(string Json, string Kind)
    {
        using JsonDocument Document = JsonDocument.Parse(Json);
        JsonElement Root = Document.RootElement;
        if (Kind == "departments")
        {
            foreach (JsonElement Item in Root.GetProperty("departments").EnumerateArray())
                if (Item.GetProperty("departmentId").GetInt32() <= 0 || string.IsNullOrWhiteSpace(Item.GetProperty("displayName").GetString()))
                    throw new JsonException("Invalid Met department.");
        }
        else if (Kind == "search")
        {
            if (Root.GetProperty("total").GetInt32() < 0)
                throw new JsonException("Invalid Met result count.");
            JsonElement Items = Root.GetProperty("objectIDs");
            if (Items.ValueKind != JsonValueKind.Null)
                foreach (JsonElement Item in Items.EnumerateArray())
                    if (Item.GetInt32() <= 0)
                        throw new JsonException("Invalid Met object ID.");
        }
        else if (Root.GetProperty("objectID").GetInt32().ToString(CultureInfo.InvariantCulture) != Kind
            || string.IsNullOrWhiteSpace(Root.GetProperty("title").GetString()))
            throw new JsonException("The requested Met object was not returned.");
    }
    /// <summary>
    /// Reads validated cached JSON and serializes Met requests at most ten requests per second.
    /// </summary>
    async Task<ApiClientResult> LoadAsync(string PathAndQuery, string Kind, CancellationToken CancellationToken)
    {
        string Url = "https://collectionapi.metmuseum.org" + PathAndQuery;
        ApiClientResult Result = new(Url);
        string Key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(Url)));
        string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PInfo", "Data", "Met", Key + ".json");
        await fGate.WaitAsync(CancellationToken).ConfigureAwait(false);
        try
        {
            string Cached = null;
            try
            {
                if (File.Exists(FilePath))
                {
                    Cached = await File.ReadAllTextAsync(FilePath, CancellationToken).ConfigureAwait(false);
                    Validate(Cached, Kind);
                    DateTime Written = File.GetLastWriteTimeUtc(FilePath);
                    if (Written <= DateTime.UtcNow && Written > DateTime.UtcNow.AddDays(-Lib.Settings.ArtRefreshDays))
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
                    throw new HttpRequestException("The Met is temporarily limiting requests. Please try again later.");
                long Delay = fNextRequest - Environment.TickCount64;
                if (Delay > 0)
                    await Task.Delay(TimeSpan.FromMilliseconds(Delay), CancellationToken).ConfigureAwait(false);
                using CancellationTokenSource Deadline = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
                Deadline.CancelAfter(TimeSpan.FromSeconds(30));
                using HttpRequestMessage Request = new(HttpMethod.Get, Url);
                Request.Headers.UserAgent.ParseAdd("PInfo/1.0");
                fNextRequest = Environment.TickCount64 + 100;
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
                Validate(Json, Kind);
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
                    Result.ReasonPhrase = "Art data could not be saved for offline use.";
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
                    ? "The Met is temporarily limiting requests. Please try again later."
                    : "The Met could not be loaded. Check your connection and try again.";
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
    /// Creates the Art service without performing any network requests.
    /// </summary>
    public MetService() : base("Art - The Met")
    {
    }

    // ● public
    /// <summary>
    /// Reads the museum's current collection departments.
    /// </summary>
    public Task<ApiClientResult> GetDepartmentsAsync(CancellationToken CancellationToken = default)
    {
        return LoadAsync("/public/collection/v1/departments", "departments", CancellationToken);
    }
    /// <summary>
    /// Browses a collection using a wildcard when the optional query is empty, with server-side paging.
    /// </summary>
    public Task<ApiClientResult> SearchAsync(string Search, int Department, bool WithImages, int Offset, int Limit, CancellationToken CancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(Department);
        ArgumentOutOfRangeException.ThrowIfNegative(Offset);
        ArgumentOutOfRangeException.ThrowIfLessThan(Limit, 10);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(Limit, 100);
        return LoadAsync("/public/collection/v1.1/search?q=" + Uri.EscapeDataString(string.IsNullOrWhiteSpace(Search) ? "*" : Search.Trim())
            + (Department == 0 ? string.Empty : "&departmentId=" + Department.ToString(CultureInfo.InvariantCulture))
            + (WithImages ? "&hasImages=true" : string.Empty)
            + "&offset=" + Offset.ToString(CultureInfo.InvariantCulture) + "&limit=" + Limit.ToString(CultureInfo.InvariantCulture), "search", CancellationToken);
    }
    /// <summary>
    /// Reads one artwork by its stable museum identifier.
    /// </summary>
    public Task<ApiClientResult> GetObjectAsync(int Id, CancellationToken CancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(Id);
        return LoadAsync("/public/collection/v1/objects/" + Id.ToString(CultureInfo.InvariantCulture), Id.ToString(CultureInfo.InvariantCulture), CancellationToken);
    }
    /// <summary>
    /// Returns an optional catalogue string, including for an unavailable recycled list item.
    /// </summary>
    static public string Text(JsonElement Item, string Name)
    {
        return Item.ValueKind == JsonValueKind.Object && Item.TryGetProperty(Name, out JsonElement Value)
            && Value.ValueKind == JsonValueKind.String ? Value.GetString() : string.Empty;
    }
    /// <summary>
    /// Returns only an explicitly public-domain image served by the museum.
    /// </summary>
    static public string ImageUrl(JsonElement Item)
    {
        string Url = Text(Item, "primaryImageSmall");
        return Item.ValueKind == JsonValueKind.Object && Item.TryGetProperty("isPublicDomain", out JsonElement PublicDomain)
            && PublicDomain.ValueKind == JsonValueKind.True && Uri.TryCreate(Url, UriKind.Absolute, out Uri Address)
            && Address.Scheme == "https" && Address.Host == "images.metmuseum.org" && string.IsNullOrEmpty(Address.UserInfo)
            ? Url : string.Empty;
    }
    /// <summary>
    /// Downloads public-domain previews with a retained-size limit through the shared transport, retaining saved images for offline use.
    /// </summary>
    public async Task<byte[]> GetImageAsync(JsonElement Item, CancellationToken CancellationToken = default)
    {
        string Url = ImageUrl(Item);
        if (Url.Length == 0)
            return null;
        string Key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(Url)));
        string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PInfo", "Data", "Met", Key + ".jpg");
        await fGate.WaitAsync(CancellationToken).ConfigureAwait(false);
        try
        {
            if (File.Exists(FilePath))
            {
                try { return await File.ReadAllBytesAsync(FilePath, CancellationToken).ConfigureAwait(false); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            using CancellationTokenSource Deadline = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
            Deadline.CancelAfter(TimeSpan.FromSeconds(30));
            long Delay = fNextRequest - Environment.TickCount64;
            if (Delay > 0)
                await Task.Delay(TimeSpan.FromMilliseconds(Delay), Deadline.Token).ConfigureAwait(false);
            if (fRetryAfter > DateTimeOffset.UtcNow)
                return null;
            fNextRequest = Environment.TickCount64 + 100;
            using HttpRequestMessage Request = new(HttpMethod.Get, Url);
            Request.Headers.UserAgent.ParseAdd("PInfo/1.0");
            using HttpResponseMessage Response = await PublicApiClient.SendAsync(Request, Deadline.Token).ConfigureAwait(false);
            if (Response.StatusCode == HttpStatusCode.TooManyRequests || Response.StatusCode == HttpStatusCode.ServiceUnavailable)
                fRetryAfter = Response.Headers.RetryAfter?.Date ?? DateTimeOffset.UtcNow.Add(Response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(30));
            Response.EnsureSuccessStatusCode();
            const int MaximumBytes = 8 * 1024 * 1024;
            if (Response.Content.Headers.ContentLength > MaximumBytes)
                return null;
            await using Stream Input = await Response.Content.ReadAsStreamAsync(Deadline.Token).ConfigureAwait(false);
            using MemoryStream Output = new();
            byte[] Buffer = new byte[16384];
            int Count;
            while ((Count = await Input.ReadAsync(Buffer, Deadline.Token).ConfigureAwait(false)) > 0)
            {
                if (Output.Length + Count > MaximumBytes)
                    return null;
                Output.Write(Buffer, 0, Count);
            }
            byte[] Bytes = Output.ToArray();
            string TemporaryPath = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                await File.WriteAllBytesAsync(TemporaryPath, Bytes, CancellationToken).ConfigureAwait(false);
                File.Move(TemporaryPath, FilePath, true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            finally
            {
                try { File.Delete(TemporaryPath); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            return Bytes;
        }
        catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested) { throw; }
        catch (Exception Ex) when (Ex is HttpRequestException || Ex is IOException || Ex is OperationCanceledException) { return null; }
        finally { fGate.Release(); }
    }
}
