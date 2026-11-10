/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo;

/// <summary>
/// Loads annual World Development and Worldwide Governance Indicators for one country with validated offline caching.
/// </summary>
static public class WorldBankService
{
    // ● private
    static readonly ConcurrentDictionary<string, SemaphoreSlim> fLoadLocks = new(StringComparer.Ordinal);
    static readonly ReadOnlyDictionary<string, (string Name, string Unit, string Description)> fIndicators = new(
        new Dictionary<string, (string Name, string Unit, string Description)>
        {
            ["SP.POP.TOTL"] = ("Population", "people", "Total population, including the World Bank's population estimates."),
            ["NY.GDP.PCAP.CD"] = ("GDP per capita", "current US$", "Gross domestic product per person in current US dollars. Values are not adjusted for inflation."),
            ["FP.CPI.TOTL.ZG"] = ("Inflation", "% annually", "Annual percentage change in consumer prices. Negative values indicate falling consumer prices."),
            ["SL.UEM.TOTL.ZS"] = ("Unemployment", "% of labour force", "Total unemployment as a percentage of the labour force, using the modeled ILO estimate."),
            ["SP.DYN.LE00.IN"] = ("Life expectancy", "years", "Life expectancy at birth, total population, in years."),
            ["NY.GDP.MKTP.KD.ZG"] = ("GDP growth", "% annually", "Annual growth of gross domestic product in real terms, based on constant local currency. Negative values indicate economic contraction."),
            ["IT.NET.USER.ZS"] = ("Internet use", "% of population", "Individuals using the Internet as a percentage of the total population."),
            ["SH.XPD.CHEX.GD.ZS"] = ("Health expenditure", "% of GDP", "Current health expenditure as a percentage of GDP, including public and private spending. Capital investment is excluded."),
            ["AG.LND.FRST.ZS"] = ("Forest area", "% of land area", "Forest area as a percentage of the country's land area."),
            ["VC.IHR.PSRC.P5"] = ("Intentional homicides", "per 100,000 people", "Intentional homicides per 100,000 people, from UNODC via World Bank. This measures homicide, not overall crime or personal safety. Lower values mean fewer recorded homicides per person; reporting and coverage vary."),
            ["GOV_WGI_VA.SC"] = ("Voice and accountability", "governance score (0–100)", "A perception-based composite covering participation, accountability, access to information and media. It is broader than freedom of expression and is not a dedicated press-freedom ranking. Higher scores indicate better governance. WGI estimates have uncertainty; small differences should not be overinterpreted."),
            ["GOV_WGI_CC.SC"] = ("Control of corruption", "governance score (0–100)", "A perception-based composite of the extent to which public power is used for private gain. Higher scores mean stronger control of corruption, not more corruption. This is not a count of corruption incidents. WGI estimates have uncertainty."),
            ["GOV_WGI_RL.SC"] = ("Rule of law", "governance score (0–100)", "A perception-based composite covering confidence in societal rules, contract enforcement, property rights, police and courts. Higher scores indicate better governance. WGI estimates have uncertainty."),
            ["GOV_WGI_PV.SC"] = ("Political stability", "governance score (0–100)", "A perception-based composite covering political stability and the risk of violent, coercive or unconstitutional challenges to authority. Higher scores indicate better governance. WGI estimates have uncertainty.")
        });

    /// <summary>
    /// Validates a country identifier before including it in a request or local filename.
    /// </summary>
    /// <param name="CountryCode">The selected country's ISO alpha-3 code.</param>
    /// <returns>The normalized identifier.</returns>
    static string NormalizeCountry(string CountryCode)
    {
        string Code = (CountryCode ?? string.Empty).Trim().ToUpperInvariant();
        if (Code.Length != 3 || Code.Any(Character => Character < 'A' || Character > 'Z'))
            throw new ArgumentException("An ISO alpha-3 country code is required.", nameof(CountryCode));
        return Code;
    }

