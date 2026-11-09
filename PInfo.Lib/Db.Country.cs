/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo;

/// <summary>
/// Provides transactional persistence and typed reconstruction of country records.
/// </summary>
static public partial class Db
{
    // ● private
    /// <summary>
    /// Reads optional text as an empty string, matching the POCO defaults.
    /// </summary>
    static string ReadCountryText(DbDataReader Reader, string Column)
    {
        int Index = Reader.GetOrdinal(Column);
        return Reader.IsDBNull(Index) ? string.Empty : Reader.GetString(Index);
    }
    /// <summary>
    /// Reads an optional number without treating missing data as zero.
    /// </summary>
    static double? ReadCountryNumber(DbDataReader Reader, string Column)
    {
        int Index = Reader.GetOrdinal(Column);
        return Reader.IsDBNull(Index) ? null : Reader.GetDouble(Index);
    }
    /// <summary>
    /// Reads a child table in the same snapshot as its countries. SQL is supplied only by this class.
    /// </summary>
    static void ReadCountryChildren(DbConnection Connection, DbTransaction Transaction,
        Dictionary<string, CountryModel> Countries, string SqlText, Action<CountryModel, DbDataReader> ReadRow,
        CancellationToken CancellationToken)
    {
        using DbCommand Command = CreateCommand(Connection, Transaction, SqlText, null);
        using DbDataReader Reader = Command.ExecuteReader();
        while (Reader.Read())
        {
            CancellationToken.ThrowIfCancellationRequested();
            ReadRow(Countries[Reader.GetString(0)], Reader);
        }
    }
    /// <summary>
    /// Writes one string collection. Table and column names are internal constants, never user input.
    /// </summary>
    static void SaveCountryValues(DbConnection Connection, DbTransaction Transaction, string CountryCode,
        string Table, string Column, IEnumerable<string> Values, CancellationToken CancellationToken)
    {
        if (Values == null)
            return;
        foreach (string Value in Values.Distinct(StringComparer.Ordinal))
        {
            CancellationToken.ThrowIfCancellationRequested();
            if (Value == null)
                throw new ArgumentException("Country collections cannot contain null values.");
            Execute(Connection, Transaction,
                $"INSERT INTO {Table} (CountryCode, {Column}) VALUES (@CountryCode, @Value);",
                Parameter("@CountryCode", CountryCode), Parameter("@Value", Value));
        }
    }
    /// <summary>
    /// Updates a country without replacing its identity, then refreshes its dependent collections.
    /// </summary>
    static void SaveCountry(DbConnection Connection, DbTransaction Transaction, CountryModel Country,
        CancellationToken CancellationToken)
    {
        if (Country.LatLng != null && Country.LatLng.Count != 0 && Country.LatLng.Count != 2)
            throw new ArgumentException("Country coordinates must be absent or contain latitude and longitude.");
        const string SqlText = """
            INSERT INTO Country (
                Alpha3Code, Alpha2Code, Name, NumericCode, Cioc, NativeName, Capital, Region, Subregion, Demonym, Independent, Population, Area, PopulationDensity, Gini, Latitude, Longitude, Flag, FlagPngUrl, FlagSvgUrl, GoogleMapsUrl, OpenStreetMapsUrl)
            VALUES (
                @Alpha3Code, @Alpha2Code, @Name, @NumericCode, @Cioc, @NativeName, @Capital, @Region, @Subregion, @Demonym, @Independent, @Population, @Area, @PopulationDensity, @Gini, @Latitude, @Longitude, @Flag, @FlagPngUrl, @FlagSvgUrl, @GoogleMapsUrl, @OpenStreetMapsUrl)
            ON CONFLICT (Alpha3Code) DO UPDATE SET
                Alpha2Code = excluded.Alpha2Code,
                Name = excluded.Name,
                NumericCode = excluded.NumericCode,
                Cioc = excluded.Cioc,
                NativeName = excluded.NativeName,
                Capital = excluded.Capital,
                Region = excluded.Region,
                Subregion = excluded.Subregion,
                Demonym = excluded.Demonym,
                Independent = excluded.Independent,
                Population = excluded.Population,
                Area = excluded.Area,
                PopulationDensity = excluded.PopulationDensity,
                Gini = excluded.Gini,
                Latitude = excluded.Latitude,
                Longitude = excluded.Longitude,
                Flag = excluded.Flag,
                FlagPngUrl = excluded.FlagPngUrl,
                FlagSvgUrl = excluded.FlagSvgUrl,
                GoogleMapsUrl = excluded.GoogleMapsUrl,
                OpenStreetMapsUrl = excluded.OpenStreetMapsUrl;
            """;
        Execute(Connection, Transaction, SqlText,
            Parameter("@Alpha3Code", Country.Alpha3Code),
            Parameter("@Alpha2Code", Country.Alpha2Code),
            Parameter("@Name", Country.Name),
            Parameter("@NumericCode", Country.NumericCode),
            Parameter("@Cioc", Country.Cioc),
            Parameter("@NativeName", Country.NativeName),
            Parameter("@Capital", Country.Capital),
            Parameter("@Region", Country.Region),
            Parameter("@Subregion", Country.Subregion),
            Parameter("@Demonym", Country.Demonym),
            Parameter("@Independent", Country.Independent ? 1 : 0),
            Parameter("@Population", Country.Population),
            Parameter("@Area", Country.Area),
            Parameter("@PopulationDensity", Country.PopulationDensity),
            Parameter("@Gini", Country.Gini),
            Parameter("@Latitude", Country.LatLng != null && Country.LatLng.Count == 2 ? Country.LatLng[0] : null),
            Parameter("@Longitude", Country.LatLng != null && Country.LatLng.Count == 2 ? Country.LatLng[1] : null),
            Parameter("@Flag", Country.Flag),
            Parameter("@FlagPngUrl", Country.Flags?.Png),
            Parameter("@FlagSvgUrl", Country.Flags?.Svg),
            Parameter("@GoogleMapsUrl", Country.Maps?.GoogleMaps),
            Parameter("@OpenStreetMapsUrl", Country.Maps?.OpenStreetMaps));

        // Replacing child rows does not delete the Country row or future Place/Favorite references.
        string[] ChildTables =
        {
            "CountryTranslation", "CountryCurrency", "CountryLanguage", "CountryBorder", "CountryTimezone",
            "CountryDomain", "CountryCallingCode", "CountryAlternativeName", "CountryRegionalBloc"
        };
        foreach (string Table in ChildTables)
        {
            CancellationToken.ThrowIfCancellationRequested();
            Execute(Connection, Transaction, $"DELETE FROM {Table} WHERE CountryCode = @Code;",
                Parameter("@Code", Country.Alpha3Code));
        }
        SaveCountryValues(Connection, Transaction, Country.Alpha3Code, "CountryBorder", "NeighborAlpha3Code", Country.Borders, CancellationToken);
        SaveCountryValues(Connection, Transaction, Country.Alpha3Code, "CountryTimezone", "Timezone", Country.Timezones, CancellationToken);
        SaveCountryValues(Connection, Transaction, Country.Alpha3Code, "CountryDomain", "Domain", Country.TopLevelDomain, CancellationToken);
        SaveCountryValues(Connection, Transaction, Country.Alpha3Code, "CountryCallingCode", "CallingCode", Country.CallingCodes, CancellationToken);
        SaveCountryValues(Connection, Transaction, Country.Alpha3Code, "CountryAlternativeName", "Name", Country.AltSpellings, CancellationToken);

        if (Country.Translations != null)
        {
            foreach (KeyValuePair<string, string> Item in Country.Translations)
            {
                CancellationToken.ThrowIfCancellationRequested();
                Execute(Connection, Transaction,
                    "INSERT INTO CountryTranslation (CountryCode, LanguageCode, Name) VALUES (@Code, @Language, @Name);",
                    Parameter("@Code", Country.Alpha3Code), Parameter("@Language", Item.Key), Parameter("@Name", Item.Value));
            }
        }
        if (Country.Currencies != null)
        {
            foreach (CurrencyDetail Item in Country.Currencies)
            {
                CancellationToken.ThrowIfCancellationRequested();
                ArgumentNullException.ThrowIfNull(Item);
                Execute(Connection, Transaction,
                    "INSERT INTO CountryCurrency (CountryCode, Code, Name, Symbol) VALUES (@CountryCode, @Code, @Name, @Symbol);",
                    Parameter("@CountryCode", Country.Alpha3Code), Parameter("@Code", Item.Code), Parameter("@Name", Item.Name), Parameter("@Symbol", Item.Symbol));
            }
        }
        if (Country.Languages != null)
        {
            foreach (LanguageInfo Item in Country.Languages)
            {
                CancellationToken.ThrowIfCancellationRequested();
                ArgumentNullException.ThrowIfNull(Item);
                Execute(Connection, Transaction,
                    "INSERT INTO CountryLanguage (CountryCode, Iso639_2, Iso639_1, Name, NativeName) VALUES (@Code, @Iso639_2, @Iso639_1, @Name, @NativeName);",
                    Parameter("@Code", Country.Alpha3Code), Parameter("@Iso639_2", Item.Iso639_2), Parameter("@Iso639_1", Item.Iso639_1), Parameter("@Name", Item.Name), Parameter("@NativeName", Item.NativeName));
            }
        }
        if (Country.RegionalBlocs != null)
        {
            foreach (RegionalBlocInfo Item in Country.RegionalBlocs)
            {
                CancellationToken.ThrowIfCancellationRequested();
                ArgumentNullException.ThrowIfNull(Item);
                Execute(Connection, Transaction,
                    "INSERT INTO CountryRegionalBloc (CountryCode, Acronym, Name) VALUES (@Code, @Acronym, @Name);",
                    Parameter("@Code", Country.Alpha3Code), Parameter("@Acronym", Item.Acronym), Parameter("@Name", Item.Name));
                if (Item.OtherNames != null)
                {
                    foreach (string Value in Item.OtherNames.Distinct(StringComparer.Ordinal))
                    {
                        CancellationToken.ThrowIfCancellationRequested();
                        Execute(Connection, Transaction,
                            "INSERT INTO CountryRegionalBlocAlternativeName (CountryCode, Acronym, Name) VALUES (@Code, @Acronym, @Value);",
                            Parameter("@Code", Country.Alpha3Code), Parameter("@Acronym", Item.Acronym), Parameter("@Value", Value));
                    }
                }
                if (Item.OtherAcronyms != null)
                {
                    foreach (string Value in Item.OtherAcronyms.Distinct(StringComparer.Ordinal))
                    {
                        CancellationToken.ThrowIfCancellationRequested();
                        Execute(Connection, Transaction,
                            "INSERT INTO CountryRegionalBlocAlternativeAcronym (CountryCode, Acronym, AlternativeAcronym) VALUES (@Code, @Acronym, @Value);",
                            Parameter("@Code", Country.Alpha3Code), Parameter("@Acronym", Item.Acronym), Parameter("@Value", Value));
                    }
                }
            }
        }
    }

