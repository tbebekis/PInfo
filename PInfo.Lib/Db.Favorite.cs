/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo;

/// <summary>
/// Persists favorites independently of the imported country and place datasets.
/// </summary>
static public partial class Db
{
    // ● public
    /// <summary>
    /// Reads saved destinations in one category. Call on a worker thread.
    /// </summary>
    /// <param name="Kind">The category.</param>
    /// <returns>The saved rows in display-name order.</returns>
    static public List<FavoriteModel> GetFavorites(FavoriteKind Kind)
    {
        using DbConnection Connection = Connect();
        using DbCommand Command = CreateCommand(Connection, null,
            "SELECT TargetKey, Name, Context, DataJson, AddedUtc FROM Favorite WHERE Kind = @Kind ORDER BY Name COLLATE NOCASE, Context COLLATE NOCASE, TargetKey;",
            new[] { Parameter("@Kind", (int)Kind) });
        using DbDataReader Reader = Command.ExecuteReader();
        List<FavoriteModel> Items = new();
        while (Reader.Read())
            Items.Add(new FavoriteModel
            {
                Kind = Kind, TargetKey = Reader.GetString(0), Name = Reader.GetString(1), Context = Reader.GetString(2),
                DataJson = Reader.GetString(3), AddedUtc = DateTime.Parse(Reader.GetString(4), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            });
        return Items;
    }
    /// <summary>
    /// Reads category counts, including zero counts. Call on a worker thread.
    /// </summary>
    /// <returns>The four persisted category counts.</returns>
    static public Dictionary<FavoriteKind, int> GetFavoriteCounts()
    {
        Dictionary<FavoriteKind, int> Counts = Enum.GetValues<FavoriteKind>().ToDictionary(Kind => Kind, Kind => 0);
        using DbConnection Connection = Connect();
        using DbCommand Command = CreateCommand(Connection, null, "SELECT Kind, COUNT(*) FROM Favorite GROUP BY Kind;", Array.Empty<DbParameter>());
        using DbDataReader Reader = Command.ExecuteReader();
        while (Reader.Read())
            Counts[(FavoriteKind)Reader.GetInt32(0)] = Reader.GetInt32(1);
        return Counts;
    }
    /// <summary>
    /// Reads membership using the unique category and target key. Call on a worker thread.
    /// </summary>
    /// <param name="Favorite">The destination identity.</param>
    /// <returns>True when saved.</returns>
    static public bool IsFavorite(FavoriteModel Favorite)
    {
        using DbConnection Connection = Connect();
        using DbCommand Command = CreateCommand(Connection, null,
            "SELECT COUNT(*) FROM Favorite WHERE Kind = @Kind AND TargetKey = @Key;",
            new[] { Parameter("@Kind", (int)Favorite.Kind), Parameter("@Key", Favorite.TargetKey) });
        return Convert.ToInt32(Command.ExecuteScalar(), CultureInfo.InvariantCulture) != 0;
    }
    /// <summary>
    /// Atomically adds or removes a favorite. Call on a worker thread.
    /// </summary>
    /// <param name="Favorite">The destination and offline snapshot.</param>
    /// <returns>The membership state after the transaction commits.</returns>
    static public bool ToggleFavorite(FavoriteModel Favorite)
    {
        ArgumentNullException.ThrowIfNull(Favorite);
        if (!Enum.IsDefined(Favorite.Kind) || string.IsNullOrWhiteSpace(Favorite.TargetKey)
            || string.IsNullOrWhiteSpace(Favorite.Name) || string.IsNullOrWhiteSpace(Favorite.DataJson))
            throw new ArgumentException("A valid favorite identity and snapshot are required.", nameof(Favorite));
        using DbConnection Connection = Connect();
        using DbTransaction Transaction = Connection.BeginTransaction();
        using DbCommand Delete = CreateCommand(Connection, Transaction,
            "DELETE FROM Favorite WHERE Kind = @Kind AND TargetKey = @Key;",
            new[] { Parameter("@Kind", (int)Favorite.Kind), Parameter("@Key", Favorite.TargetKey) });
        bool Saved = Delete.ExecuteNonQuery() == 0;
        if (Saved)
        {
            using DbCommand Insert = CreateCommand(Connection, Transaction,
                "INSERT INTO Favorite (Kind, TargetKey, Name, Context, DataJson, AddedUtc) VALUES (@Kind, @Key, @Name, @Context, @Data, @Added);",
                new[] { Parameter("@Kind", (int)Favorite.Kind), Parameter("@Key", Favorite.TargetKey), Parameter("@Name", Favorite.Name),
                    Parameter("@Context", Favorite.Context ?? string.Empty), Parameter("@Data", Favorite.DataJson),
                    Parameter("@Added", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)) });
            Insert.ExecuteNonQuery();
        }
        Transaction.Commit();
        return Saved;
    }
}