    // ● public
    /// <summary>
    /// Reads complete annual series, preserving missing values and rejecting partial or mismatched responses.
    /// </summary>
    /// <param name="Json">The original World Bank API response.</param>
    /// <param name="CountryCode">The requested ISO alpha-3 country code.</param>
    /// <param name="Updated">The selected source database update date, when supplied.</param>
    /// <param name="SourceId">Source 2 for WDI or source 3 for WGI.</param>
    /// <returns>The selected source's configured indicators, ordered by year.</returns>
    static public IReadOnlyDictionary<string, IReadOnlyList<(int Year, decimal? Value)>> ReadSeries(
        string Json, string CountryCode, out string Updated, string SourceId = "2")
    {
        string Code = NormalizeCountry(CountryCode);
        GetSourceName(SourceId);
        using JsonDocument Document = JsonDocument.Parse(Json);
        JsonElement Root = Document.RootElement;
        if (Root.ValueKind != JsonValueKind.Array || Root.GetArrayLength() != 2
            || Root[0].ValueKind != JsonValueKind.Object || Root[0].TryGetProperty("message", out _))
            throw new JsonException("World Bank did not return a country data response.");
        JsonElement Metadata = Root[0];
        if (Metadata.GetProperty("pages").GetInt32() > 1 || Metadata.GetProperty("page").GetInt32() != 1)
            throw new JsonException("World Bank returned an incomplete data page.");
        if (Metadata.TryGetProperty("sourceid", out JsonElement Source)
            && Source.ValueKind != JsonValueKind.Null && Source.GetString() != SourceId)
            throw new JsonException("World Bank returned a different data source.");
        Updated = Metadata.TryGetProperty("lastupdated", out JsonElement Date) ? Date.GetString() ?? string.Empty : string.Empty;
        Dictionary<string, SortedDictionary<int, decimal?>> Series = fIndicators.Keys.Where(Key => GetSourceId(Key) == SourceId).ToDictionary(Key => Key,
            Key => new SortedDictionary<int, decimal?>(), StringComparer.Ordinal);
        JsonElement Records = Root[1];
        if (Records.ValueKind != JsonValueKind.Null && Records.ValueKind != JsonValueKind.Array)
            throw new JsonException("World Bank returned an invalid observation collection.");
        int Count = Records.ValueKind == JsonValueKind.Array ? Records.GetArrayLength() : 0;
        if (Metadata.GetProperty("total").GetInt32() != Count)
            throw new JsonException("World Bank returned a partial observation collection.");
        if (Records.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement Item in Records.EnumerateArray())
            {
                string Indicator = Item.GetProperty("indicator").GetProperty("id").GetString();
                if (!Series.TryGetValue(Indicator, out SortedDictionary<int, decimal?> Points)
                    || Item.GetProperty("countryiso3code").GetString() != Code
                    || !int.TryParse(Item.GetProperty("date").GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out int Year)
                    || Year < 1800 || Year > 3000)
                    throw new JsonException("World Bank returned mismatched identifiers or an invalid year.");
                JsonElement Value = Item.GetProperty("value");
                decimal? Number = null;
                if (Value.ValueKind != JsonValueKind.Null)
                {
                    if (Value.ValueKind != JsonValueKind.Number || !Value.TryGetDecimal(out decimal Observation))
                        throw new JsonException("World Bank returned an invalid observation value.");
                    if ((SourceId == "3" && (Observation < 0 || Observation > 100))
                        || (Indicator == "VC.IHR.PSRC.P5" && Observation < 0))
                        throw new JsonException("World Bank returned a value outside the indicator's scale.");
                    Number = Observation;
                }
                if (!Points.TryAdd(Year, Number))
                    throw new JsonException("World Bank returned duplicate country-indicator years.");
            }
        }
        return new ReadOnlyDictionary<string, IReadOnlyList<(int Year, decimal? Value)>>(Series.ToDictionary(Item => Item.Key,
            Item => (IReadOnlyList<(int Year, decimal? Value)>)Item.Value.Where(Point => Point.Key <= DateTime.UtcNow.Year)
                .Select(Point => (Year: Point.Key, Value: Point.Value)).ToList().AsReadOnly(), StringComparer.Ordinal));
    }
    /// <summary>
    /// Loads the configured annual indicators for one source in one request, with a 30-second deadline and 30-day cache lifetime.
    /// </summary>
    /// <param name="CountryCode">The selected country's ISO alpha-3 code.</param>
    /// <param name="CancellationToken">Cancels waiting, downloading and saving.</param>
    /// <param name="SourceId">Source 2 for WDI or source 3 for WGI.</param>
    /// <returns>The original validated JSON and any cache or load warning.</returns>
    static public async Task<ApiClientResult> LoadCountryAsync(string CountryCode, CancellationToken CancellationToken = default, string SourceId = "2")
    {
        string Code = NormalizeCountry(CountryCode);
        GetSourceName(SourceId);
        string Url = "https://api.worldbank.org/v2/country/" + Code + "/indicator/"
            + string.Join(";", fIndicators.Keys.Where(Key => GetSourceId(Key) == SourceId))
            + "?source=" + SourceId + "&format=json&per_page=1000";
        ApiClientResult Result = new(Url);
        SemaphoreSlim Gate = fLoadLocks.GetOrAdd(SourceId + "-" + Code, Key => new SemaphoreSlim(1, 1));
        await Gate.WaitAsync(CancellationToken).ConfigureAwait(false);
        try
        {
            string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PInfo", "Data", "WorldBank", (SourceId == "2" ? "wdi-ten-" : "wgi-scores-") + Code + ".json");
            string Cached = null;
            try
            {
                if (File.Exists(FilePath))
                {
                    string Json = await File.ReadAllTextAsync(FilePath, CancellationToken).ConfigureAwait(false);
                    ReadSeries(Json, Code, out _, SourceId);
                    Cached = Json;
                    DateTime Saved = File.GetLastWriteTimeUtc(FilePath);
                    if (Saved <= DateTime.UtcNow && DateTime.UtcNow - Saved < TimeSpan.FromDays(30))
                    {
                        Result.ResponseJsonText = Cached;
                        Result.ResultStatus = ApiClientResultStatus.Success;
                        return Result;
                    }
                }
            }
            catch (Exception Ex) when (Ex is IOException || Ex is UnauthorizedAccessException || Ex is JsonException
                || Ex is InvalidOperationException || Ex is KeyNotFoundException || Ex is ArgumentException)
            {
                Cached = null;
            }
            try
            {
                using CancellationTokenSource Deadline = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
                Deadline.CancelAfter(TimeSpan.FromSeconds(30));
                using HttpRequestMessage Request = new(HttpMethod.Get, Url);
                Request.Headers.UserAgent.ParseAdd("PInfo/1.0 (+https://github.com/tbebekis/PInfo)");
                using HttpResponseMessage Response = await PublicApiClient.SendAsync(Request, Deadline.Token).ConfigureAwait(false);
                Result.StatusCode = Response.StatusCode;
                Response.EnsureSuccessStatusCode();
                string Json = await Response.Content.ReadAsStringAsync(Deadline.Token).ConfigureAwait(false);
                ReadSeries(Json, Code, out _, SourceId);
                CancellationToken.ThrowIfCancellationRequested();
                string TemporaryPath = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                    await File.WriteAllTextAsync(TemporaryPath, Json, CancellationToken).ConfigureAwait(false);
                    File.Move(TemporaryPath, FilePath, true);
                }
                catch (Exception Ex) when (Ex is IOException || Ex is UnauthorizedAccessException)
                {
                    Result.ReasonPhrase = "World Bank data could not be saved for offline use.";
                }
                finally
                {
                    try { File.Delete(TemporaryPath); }
                    catch (Exception Ex) when (Ex is IOException || Ex is UnauthorizedAccessException) { }
                }
                CancellationToken.ThrowIfCancellationRequested();
                Result.ResponseJsonText = Json;
                Result.ResultStatus = ApiClientResultStatus.Success;
            }
            catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception Ex) when (Ex is HttpRequestException || Ex is IOException || Ex is JsonException
                || Ex is InvalidOperationException || Ex is KeyNotFoundException || Ex is ArgumentException || Ex is OperationCanceledException)
            {
                Result.ResponseJsonText = Cached;
                Result.ResultStatus = Cached == null ? ApiClientResultStatus.Error : ApiClientResultStatus.Success;
                Result.ReasonPhrase = Cached != null ? "World Bank could not be refreshed. Previously downloaded observations are shown; check their years."
                    : Ex is JsonException ? "World Bank data is unavailable for this country, or the response is incomplete. Please try again later."
                    : "World Bank could not be reached. Check your connection and try again.";
            }
            return Result;
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>
    /// Identifies the source dataset of a configured indicator.
    /// </summary>
    /// <param name="IndicatorCode">The configured indicator code.</param>
    /// <returns>World Bank source identifier 2 or 3.</returns>
    static public string GetSourceId(string IndicatorCode)
    {
        if (!fIndicators.ContainsKey(IndicatorCode))
            throw new ArgumentException("Unknown World Bank indicator.", nameof(IndicatorCode));
        return IndicatorCode.StartsWith("GOV_WGI_", StringComparison.Ordinal) ? "3" : "2";
    }
    /// <summary>
    /// Returns the display name of a supported dataset, rejecting arbitrary source identifiers.
    /// </summary>
    /// <param name="SourceId">The API source identifier.</param>
    /// <returns>The source dataset name.</returns>
    static public string GetSourceName(string SourceId)
    {
        return SourceId switch
        {
            "2" => "World Development Indicators (WDI)", "3" => "Worldwide Governance Indicators (WGI)",
            _ => throw new ArgumentException("Unsupported World Bank source.", nameof(SourceId))
        };
    }

    // ● properties
    /// <summary>
    /// The configured development, governance and homicide indicators, their display units and their interpretation notes.
    /// </summary>
    static public IReadOnlyDictionary<string, (string Name, string Unit, string Description)> Indicators => fIndicators;
}
