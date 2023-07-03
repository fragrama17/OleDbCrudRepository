using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.OleDb;
using System.Reflection;

namespace OleDbCrudRepository;

public interface ICrudRepository<T, in TId>
{
    T? FindById(TId id);
    IEnumerable<T?> FindAll();
    bool Create(T entity);
    bool Update(TId id, T entity);
    bool Delete(TId id);
}

public abstract class OleDbCrudRepository<T, TId> : ICrudRepository<T, TId>
{
    private readonly PropertyInfo[] _properties;
    private readonly Dictionary<string, string> _propertyColumnMappings;
    private readonly string _tableName;
    private readonly string _idColumnName;

    //pass an already open connection
    protected OleDbCrudRepository()
    {
        _properties = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public);
        _propertyColumnMappings = GetPropertyColumnMappings();
        _tableName = GetTableName();
        _idColumnName = GetIdColumnName();
    }

    public T? FindById(TId id)
    {
        var conn = OleDbPool.GetOpenConnection();
        var command = conn.CreateCommand();

        command.CommandText = typeof(TId).FullName == "System.String"
            ? $"SELECT {GetColumnList()} FROM {_tableName} WHERE {_idColumnName} = '{id}'"
            : $"SELECT {GetColumnList()} FROM {_tableName} WHERE {_idColumnName} = {id}";

        using var reader = command.ExecuteReader();
        var result = reader.Read() ? MapToObject(reader) : default;

        OleDbPool.ReleaseConnection(conn);
        return result;
    }

    public IEnumerable<T?> FindAll()
    {
        var conn = OleDbPool.GetOpenConnection();
        var command = conn.CreateCommand();

        command.CommandText = $"SELECT {GetColumnList()} FROM {_tableName}";

        using var reader = command.ExecuteReader();
        var result = new List<T>();
        while (reader.Read())
        {
            result.Add(MapToObject(reader));
        }

        OleDbPool.ReleaseConnection(conn);
        return result;
    }

    public bool Create(T entity)
    {
        var conn = OleDbPool.GetNewOpenConnection();
        var command = conn.CreateCommand();
        command.CommandText = BuildInsertStatement(entity);

        foreach (var parameter in GetParameters(entity))
        {
            command.Parameters.Add(parameter);
        }

        var ok = command.ExecuteNonQuery() > 0;
        OleDbPool.ReleaseConnection(conn);
        return ok;
    }

    public bool Update(TId id, T entity)
    {
        var conn = OleDbPool.GetNewOpenConnection();
        var command = conn.CreateCommand();
        command.CommandText = BuildUpdateStatement(id, entity);

        foreach (var parameter in GetParameters(entity))
        {
            command.Parameters.Add(parameter);
        }

        var ok = command.ExecuteNonQuery() > 0;
        OleDbPool.ReleaseConnection(conn);
        return ok;
    }

    public bool Delete(TId id)
    {
        var conn = OleDbPool.GetNewOpenConnection();
        var command = conn.CreateCommand();
        command.CommandText = typeof(TId).FullName == "System.String"
            ? $"DELETE FROM {_tableName} WHERE {_idColumnName} = '{id}'"
            : $"DELETE FROM {_tableName} WHERE {_idColumnName} = {id}";

        var ok = command.ExecuteNonQuery() > 0;
        OleDbPool.ReleaseConnection(conn);
        return ok;
    }

    private string BuildInsertStatement(T entity)
    {
        var properties = _properties
            .Where(p => GetColumnName(p) != _idColumnName && p.GetValue(entity) != null);

        var propertyInfos = properties as PropertyInfo[] ?? properties.ToArray();
        var columnNames = string.Join(", ", propertyInfos.Select(GetColumnName));
        var parameterNames = string.Join(", ", propertyInfos.Select(p => $"@{p.Name}"));

        return $"INSERT INTO {_tableName} ({columnNames}) VALUES ({parameterNames})";
    }

    private string BuildUpdateStatement(TId id, T entity)
    {
        var properties = _properties
            .Where(p => GetColumnName(p) != _idColumnName && p.GetValue(entity) != null);

        var assignments = string.Join(", ", properties.Select(p => $"{GetColumnName(p)} = @{p.Name}"));

        return $"UPDATE {_tableName} SET {assignments} WHERE {_idColumnName} = {id}";
    }

    private string GetIdColumnName()
    {
        var keyProperty = _properties.FirstOrDefault(p => p.GetCustomAttribute<KeyAttribute>() != null);
        if (keyProperty == default)
        {
            throw new DataException($"You must provide a Key attribute for class {typeof(T).Name}");
        }

        var columnAttribute = keyProperty.GetCustomAttribute<ColumnAttribute>();
        if (columnAttribute != null && !string.IsNullOrEmpty(columnAttribute.Name))
            return columnAttribute.Name;

        return keyProperty.Name;
    }

    private static string GetTableName()
    {
        var tableAttribute = typeof(T).GetCustomAttribute<TableAttribute>();
        if (tableAttribute != null && !string.IsNullOrEmpty(tableAttribute.Name))
            return tableAttribute.Name;

        return typeof(T).Name;
    }

    private IEnumerable<OleDbParameter> GetParameters(T entity)
    {
        var properties = _properties
            .Where(p => GetColumnName(p) != _idColumnName && p.GetValue(entity) != null);

        return (from property in properties
            let value = property.GetValue(entity)
            select new OleDbParameter($"@{property.Name}", value)).ToList();
    }

    private string GetColumnList()
    {
        var properties = _properties
            .Where(p => _propertyColumnMappings.ContainsKey(p.Name));

        var columnNames = string.Join(", ", properties.Select(GetColumnName));

        return columnNames;
    }

    private string GetColumnName(PropertyInfo property)
    {
        return _propertyColumnMappings[property.Name];
    }

    private Dictionary<string, string> GetPropertyColumnMappings()
    {
        var mappings = new Dictionary<string, string>();

        foreach (var property in _properties)
        {
            var columnAttribute = property.GetCustomAttribute<ColumnAttribute>();
            if (columnAttribute != null && !string.IsNullOrEmpty(columnAttribute.Name))
            {
                mappings[property.Name] = columnAttribute.Name;
            }
            else
            {
                mappings[property.Name] = property.Name;
            }
        }

        return mappings;
    }

    private T? MapToObject(IDataRecord record)
    {
        var instance = Activator.CreateInstance<T>();

        foreach (var property in _properties)
        {
            var columnName = _propertyColumnMappings[property.Name];
            var propertyType = property.PropertyType;

            var value = record[columnName];
            if (value == DBNull.Value)
            {
                value = propertyType.IsValueType ? Activator.CreateInstance(propertyType) : null;
            }

            property.SetValue(instance, value);
        }

        return instance;
    }
}