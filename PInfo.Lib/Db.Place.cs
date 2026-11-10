/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo;

/// <summary>
/// Stores GeoNames country dumps and their refresh timestamps atomically.
/// </summary>
static public partial class Db
{
    // ● public
    /// <summary>
    /// Reads the last successful GeoNames import time for a country. Call off the UI thread.
    /// </summary>
    /// <param name="CountryCode">The ISO alpha-2 country code.</param>
    /// <param name="CancellationToken">Cancels the operation between database calls.</param>
    /// <returns>The UTC import time, or DateTime.MinValue when no import exists.</returns>
    static public DateTime GetPlacesUpdatedUtc(string CountryCode, CancellationToken CancellationToken = default)
    {
        CancellationToken.ThrowIfCancellationRequested();
        using DbConnection Connection = Connect();
        using DbCommand Command = CreateCommand(Connection, null,
            "SELECT UpdatedUtc FROM DataRefresh WHERE Dataset = 'Places' AND Scope = @CountryCode "
            + "AND EXISTS (SELECT 1 FROM Place WHERE CountryCode = @CountryCode);",
            new[] { Parameter("@CountryCode", CountryCode) });
        object Value = Command.ExecuteScalar();
        CancellationToken.ThrowIfCancellationRequested();
        if (Value == null || Value == DBNull.Value)
            return DateTime.MinValue;
        if (!DateTime.TryParseExact(Convert.ToString(Value, CultureInfo.InvariantCulture), "O",
            CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime UpdatedUtc)
            || UpdatedUtc.Kind != DateTimeKind.Utc)
            throw new InvalidDataException("The place refresh timestamp is invalid.");
        return UpdatedUtc;
    }
    /// <summary>
    /// Reads a bounded city page and its matching count from one SQLite snapshot.
    /// Big cities are selected before searching: at most 100 places with population at least 15,000.
    /// Call off the UI thread. Search matches literal text in primary, ASCII, or alternative names.
    /// </summary>
    /// <param name="CountryCode">The country's ISO alpha-2 code.</param>
    /// <param name="BigCities">Selects the country's largest cities instead of all populated places.</param>
    /// <param name="Search">The name fragment to find.</param>
    /// <param name="Page">The zero-based page index.</param>
    /// <param name="CancellationToken">Cancels reading between rows.</param>
    /// <param name="PageSize">The page size for All Cities, from 10 to 100. Big Cities always uses 100.</param>
    /// <returns>The page, total matching count, and effective page index.</returns>
    static public (List<CityModel> Items, int Total, int Page) GetPlaceCities(string CountryCode,
        bool BigCities, string Search, int Page, CancellationToken CancellationToken = default, int PageSize = 100)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(CountryCode);
        ArgumentOutOfRangeException.ThrowIfNegative(Page);
        CancellationToken.ThrowIfCancellationRequested();
        ArgumentOutOfRangeException.ThrowIfLessThan(PageSize, 10);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(PageSize, 100);
        PageSize = BigCities ? 100 : PageSize;
        string Source = "WITH Cities AS (SELECT * FROM Place WHERE CountryCode = @CountryCode AND FeatureClass = 'P'"
            + (BigCities ? " AND Population >= 15000 ORDER BY Population DESC, Name COLLATE NOCASE, GeoNameId LIMIT 100" : string.Empty)
            + ") ";
        string Filter = " WHERE Name LIKE @Search ESCAPE '!' OR AsciiName LIKE @Search ESCAPE '!'"
            + " OR AlternateNames LIKE @Search ESCAPE '!'";
        string Pattern = "%" + (Search ?? string.Empty).Trim().Replace("!", "!!")
            .Replace("%", "!%").Replace("_", "!_") + "%";
        using DbConnection Connection = Connect();
        using DbTransaction Transaction = Connection.BeginTransaction();
        DbParameter[] Parameters = { Parameter("@CountryCode", CountryCode.Trim().ToUpperInvariant()), Parameter("@Search", Pattern) };
        using DbCommand CountCommand = CreateCommand(Connection, Transaction,
            Source + "SELECT COUNT(*) FROM Cities" + Filter, Parameters);
        int Total = Convert.ToInt32(CountCommand.ExecuteScalar(), CultureInfo.InvariantCulture);
        int CurrentPage = Math.Min(Page, Math.Max(0, (Total - 1) / PageSize));
        using DbCommand Command = CreateCommand(Connection, Transaction,
            Source + "SELECT GeoNameId, Name, AsciiName, CountryCode, Admin1Code, Latitude, Longitude, Population, Timezone, FeatureCode FROM Cities"
            + Filter + " ORDER BY Name COLLATE NOCASE, GeoNameId"
            + " LIMIT @Limit OFFSET @Offset;",
            new[] { Parameter("@CountryCode", CountryCode.Trim().ToUpperInvariant()), Parameter("@Search", Pattern),
                Parameter("@Limit", PageSize), Parameter("@Offset", CurrentPage * PageSize) });
        List<CityModel> Items = new();
        using (DbDataReader Reader = Command.ExecuteReader())
        {
            while (Reader.Read())
            {
                CancellationToken.ThrowIfCancellationRequested();
                Items.Add(new CityModel
                {
                    GeonameId = Reader.GetInt64(0), Name = Reader.GetString(1),
                    AsciiName = Reader.IsDBNull(2) ? string.Empty : Reader.GetString(2),
                    CountryCode = Reader.GetString(3), Admin1Code = Reader.IsDBNull(4) ? string.Empty : Reader.GetString(4),
                    Latitude = Reader.GetDouble(5), Longitude = Reader.GetDouble(6), Population = Reader.GetInt64(7),
                    Timezone = Reader.IsDBNull(8) ? string.Empty : Reader.GetString(8), FeatureCode = Reader.GetString(9)
                });
            }
        }
        CancellationToken.ThrowIfCancellationRequested();
        Transaction.Commit();
        return (Items, Total, CurrentPage);
    }
    /// <summary>
    /// Replaces one country's places using a reusable insert command and one transaction.
    /// Enumeration, validation, or cancellation failures preserve the previous import.
    /// Call off the UI thread. The country must already exist in the database.
    /// </summary>
    /// <param name="CountryCode">The ISO alpha-2 country code.</param>
    /// <param name="Places">The streaming collection of parsed GeoNames records.</param>
    /// <param name="UpdatedUtc">The successful download time in UTC.</param>
    /// <param name="CancellationToken">Cancels the import before commit.</param>
    /// <param name="Progress">Receives the final save status and imported record count.</param>
    /// <returns>The number of imported records.</returns>
    static public int SavePlaces(string CountryCode, IEnumerable<PlaceModel> Places, DateTime UpdatedUtc,
        CancellationToken CancellationToken = default, IProgress<string> Progress = null)
    {
        ArgumentNullException.ThrowIfNull(Places);
        ArgumentException.ThrowIfNullOrWhiteSpace(CountryCode);
        if (UpdatedUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("The refresh timestamp must be UTC.", nameof(UpdatedUtc));
        CancellationToken.ThrowIfCancellationRequested();
        using DbConnection Connection = Connect();
        using DbTransaction Transaction = Connection.BeginTransaction();
        Execute(Connection, Transaction, "DELETE FROM Place WHERE CountryCode = @CountryCode;",
            Parameter("@CountryCode", CountryCode));
        using DbCommand Command = CreateCommand(Connection, Transaction,
            "INSERT INTO Place (GeoNameId, CountryCode, Name, AsciiName, AlternateNames, Latitude, Longitude, "
            + "FeatureClass, FeatureCode, CountryCodes, Admin1Code, Admin2Code, Admin3Code, Admin4Code, "
            + "Population, Elevation, Dem, Timezone, ModificationDate) "
            + "VALUES (@GeoNameId, @CountryCode, @Name, @AsciiName, @AlternateNames, @Latitude, @Longitude, "
            + "@FeatureClass, @FeatureCode, @CountryCodes, @Admin1Code, @Admin2Code, @Admin3Code, @Admin4Code, "
            + "@Population, @Elevation, @Dem, @Timezone, @ModificationDate);", null);
        string[] Names = { "GeoNameId", "CountryCode", "Name", "AsciiName", "AlternateNames", "Latitude", "Longitude",
            "FeatureClass", "FeatureCode", "CountryCodes", "Admin1Code", "Admin2Code", "Admin3Code", "Admin4Code",
            "Population", "Elevation", "Dem", "Timezone", "ModificationDate" };
        foreach (string Name in Names)
        {
            DbParameter ValueParameter = Command.CreateParameter();
            ValueParameter.ParameterName = "@" + Name;
            ValueParameter.Value = DBNull.Value;
            Command.Parameters.Add(ValueParameter);
        }
        // Parameters are rebound for each row; preparing avoids recompiling the insert for every place.
        Command.Prepare();
        int Count = 0;
        foreach (PlaceModel Place in Places)
        {
            CancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(Place);
            if (Place.CountryCode != CountryCode)
                throw new InvalidDataException("The place belongs to a different country.");
            object[] Values = { Place.GeoNameId, Place.CountryCode, Place.Name, Place.AsciiName, Place.AlternateNames,
                Place.Latitude, Place.Longitude, Place.FeatureClass, Place.FeatureCode, Place.CountryCodes,
                Place.Admin1Code, Place.Admin2Code, Place.Admin3Code, Place.Admin4Code, Place.Population,
                (object)Place.Elevation ?? DBNull.Value, (object)Place.Dem ?? DBNull.Value, Place.Timezone,
                Place.ModificationDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) };
            for (int Index = 0; Index < Values.Length; Index++)
                Command.Parameters[Index].Value = Values[Index] ?? DBNull.Value;
            Command.ExecuteNonQuery();
            Count++;
        }
        if (Count == 0)
            throw new InvalidDataException("The GeoNames dump contains no places; the previous import was retained.");
        Progress?.Report($"Saving changes... {Count:N0} records");
        Execute(Connection, Transaction,
            "INSERT INTO DataRefresh (Dataset, Scope, UpdatedUtc) VALUES ('Places', @CountryCode, @UpdatedUtc) "
            + "ON CONFLICT (Dataset, Scope) DO UPDATE SET UpdatedUtc = excluded.UpdatedUtc;",
            Parameter("@CountryCode", CountryCode), Parameter("@UpdatedUtc", UpdatedUtc.ToString("O", CultureInfo.InvariantCulture)));
        CancellationToken.ThrowIfCancellationRequested();
        Transaction.Commit();
        Progress?.Report($"Done. {Count:N0} records imported.");
        return Count;
    }
}
