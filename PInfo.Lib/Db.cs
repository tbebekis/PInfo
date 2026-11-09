/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo;

/// <summary>
/// Creates and versions the private SQLite database without blocking the caller's UI thread.
/// Connections are opened per operation and are never shared between threads.
/// </summary>
static public partial class Db
{
    // ● private
    const int SchemaVersion = 1;
    static readonly SemaphoreSlim fInitializeLock = new(1, 1);
    static bool fInitialized;

    /// <summary>
    /// Opens a connection with foreign keys enabled. Call only on a background worker.
    /// The caller owns the connection and must dispose it.
    /// </summary>
    static SqliteConnection OpenConnection()
    {
        SqliteConnectionStringBuilder Builder = new()
        {
            DataSource = FilePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true,
            DefaultTimeout = 15
        };
        SqliteConnection Connection = new(Builder.ToString());
        try
        {
            Connection.Open();
            return Connection;
        }
        catch
        {
            Connection.Dispose();
            throw;
        }
    }
    /// <summary>
    /// Reads the schema version within the supplied transaction, when present.
    /// </summary>
    static int ReadVersion(SqliteConnection Connection, SqliteTransaction Transaction = null)
    {
        using SqliteCommand Command = Connection.CreateCommand();
        Command.Transaction = Transaction;
        Command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(Command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }
    /// <summary>
    /// Refuses to modify a database created by a newer application version.
    /// </summary>
    static void CheckVersion(int Version)
    {
        if (Version < 0)
            throw new InvalidOperationException("The database has an invalid schema version.");
        if (Version > SchemaVersion)
            throw new InvalidOperationException("The database was created by a newer version of PInfo.");
    }
    /// <summary>
    /// Applies the initial schema and version atomically. Runs entirely on a worker thread.
    /// </summary>
    static void InitializeCore(CancellationToken CancellationToken)
    {
        CancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
        using SqliteConnection Connection = OpenConnection();
        CheckVersion(ReadVersion(Connection));

        using (SqliteCommand JournalCommand = Connection.CreateCommand())
        {
            JournalCommand.CommandText = "PRAGMA journal_mode = WAL;";
            string Mode = Convert.ToString(JournalCommand.ExecuteScalar(), CultureInfo.InvariantCulture);
            if (!string.Equals(Mode, "wal", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("SQLite could not enable write-ahead logging.");
        }

        CancellationToken.ThrowIfCancellationRequested();
        // A write transaction also prevents another process from applying the initial schema concurrently.
        using SqliteTransaction Transaction = Connection.BeginTransaction();
        int Version = ReadVersion(Connection, Transaction);
        CheckVersion(Version);
        if (Version == 0)
        {
            using (SqliteCommand ExistingTables = Connection.CreateCommand())
            {
                ExistingTables.Transaction = Transaction;
                ExistingTables.CommandText = "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%';";
                if (Convert.ToInt64(ExistingTables.ExecuteScalar(), CultureInfo.InvariantCulture) != 0)
                    throw new InvalidOperationException("The database contains tables but has no recognized schema version.");
            }

            using Stream SchemaStream = typeof(Db).Assembly.GetManifestResourceStream("PInfo.Schema.sql");
            if (SchemaStream == null)
                throw new InvalidOperationException("The embedded database schema could not be found.");
            using StreamReader Reader = new(SchemaStream, Encoding.UTF8);
            string Schema = Reader.ReadToEnd();
            CancellationToken.ThrowIfCancellationRequested();
            using SqliteCommand SchemaCommand = Connection.CreateCommand();
            SchemaCommand.Transaction = Transaction;
            SchemaCommand.CommandText = Schema;
            SchemaCommand.ExecuteNonQuery();

            CancellationToken.ThrowIfCancellationRequested();
            using SqliteCommand VersionCommand = Connection.CreateCommand();
            VersionCommand.Transaction = Transaction;
            VersionCommand.CommandText = "PRAGMA user_version = 1;";
            VersionCommand.ExecuteNonQuery();
        }
        // Future schema upgrades belong here as explicit, ordered migration steps.
        CancellationToken.ThrowIfCancellationRequested();
        Transaction.Commit();
    }

    /// <summary>
    /// Creates a command and copies parameter definitions so input parameters can be reused safely.
    /// </summary>
    static DbCommand CreateCommand(DbConnection Connection, DbTransaction Transaction, string SqlText,
        DbParameter[] Parameters)
    {
        ArgumentNullException.ThrowIfNull(Connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(SqlText);
        if (Connection.State != ConnectionState.Open)
            throw new InvalidOperationException("The database connection must be open.");
        if (Transaction != null && Transaction.Connection != Connection)
            throw new ArgumentException("The transaction must belong to the supplied connection.", nameof(Transaction));

        DbCommand Command = Connection.CreateCommand();
        try
        {
            Command.Transaction = Transaction;
            Command.CommandText = SqlText;
            if (Parameters != null)
            {
                foreach (DbParameter Parameter in Parameters)
                {
                    ArgumentNullException.ThrowIfNull(Parameter);
                    DbParameter Copy = Command.CreateParameter();
                    Copy.ParameterName = Parameter.ParameterName;
                    Copy.DbType = Parameter.DbType;
                    Copy.Direction = Parameter.Direction;
                    Copy.Size = Parameter.Size;
                    Copy.Precision = Parameter.Precision;
                    Copy.Scale = Parameter.Scale;
                    Copy.Value = Parameter.Value ?? DBNull.Value;
                    Command.Parameters.Add(Copy);
                }
            }
            return Command;
        }
        catch
        {
            Command.Dispose();
            throw;
        }
    }

    // ● public
    /// <summary>
    /// Initializes the schema once on demand. Failures and cancellations leave initialization retryable.
    /// SQLite work runs on a worker because the provider's async database methods execute synchronously.
    /// </summary>
    /// <param name="CancellationToken">Cancels waiting and aborts initialization before commit when requested.</param>
    /// <returns>A task completed after the database is ready.</returns>
    static public async Task InitializeAsync(CancellationToken CancellationToken = default)
    {
        await fInitializeLock.WaitAsync(CancellationToken).ConfigureAwait(false);
        try
        {
            CancellationToken.ThrowIfCancellationRequested();
            if (fInitialized)
                return;
            await Task.Run(() => InitializeCore(CancellationToken), CancellationToken).ConfigureAwait(false);
            fInitialized = true;
        }
        finally
        {
            fInitializeLock.Release();
        }
    }

    /// <summary>
    /// Initializes the schema synchronously. Call on a background worker, not the UI thread.
    /// </summary>
    static public void Initialize()
    {
        fInitializeLock.Wait();
        try
        {
            if (fInitialized)
                return;
            InitializeCore(CancellationToken.None);
            fInitialized = true;
        }
        finally
        {
            fInitializeLock.Release();
        }
    }
    /// <summary>
    /// Returns a new open connection to the initialized database. The caller must dispose it.
    /// This synchronous operation must not be called on the UI thread.
    /// </summary>
    /// <returns>An open connection with foreign keys enabled.</returns>
    static public DbConnection Connect()
    {
        Initialize();
        return OpenConnection();
    }
    /// <summary>
    /// Creates a named parameter, representing a null value as SQL NULL.
    /// </summary>
    /// <param name="Name">The SQL parameter name, including its prefix.</param>
    /// <param name="Value">The value to bind.</param>
    /// <returns>A parameter usable by Select or Execute.</returns>
    static public DbParameter Parameter(string Name, object Value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        return new SqliteParameter(Name, Value ?? DBNull.Value);
    }
    /// <summary>
    /// Selects a result table using an internally owned connection. Runs synchronously.
    /// </summary>
    /// <param name="SqlText">The SQL query.</param>
    /// <param name="Parameters">The parameter values.</param>
    /// <returns>A disconnected table containing the first result set.</returns>
    static public DataTable Select(string SqlText, params DbParameter[] Parameters)
    {
        using DbConnection Connection = Connect();
        return Select(Connection, null, SqlText, Parameters);
    }
    /// <summary>
    /// Selects a result table without disposing or committing the caller's connection or transaction.
    /// </summary>
    /// <param name="Connection">The caller's open connection.</param>
    /// <param name="Transaction">The active transaction, or null when none is active.</param>
    /// <param name="SqlText">The SQL query.</param>
    /// <param name="Parameters">The parameter values.</param>
    /// <returns>A disconnected table containing the first result set.</returns>
    static public DataTable Select(DbConnection Connection, DbTransaction Transaction, string SqlText,
        params DbParameter[] Parameters)
    {
        using DbCommand Command = CreateCommand(Connection, Transaction, SqlText, Parameters);
        using DbDataReader Reader = Command.ExecuteReader();
        DataTable Table = new();
        try
        {
            Table.Load(Reader);
            return Table;
        }
        catch
        {
            Table.Dispose();
            throw;
        }
    }
    /// <summary>
    /// Executes SQL using an internally owned connection. Runs synchronously.
    /// Use a caller-owned transaction for multiple statements that must succeed together.
    /// </summary>
    /// <param name="SqlText">The SQL statement.</param>
    /// <param name="Parameters">The parameter values.</param>
    /// <returns>The provider's affected-row count.</returns>
    static public int Execute(string SqlText, params DbParameter[] Parameters)
    {
        using DbConnection Connection = Connect();
        return Execute(Connection, null, SqlText, Parameters);
    }
    /// <summary>
    /// Executes SQL without disposing or committing the caller's connection or transaction.
    /// </summary>
    /// <param name="Connection">The caller's open connection.</param>
    /// <param name="Transaction">The active transaction, or null when none is active.</param>
    /// <param name="SqlText">The SQL statement.</param>
    /// <param name="Parameters">The parameter values.</param>
    /// <returns>The provider's affected-row count.</returns>
    static public int Execute(DbConnection Connection, DbTransaction Transaction, string SqlText,
        params DbParameter[] Parameters)
    {
        using DbCommand Command = CreateCommand(Connection, Transaction, SqlText, Parameters);
        return Command.ExecuteNonQuery();
    }

    // ● properties
    /// <summary>
    /// The database path in private persistent storage. Reading this property does not create the file.
    /// </summary>
    static public string FilePath
    {
        get
        {
            string Root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(Root))
                throw new IOException("The application data directory is unavailable.");
            return Path.Combine(Root, "PInfo", "Data", "pinfo.db");
        }
    }
}
