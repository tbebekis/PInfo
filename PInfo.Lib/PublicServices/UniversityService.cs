/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo;

/// <summary>
/// Reads the Hipo university catalogue securely and caches it for local searching and offline browsing.
/// </summary>
public class UniversityService : PublicService
{
    // ● private
    static readonly SemaphoreSlim fGate = new(1, 1);

    /// <summary>
    /// Checks the response shape before displaying or caching provider data.
    /// </summary>
    static void Validate(string Json, CancellationToken CancellationToken, IProgress<string> Progress)
    {
        using JsonDocument Document = JsonDocument.Parse(Json);
        JsonElement Root = Document.RootElement;
        if (Root.ValueKind != JsonValueKind.Array || Root.GetArrayLength() == 0)
            throw new JsonException("Invalid university catalogue.");
        long Started = Environment.TickCount64;
        long LastReport = Started;
        int Completed = 0;
        int Total = Root.GetArrayLength();
        foreach (JsonElement Item in Root.EnumerateArray())
        {
            CancellationToken.ThrowIfCancellationRequested();
            if (Item.ValueKind != JsonValueKind.Object
                || string.IsNullOrWhiteSpace(Item.GetProperty("name").GetString())
                || string.IsNullOrWhiteSpace(Item.GetProperty("country").GetString())
                || Item.GetProperty("alpha_two_code").GetString()?.Length != 2)
                throw new JsonException("Invalid university identity.");
            if (Item.TryGetProperty("state-province", out JsonElement Region)
                && Region.ValueKind != JsonValueKind.Null && Region.ValueKind != JsonValueKind.String)
                throw new JsonException("Invalid university region.");
            foreach (string Name in new[] { "domains", "web_pages" })
            {
                JsonElement Values = Item.GetProperty(Name);
                if (Values.ValueKind != JsonValueKind.Array
                    || Values.EnumerateArray().Any(Value => Value.ValueKind != JsonValueKind.String))
                    throw new JsonException("Invalid university links.");
            }
            Completed++;
            if (Progress != null && Environment.TickCount64 - LastReport >= 500)
            {
                Progress.Report($"Checking university data... {Completed:N0} / {Total:N0} records"
                    + PublicApiClient.EstimateRemainingTime(Completed, Total, Environment.TickCount64 - Started));
                LastReport = Environment.TickCount64;
            }
        }
    }
    /// <summary>
    /// Reads validated cached JSON and serializes catalogue refreshes across service instances.
    /// </summary>
    async Task<ApiClientResult> LoadAsync(CancellationToken CancellationToken, IProgress<string> Progress)
    {
        const string Url = "https://raw.githubusercontent.com/Hipo/university-domains-list/master/world_universities_and_domains.json";
        ApiClientResult Result = new(Url);
        string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PInfo", "Data", "Universities", "catalogue.json");
        Progress?.Report("Waiting for university data...");
        await fGate.WaitAsync(CancellationToken).ConfigureAwait(false);
        try
        {
            string Cached = null;
            try
            {
                if (File.Exists(FilePath))
                {
                    Progress?.Report("Reading saved university data...");
                    Cached = await File.ReadAllTextAsync(FilePath, CancellationToken).ConfigureAwait(false);
                    Progress?.Report("Checking saved university data...");
                    Validate(Cached, CancellationToken, Progress);
                    DateTime Written = File.GetLastWriteTimeUtc(FilePath);
                    if (Written <= DateTime.UtcNow && Written > DateTime.UtcNow.AddDays(-Lib.Settings.UniversitiesRefreshDays))
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
            string TemporaryPath = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                Progress?.Report("Downloading university catalogue...\nRemaining time not yet available.");
                ApiClientResult Download = await PublicApiClient.DownloadFileAsync(Url, TemporaryPath,
                    CancellationToken, Progress == null ? null : new DownloadProgress(Progress)).ConfigureAwait(false);
                CancellationToken.ThrowIfCancellationRequested();
                Result.StatusCode = Download.StatusCode;
                if (Download.ResultStatus != ApiClientResultStatus.Success)
                    throw new HttpRequestException(Download.ReasonPhrase);
                Progress?.Report("Reading downloaded university data...");
                string Json = await File.ReadAllTextAsync(TemporaryPath, CancellationToken).ConfigureAwait(false);
                Progress?.Report("Checking university data...");
                Validate(Json, CancellationToken, Progress);
                CancellationToken.ThrowIfCancellationRequested();
                Result.ResponseJsonText = Json;
                Result.ResultStatus = ApiClientResultStatus.Success;
                try
                {
                    Progress?.Report("Saving university data for offline use...");
                    File.Move(TemporaryPath, FilePath, true);
                }
                catch (Exception Ex) when (Ex is IOException || Ex is UnauthorizedAccessException)
                {
                    Result.ReasonPhrase = "University data could not be saved for offline use.";
                }
            }
            catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception Ex) when (Ex is HttpRequestException || Ex is IOException || Ex is UnauthorizedAccessException || Ex is JsonException
                || Ex is OperationCanceledException || Ex is KeyNotFoundException || Ex is InvalidOperationException || Ex is FormatException)
            {
                Result.ResponseJsonText = Cached;
                Result.ResultStatus = Cached == null ? ApiClientResultStatus.Error : ApiClientResultStatus.Success;
                Result.ReasonPhrase = "The university catalogue could not be loaded. Check your connection and try again.";
                if (Cached != null)
                    Result.ReasonPhrase += " Previously downloaded content is shown.";
            }
            finally
            {
                try { File.Delete(TemporaryPath); }
                catch (Exception Ex) when (Ex is IOException || Ex is UnauthorizedAccessException) { }
            }
            return Result;
        }
        finally
        {
            fGate.Release();
        }
    }

    /// <summary>
    /// Adapts the shared file downloader's ZIP labels for the university JSON catalogue.
    /// </summary>
    sealed class DownloadProgress : IProgress<string>
    {
        // ● private
        readonly IProgress<string> fProgress;

        // ● constructor
        /// <summary>
        /// Retains the caller's progress reporter without creating another dispatch queue.
        /// </summary>
        public DownloadProgress(IProgress<string> Progress)
        {
            fProgress = Progress;
        }

        // ● public
        /// <summary>
        /// Forwards byte counts and approximate stage estimates with the correct resource name.
        /// </summary>
        public void Report(string Value)
        {
            string Message = Value.Replace("Downloading ZIP file", "Downloading university catalogue");
            if (!Message.Contains("remaining", StringComparison.OrdinalIgnoreCase)
                && !Message.Contains("Finishing", StringComparison.Ordinal))
                Message += Value.Contains(" / ", StringComparison.Ordinal)
                    ? "\nRemaining time: calculating..." : "\nRemaining time unavailable (download size not supplied).";
            fProgress.Report(Message);
        }
    }

    // ● constructor
    /// <summary>
    /// Creates the Universities service without performing any network requests.
    /// </summary>
    public UniversityService() : base("Universities")
    {
    }

    // ● public
    /// <summary>
    /// Loads the complete catalogue for local name searches, paging and offline details.
    /// </summary>
    /// <param name="CancellationToken">Cancels waiting, network access and cache operations.</param>
    /// <param name="Progress">Receives download bytes, approximate stage time remaining and processing stages.</param>
    /// <returns>The validated catalogue JSON, with a warning when older cached data is used.</returns>
    public Task<ApiClientResult> LoadUniversitiesAsync(CancellationToken CancellationToken = default, IProgress<string> Progress = null)
    {
        return Task.Run(() => LoadAsync(CancellationToken, Progress), CancellationToken);
    }
}