    // ● public
    /// <summary>
    /// Reads all countries and collections from one consistent snapshot. Call off the UI thread.
    /// </summary>
    /// <param name="UpdatedUtc">The successful import time, or DateTime.MinValue if unavailable.</param>
    /// <param name="CancellationToken">Cancels reading between rows and queries.</param>
    /// <returns>Country POCOs sorted by name, or an empty list before the first import.</returns>
    static public List<CountryModel> GetCountries(out DateTime UpdatedUtc, CancellationToken CancellationToken = default)
    {
        CancellationToken.ThrowIfCancellationRequested();
        UpdatedUtc = DateTime.MinValue;
        using DbConnection Connection = Connect();
        using DbTransaction Transaction = Connection.BeginTransaction();
        Dictionary<string, CountryModel> Countries = new(StringComparer.OrdinalIgnoreCase);
        using (DbCommand Command = CreateCommand(Connection, Transaction, "SELECT * FROM Country;", null))
        using (DbDataReader Reader = Command.ExecuteReader())
        {
            while (Reader.Read())
            {
                CancellationToken.ThrowIfCancellationRequested();
                CountryModel Country = new()
                {
                    Alpha3Code = ReadCountryText(Reader, "Alpha3Code"),
                    Alpha2Code = ReadCountryText(Reader, "Alpha2Code"),
                    Name = ReadCountryText(Reader, "Name"),
                    NumericCode = ReadCountryText(Reader, "NumericCode"),
                    Cioc = ReadCountryText(Reader, "Cioc"),
                    NativeName = ReadCountryText(Reader, "NativeName"),
                    Capital = ReadCountryText(Reader, "Capital"),
                    Region = ReadCountryText(Reader, "Region"),
                    Subregion = ReadCountryText(Reader, "Subregion"),
                    Demonym = ReadCountryText(Reader, "Demonym"),
                    Independent = Reader.GetInt64(Reader.GetOrdinal("Independent")) != 0,
                    Population = Reader.GetInt64(Reader.GetOrdinal("Population")),
                    Area = ReadCountryNumber(Reader, "Area"),
                    PopulationDensity = ReadCountryNumber(Reader, "PopulationDensity"),
                    Gini = ReadCountryNumber(Reader, "Gini"),
                    Flag = ReadCountryText(Reader, "Flag"),
                    Flags = new FlagInfo { Png = ReadCountryText(Reader, "FlagPngUrl"), Svg = ReadCountryText(Reader, "FlagSvgUrl") },
                    Maps = new MapInfo { GoogleMaps = ReadCountryText(Reader, "GoogleMapsUrl"), OpenStreetMaps = ReadCountryText(Reader, "OpenStreetMapsUrl") }
                };
                double? Latitude = ReadCountryNumber(Reader, "Latitude");
                double? Longitude = ReadCountryNumber(Reader, "Longitude");
                if (Latitude.HasValue && Longitude.HasValue)
                {
                    Country.LatLng.Add(Latitude.Value);
                    Country.LatLng.Add(Longitude.Value);
                }
                Countries.Add(Country.Alpha3Code, Country);
            }
        }
        ReadCountryChildren(Connection, Transaction, Countries,
            "SELECT CountryCode, NeighborAlpha3Code FROM CountryBorder ORDER BY CountryCode, NeighborAlpha3Code;",
            (Country, Reader) => Country.Borders.Add(Reader.GetString(1)), CancellationToken);
        ReadCountryChildren(Connection, Transaction, Countries,
            "SELECT CountryCode, Timezone FROM CountryTimezone ORDER BY CountryCode, Timezone;",
            (Country, Reader) => Country.Timezones.Add(Reader.GetString(1)), CancellationToken);
        ReadCountryChildren(Connection, Transaction, Countries,
            "SELECT CountryCode, Domain FROM CountryDomain ORDER BY CountryCode, Domain;",
            (Country, Reader) => Country.TopLevelDomain.Add(Reader.GetString(1)), CancellationToken);
        ReadCountryChildren(Connection, Transaction, Countries,
            "SELECT CountryCode, CallingCode FROM CountryCallingCode ORDER BY CountryCode, CallingCode;",
            (Country, Reader) => Country.CallingCodes.Add(Reader.GetString(1)), CancellationToken);
        ReadCountryChildren(Connection, Transaction, Countries,
            "SELECT CountryCode, Name FROM CountryAlternativeName ORDER BY CountryCode, Name;",
            (Country, Reader) => Country.AltSpellings.Add(Reader.GetString(1)), CancellationToken);
        ReadCountryChildren(Connection, Transaction, Countries,
            "SELECT CountryCode, LanguageCode, Name FROM CountryTranslation ORDER BY CountryCode, LanguageCode;",
            (Country, Reader) => Country.Translations.Add(Reader.GetString(1), Reader.GetString(2)), CancellationToken);
        ReadCountryChildren(Connection, Transaction, Countries,
            "SELECT CountryCode, Code, Name, Symbol FROM CountryCurrency ORDER BY CountryCode, Code;",
            (Country, Reader) => Country.Currencies.Add(new CurrencyDetail
            {
                Code = ReadCountryText(Reader, "Code"),
                Name = ReadCountryText(Reader, "Name"),
                Symbol = ReadCountryText(Reader, "Symbol")
            }), CancellationToken);
        ReadCountryChildren(Connection, Transaction, Countries,
            "SELECT CountryCode, Iso639_2, Iso639_1, Name, NativeName FROM CountryLanguage ORDER BY CountryCode, Iso639_2;",
            (Country, Reader) => Country.Languages.Add(new LanguageInfo
            {
                Iso639_2 = ReadCountryText(Reader, "Iso639_2"),
                Iso639_1 = ReadCountryText(Reader, "Iso639_1"),
                Name = ReadCountryText(Reader, "Name"),
                NativeName = ReadCountryText(Reader, "NativeName")
            }), CancellationToken);
        ReadCountryChildren(Connection, Transaction, Countries,
            "SELECT CountryCode, Acronym, Name FROM CountryRegionalBloc ORDER BY CountryCode, Acronym;",
            (Country, Reader) => Country.RegionalBlocs.Add(new RegionalBlocInfo
            {
                Acronym = ReadCountryText(Reader, "Acronym"),
                Name = ReadCountryText(Reader, "Name")
            }), CancellationToken);
        ReadCountryChildren(Connection, Transaction, Countries,
            "SELECT CountryCode, Acronym, Name FROM CountryRegionalBlocAlternativeName ORDER BY CountryCode, Acronym, Name;",
            (Country, Reader) => Country.RegionalBlocs.Single(Item => Item.Acronym == Reader.GetString(1))
                .OtherNames.Add(Reader.GetString(2)), CancellationToken);
        ReadCountryChildren(Connection, Transaction, Countries,
            "SELECT CountryCode, Acronym, AlternativeAcronym FROM CountryRegionalBlocAlternativeAcronym ORDER BY CountryCode, Acronym, AlternativeAcronym;",
            (Country, Reader) => Country.RegionalBlocs.Single(Item => Item.Acronym == Reader.GetString(1))
                .OtherAcronyms.Add(Reader.GetString(2)), CancellationToken);
        using (DbCommand Command = CreateCommand(Connection, Transaction,
            "SELECT UpdatedUtc FROM DataRefresh WHERE Dataset = @Dataset AND Scope = '';",
            new[] { Parameter("@Dataset", "Countries") }))
        {
            object Value = Command.ExecuteScalar();
            if (Value is string Text)
            {
                if (!DateTime.TryParseExact(Text, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime Parsed)
                    || Parsed.Kind != DateTimeKind.Utc)
                    throw new InvalidDataException("The country refresh timestamp is invalid.");
                UpdatedUtc = Parsed;
            }
        }
        CancellationToken.ThrowIfCancellationRequested();
        Transaction.Commit();
        return Countries.Values.OrderBy(Item => Item.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }
    /// <summary>
    /// Upserts countries and replaces their collections atomically with the dataset refresh timestamp.
    /// Countries absent from the supplied list are retained to protect future related data.
    /// Call off the UI thread; errors and cancellation roll back the entire import.
    /// </summary>
    /// <param name="Countries">The complete downloaded country collection.</param>
    /// <param name="UpdatedUtc">The successful download time in UTC, or the original JSON cache timestamp.</param>
    /// <param name="CancellationToken">Cancels the import before commit.</param>
    static public void SaveCountries(IReadOnlyList<CountryModel> Countries, DateTime UpdatedUtc,
        CancellationToken CancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(Countries);
        if (Countries.Count == 0)
            throw new ArgumentException("The country collection cannot be empty.", nameof(Countries));
        if (UpdatedUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("The refresh timestamp must be UTC.", nameof(UpdatedUtc));
        CancellationToken.ThrowIfCancellationRequested();
        using DbConnection Connection = Connect();
        using DbTransaction Transaction = Connection.BeginTransaction();
        HashSet<string> Codes = new(StringComparer.OrdinalIgnoreCase);
        foreach (CountryModel Country in Countries)
        {
            CancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(Country);
            if (string.IsNullOrWhiteSpace(Country.Alpha3Code) || !Codes.Add(Country.Alpha3Code))
                throw new ArgumentException("Country identifiers must be present and unique.", nameof(Countries));
            SaveCountry(Connection, Transaction, Country, CancellationToken);
        }
        Execute(Connection, Transaction,
            "INSERT INTO DataRefresh (Dataset, Scope, UpdatedUtc) VALUES (@Dataset, '', @UpdatedUtc) "
            + "ON CONFLICT (Dataset, Scope) DO UPDATE SET UpdatedUtc = excluded.UpdatedUtc;",
            Parameter("@Dataset", "Countries"), Parameter("@UpdatedUtc", UpdatedUtc.ToString("O", CultureInfo.InvariantCulture)));
        CancellationToken.ThrowIfCancellationRequested();
        Transaction.Commit();
    }
}
