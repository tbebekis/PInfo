/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo;

/// <summary>
/// Reads filtered pages of geographic places from existing country imports.
/// </summary>
static public partial class Db
{
    // ● public
    /// <summary>
    /// Reads the matching count and requested place page in one SQLite snapshot off the UI thread.
    /// Category, subcategory, and literal name search are applied before alphabetical paging.
    /// </summary>
    /// <param name="CountryCode">The country's ISO alpha-2 code.</param>
    /// <param name="Category">The selected place category.</param>
    /// <param name="Subcategory">A named category filter, including All.</param>
    /// <param name="Search">Literal text matched against primary, ASCII, and alternative names.</param>
    /// <param name="Page">The zero-based requested page.</param>
    /// <param name="PageSize">The shared page size, from 10 to 100.</param>
    /// <param name="CancellationToken">Cancels the query between database calls and rows.</param>
    /// <returns>The page, matching count, and effective page index.</returns>
    static public (List<PlaceModel> Items, int Total, int Page) GetPlaces(string CountryCode,
        PlaceCategory Category, string Subcategory, string Search, int Page, int PageSize,
        CancellationToken CancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(CountryCode);
        ArgumentNullException.ThrowIfNull(Category);
        ArgumentOutOfRangeException.ThrowIfNegative(Page);
        ArgumentOutOfRangeException.ThrowIfLessThan(PageSize, 10);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(PageSize, 100);
        if (!Category.Filters.TryGetValue(Subcategory, out ReadOnlyCollection<string> Features))
            throw new ArgumentException("Unknown place subcategory.", nameof(Subcategory));
        CancellationToken.ThrowIfCancellationRequested();
        string Pattern = "%" + (Search ?? string.Empty).Trim().Replace("!", "!!")
            .Replace("%", "!%").Replace("_", "!_") + "%";
        List<DbParameter> Parameters = new()
        {
            Parameter("@CountryCode", CountryCode.Trim().ToUpperInvariant()), Parameter("@Search", Pattern)
        };
        List<string> Groups = new();
        foreach (var Group in Features.GroupBy(Feature => Feature.Split('.')[0]))
        {
            string ClassParameter = "@Class" + Groups.Count;
            Parameters.Add(Parameter(ClassParameter, Group.Key));
            List<string> Codes = new();
            foreach (string Feature in Group)
            {
                string CodeParameter = "@Code" + Parameters.Count;
                Codes.Add(CodeParameter);
                Parameters.Add(Parameter(CodeParameter, Feature.Split('.')[1]));
            }
            Groups.Add("(FeatureClass = " + ClassParameter + " AND FeatureCode IN (" + string.Join(",", Codes) + "))");
        }
        string Filter = " FROM Place WHERE CountryCode = @CountryCode AND (" + string.Join(" OR ", Groups)
            + ") AND (Name LIKE @Search ESCAPE '!' OR AsciiName LIKE @Search ESCAPE '!'"
            + " OR AlternateNames LIKE @Search ESCAPE '!')";
        using DbConnection Connection = Connect();
        using DbTransaction Transaction = Connection.BeginTransaction();
        using DbCommand CountCommand = CreateCommand(Connection, Transaction, "SELECT COUNT(*)" + Filter, Parameters.ToArray());
        int Total = Convert.ToInt32(CountCommand.ExecuteScalar(), CultureInfo.InvariantCulture);
        CancellationToken.ThrowIfCancellationRequested();
        int CurrentPage = Math.Min(Page, Math.Max(0, (Total - 1) / PageSize));
        Parameters.Add(Parameter("@Limit", PageSize));
        Parameters.Add(Parameter("@Offset", CurrentPage * PageSize));
        using DbCommand Command = CreateCommand(Connection, Transaction,
            "SELECT GeoNameId, CountryCode, Name, AsciiName, AlternateNames, Latitude, Longitude, FeatureClass, "
            + "FeatureCode, CountryCodes, Admin1Code, Admin2Code, Admin3Code, Admin4Code, Population, Elevation, Dem, Timezone, ModificationDate"
            + Filter + " ORDER BY Name COLLATE NOCASE, GeoNameId LIMIT @Limit OFFSET @Offset;", Parameters.ToArray());
        List<PlaceModel> Items = new();
        using (DbDataReader Reader = Command.ExecuteReader())
        {
            while (Reader.Read())
            {
                CancellationToken.ThrowIfCancellationRequested();
                Items.Add(new PlaceModel
                {
                    GeoNameId = Reader.GetInt64(0), CountryCode = Reader.GetString(1), Name = Reader.GetString(2),
                    AsciiName = Reader.IsDBNull(3) ? string.Empty : Reader.GetString(3),
                    AlternateNames = Reader.IsDBNull(4) ? string.Empty : Reader.GetString(4),
                    Latitude = Reader.GetDouble(5), Longitude = Reader.GetDouble(6), FeatureClass = Reader.GetString(7),
                    FeatureCode = Reader.GetString(8), CountryCodes = Reader.IsDBNull(9) ? string.Empty : Reader.GetString(9),
                    Admin1Code = Reader.IsDBNull(10) ? string.Empty : Reader.GetString(10),
                    Admin2Code = Reader.IsDBNull(11) ? string.Empty : Reader.GetString(11),
                    Admin3Code = Reader.IsDBNull(12) ? string.Empty : Reader.GetString(12),
                    Admin4Code = Reader.IsDBNull(13) ? string.Empty : Reader.GetString(13),
                    Population = Reader.GetInt64(14), Elevation = Reader.IsDBNull(15) ? null : Reader.GetInt32(15),
                    Dem = Reader.IsDBNull(16) ? null : Reader.GetInt32(16),
                    Timezone = Reader.IsDBNull(17) ? string.Empty : Reader.GetString(17),
                    ModificationDate = DateTime.ParseExact(Reader.GetString(18), "yyyy-MM-dd", CultureInfo.InvariantCulture)
                });
            }
        }
        CancellationToken.ThrowIfCancellationRequested();
        Transaction.Commit();
        return (Items, Total, CurrentPage);
    }
}
