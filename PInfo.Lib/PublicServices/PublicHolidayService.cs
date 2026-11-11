/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo;

/// <summary>
/// Reads Vacanza public-holiday JSON calendars and their supported-country manifest with offline caching.
/// </summary>
static public class PublicHolidayService
{
    // ● private
    static readonly SemaphoreSlim fGate = new(1, 1);
    static long fNextRequest;
    static DateTimeOffset fRetryAfter;

    /// <summary>
    /// Checks the response shape before displaying or caching provider data.
    /// </summary>
    static void Validate(string Json, bool Catalogue)
    {
        using JsonDocument Document = JsonDocument.Parse(Json);
        JsonElement Root = Document.RootElement;
        if (Catalogue)
        {
            if (Root.GetProperty("countries").ValueKind != JsonValueKind.Object)
                throw new JsonException("Invalid holiday country catalogue.");
            foreach (JsonProperty Entry in Root.GetProperty("countries").EnumerateObject())
            {
                JsonElement Country = Entry.Value;
                if (Country.GetProperty("languages").ValueKind != JsonValueKind.Object
                    || Country.GetProperty("categories").ValueKind != JsonValueKind.Array
                    || Country.GetProperty("categories").EnumerateArray().Any(Value => Value.ValueKind != JsonValueKind.String))
                    throw new JsonException("Invalid holiday catalogue entry.");
            }
        }
        else
        {
            if (Root.ValueKind != JsonValueKind.Array)
                throw new JsonException("Invalid holiday calendar.");
            foreach (JsonElement Item in Root.EnumerateArray())
            {
                if (!DateOnly.TryParseExact(Item.GetProperty("date").GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out _) || string.IsNullOrWhiteSpace(Item.GetProperty("name").GetString()))
                    throw new JsonException("Invalid holiday date or name.");
            }
        }
    }
    /// <summary>
    /// Caches validated calendars and serializes requests, retaining older data on refresh failures.
    /// </summary>
    static async Task<ApiClientResult> LoadAsync(string Url, bool Catalogue, CancellationToken CancellationToken)
    {
        ApiClientResult Result = new(Url);
        string Key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(Url)));
        string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PInfo", "Data", "Holidays", Key + ".json");
        await fGate.WaitAsync(CancellationToken).ConfigureAwait(false);
        try
        {
            string Cached = null;
            try
            {
                if (File.Exists(FilePath))
                {
                    Cached = await File.ReadAllTextAsync(FilePath, CancellationToken).ConfigureAwait(false);
                    Validate(Cached, Catalogue);
                    DateTime Written = File.GetLastWriteTimeUtc(FilePath);
                    if (Written <= DateTime.UtcNow && Written > DateTime.UtcNow.AddDays(-Lib.Settings.HolidaysRefreshDays))
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
                    throw new HttpRequestException("Holiday data is temporarily limiting requests. Please try again later.");
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
                Validate(Json, Catalogue);
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
                    Result.ReasonPhrase = "Holiday data could not be saved for offline use.";
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
                    ? "Holiday data is temporarily limiting requests. Please try again later."
                    : "Holiday data could not be loaded. Check your connection and try again.";
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

    // ● public
    /// <summary>
    /// Loads one country's public calendar, preferring English where offered by the manifest.
    /// </summary>
    /// <param name="CountryCode">The selected country's ISO alpha-2 code.</param>
    /// <param name="CancellationToken">Cancels requests, waits and cache operations.</param>
    /// <returns>The outcome, selected language and detached holiday records across available years.</returns>
    static public async Task<(ApiClientResult Result, string Language, IReadOnlyList<JsonElement> Items)> LoadCalendarAsync(
        string CountryCode, CancellationToken CancellationToken = default)
    {
        string Code = (CountryCode ?? string.Empty).Trim().ToUpperInvariant();
        if (Code.Length != 2 || Code.Any(Character => Character < 'A' || Character > 'Z'))
            throw new ArgumentException("A two-letter country code is required.", nameof(CountryCode));
        const string BaseUrl = "https://vacanza.github.io/holidays/downloads/ics/";
        ApiClientResult Catalogue = await LoadAsync(BaseUrl + "index.json", true, CancellationToken).ConfigureAwait(false);
        if (Catalogue.ResultStatus != ApiClientResultStatus.Success)
            return (Catalogue, string.Empty, Array.Empty<JsonElement>());
        try
        {
            using JsonDocument Manifest = JsonDocument.Parse(Catalogue.ResponseJsonText);
            if (!Manifest.RootElement.GetProperty("countries").TryGetProperty(Code, out JsonElement Country)
                || !Country.GetProperty("categories").EnumerateArray().Any(Category => Category.GetString() == "public"))
                return (new ApiClientResult(Catalogue.ActionUrl)
                {
                    ReasonPhrase = "A public holiday calendar is not available for this country."
                }, string.Empty, Array.Empty<JsonElement>());
            JsonElement Languages = Country.GetProperty("languages");
            string Language = Languages.TryGetProperty("en_US", out _) ? "en_US" : Country.GetProperty("default_language").GetString();
            if (string.IsNullOrWhiteSpace(Language) || !Languages.TryGetProperty(Language, out _)
                || Language.Any(Character => !(Character >= 'a' && Character <= 'z')
                    && !(Character >= 'A' && Character <= 'Z') && Character != '_' && Character != '-'))
                throw new JsonException("The holiday language is unavailable.");
            ApiClientResult Calendar = await LoadAsync(BaseUrl + "countries/" + Code + "/ALL_"
                + Uri.EscapeDataString(Language) + "_public.json", false, CancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(Catalogue.ReasonPhrase))
                Calendar.ReasonPhrase = string.Join("\n", new[] { Catalogue.ReasonPhrase, Calendar.ReasonPhrase }
                    .Where(Value => !string.IsNullOrWhiteSpace(Value)));
            if (Calendar.ResultStatus != ApiClientResultStatus.Success)
                return (Calendar, Language, Array.Empty<JsonElement>());
            using JsonDocument Document = JsonDocument.Parse(Calendar.ResponseJsonText);
            return (Calendar, Language, Document.RootElement.EnumerateArray().Select(Item => Item.Clone()).ToList());
        }
        catch (Exception Ex) when (Ex is JsonException || Ex is InvalidOperationException || Ex is KeyNotFoundException)
        {
            return (new ApiClientResult(Catalogue.ActionUrl) { ReasonPhrase = "The holiday catalogue could not be read. Please try again." },
                string.Empty, Array.Empty<JsonElement>());
        }
    }
}
