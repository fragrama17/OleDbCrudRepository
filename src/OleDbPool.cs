using System.Data;
using System.Data.OleDb;

namespace OleDbCrudRepository;

public static class OleDbPool
{
    private const string ConnectionStringEnv = "OLE_DB_CONNECTION_STRING";
    private static readonly string ConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnv);
    private static readonly OleDbConnectionPool? Instance = Init();

    private static OleDbConnectionPool Init()
    {
        if (ConnectionString == null)
        {
            throw new DataException(
                $"You Must Provide a Connection String as Environment Variable {ConnectionStringEnv}");
        }

        return new OleDbConnectionPool(ConnectionString, Environment.ProcessorCount);
    }

    public static OleDbConnection GetOpenConnection()
    {
        return Instance.Get();
    }

    public static OleDbConnection GetNewOpenConnection()
    {
        var newConn = new OleDbConnection(ConnectionString);
        newConn.Open();
        return newConn;
    }

    public static void ReleaseConnection(OleDbConnection connection)
    {
        Instance.DisposeOrRecycle(connection);
    }

    private sealed class OleDbConnectionPool
    {
        private readonly string _connectionString;
        private readonly int _maxPoolSize;
        private readonly Queue<OleDbConnection> _pool;

        public OleDbConnectionPool(string connectionString, int maxPoolSize)
        {
            _connectionString = connectionString;
            _maxPoolSize = maxPoolSize;
            _pool = new Queue<OleDbConnection>();
        }

        public OleDbConnection Get()
        {
            lock (_pool)
            {
                if (_pool.Count > 0)
                {
                    return _pool.Dequeue();
                }
            }

            var connection = new OleDbConnection(_connectionString);
            connection.Open();
            return connection;
        }

        public void DisposeOrRecycle(OleDbConnection connection)
        {
            lock (_pool)
            {
                if (_pool.Count < _maxPoolSize)
                {
                    _pool.Enqueue(connection);
                }
                else
                {
                    connection.Dispose();
                }
            }
        }
    }
}