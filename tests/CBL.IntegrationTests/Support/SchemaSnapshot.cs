using CBL.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CBL.IntegrationTests.Support;

/// <summary>Foto del esquema (tablas, columnas, claves, claves foráneas e índices) para compararlo entre EF y SQL Server.</summary>
public sealed class SchemaSnapshot
{
    public SortedSet<string> Tables { get; } = [];

    /// <summary>Clave "Tabla.Columna" → "tipo, NULL | NOT NULL".</summary>
    public SortedDictionary<string, string> Columns { get; } = [];

    public SortedSet<string> PrimaryKeys { get; } = [];

    public SortedSet<string> ForeignKeys { get; } = [];

    /// <summary>Índices no primarios, como "Tabla(Col1,Col2)" con prefijo UNIQUE cuando corresponde.</summary>
    public SortedSet<string> Indexes { get; } = [];

    /// <summary>
    /// Índices que EF crea por convención sobre una clave foránea (una sola columna, no únicos) y que el .sqlproj no
    /// define a propósito. Son opcionales: no se consideran diferencia.
    /// </summary>
    public SortedSet<string> ConventionIndexes { get; } = [];

    /// <summary>Lee el modelo de EF Core tal como lo vería el proveedor de SQL Server.</summary>
    public static SchemaSnapshot FromEfModel(CblDbContext db)
    {
        var snapshot = new SchemaSnapshot();
        var model = db.GetService<IDesignTimeModel>().Model;

        foreach (var entity in model.GetEntityTypes())
        {
            var table = entity.GetTableName()!;
            var store = StoreObjectIdentifier.Table(table, entity.GetSchema());
            snapshot.Tables.Add(table);

            foreach (var property in entity.GetProperties())
            {
                var column = property.GetColumnName(store)!;
                var nullable = property.IsColumnNullable(store) ? "NULL" : "NOT NULL";
                snapshot.Columns[$"{table}.{column}"] = $"{property.GetColumnType(store).ToLowerInvariant()}, {nullable}";
            }

            snapshot.PrimaryKeys.Add($"{table}({string.Join(",", entity.FindPrimaryKey()!.Properties.Select(p => p.GetColumnName(store)))})");

            foreach (var foreignKey in entity.GetForeignKeys())
            {
                var principal = foreignKey.PrincipalEntityType;
                var principalStore = StoreObjectIdentifier.Table(principal.GetTableName()!, principal.GetSchema());
                snapshot.ForeignKeys.Add(
                    $"{table}({string.Join(",", foreignKey.Properties.Select(p => p.GetColumnName(store)))})" +
                    $" -> {principal.GetTableName()}({string.Join(",", foreignKey.PrincipalKey.Properties.Select(p => p.GetColumnName(principalStore)))})");
            }

            var foreignKeyColumns = entity.GetForeignKeys()
                .Select(fk => string.Join(",", fk.Properties.Select(p => p.GetColumnName(store))))
                .ToHashSet();

            foreach (var index in entity.GetIndexes())
            {
                var columns = string.Join(",", index.Properties.Select(p => p.GetColumnName(store)));
                var key = $"{(index.IsUnique ? "UNIQUE " : string.Empty)}{table}({columns})";

                if (!index.IsUnique && foreignKeyColumns.Contains(columns) && index.Properties.Count == 1)
                {
                    snapshot.ConventionIndexes.Add(key);
                }
                else
                {
                    snapshot.Indexes.Add(key);
                }
            }
        }

        return snapshot;
    }

    /// <summary>Lee el esquema realmente desplegado (el .dacpac aplicado con SqlPackage).</summary>
    public static async Task<SchemaSnapshot> FromDatabaseAsync(string connectionString)
    {
        var snapshot = new SchemaSnapshot();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        foreach (var row in await QueryAsync(connection, """
            SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_SCHEMA = 'dbo'
            """))
        {
            snapshot.Tables.Add((string)row[0]!);
        }

        foreach (var row in await QueryAsync(connection, """
            SELECT c.TABLE_NAME, c.COLUMN_NAME, c.DATA_TYPE, c.CHARACTER_MAXIMUM_LENGTH,
                   c.NUMERIC_PRECISION, c.NUMERIC_SCALE, c.DATETIME_PRECISION, c.IS_NULLABLE
            FROM INFORMATION_SCHEMA.COLUMNS c
            JOIN INFORMATION_SCHEMA.TABLES t ON t.TABLE_NAME = c.TABLE_NAME AND t.TABLE_SCHEMA = c.TABLE_SCHEMA
            WHERE t.TABLE_TYPE = 'BASE TABLE' AND c.TABLE_SCHEMA = 'dbo'
            """))
        {
            var type = ((string)row[2]!).ToLowerInvariant() switch
            {
                "nvarchar" or "varchar" or "nchar" or "char" => $"{row[2]}({(Convert.ToInt32(row[3]) == -1 ? "max" : row[3])})",
                "decimal" or "numeric" => $"decimal({row[4]},{row[5]})",
                "datetime2" => $"datetime2({row[6]})",
                var other => other
            };

            snapshot.Columns[$"{row[0]}.{row[1]}"] = $"{type.ToLowerInvariant()}, {((string)row[7]! == "YES" ? "NULL" : "NOT NULL")}";
        }

        foreach (var row in await QueryAsync(connection, IndexQuery("i.is_primary_key = 1")))
        {
            snapshot.PrimaryKeys.Add($"{row[0]}({row[2]})");
        }

        foreach (var row in await QueryAsync(connection, IndexQuery("i.is_primary_key = 0 AND i.type > 0 AND i.is_hypothetical = 0")))
        {
            snapshot.Indexes.Add($"{((bool)row[1]! ? "UNIQUE " : string.Empty)}{row[0]}({row[2]})");
        }

        foreach (var row in await QueryAsync(connection, """
            SELECT OBJECT_NAME(fk.parent_object_id) AS child_table,
                   STRING_AGG(cc.name, ',') WITHIN GROUP (ORDER BY fkc.constraint_column_id) AS child_columns,
                   OBJECT_NAME(fk.referenced_object_id) AS parent_table,
                   STRING_AGG(pc.name, ',') WITHIN GROUP (ORDER BY fkc.constraint_column_id) AS parent_columns
            FROM sys.foreign_keys fk
            JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
            JOIN sys.columns cc ON cc.object_id = fkc.parent_object_id AND cc.column_id = fkc.parent_column_id
            JOIN sys.columns pc ON pc.object_id = fkc.referenced_object_id AND pc.column_id = fkc.referenced_column_id
            GROUP BY fk.object_id, fk.parent_object_id, fk.referenced_object_id
            """))
        {
            snapshot.ForeignKeys.Add($"{row[0]}({row[1]}) -> {row[2]}({row[3]})");
        }

        return snapshot;
    }

    private static string IndexQuery(string filter) => $"""
        SELECT OBJECT_NAME(i.object_id) AS table_name, i.is_unique,
               STRING_AGG(c.name, ',') WITHIN GROUP (ORDER BY ic.key_ordinal) AS columns_list
        FROM sys.indexes i
        JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0
        JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
        JOIN sys.tables t ON t.object_id = i.object_id
        WHERE {filter} AND t.is_ms_shipped = 0
        GROUP BY i.object_id, i.index_id, i.is_unique
        """;

    private static async Task<List<object?[]>> QueryAsync(SqlConnection connection, string sql)
    {
        var rows = new List<object?[]>();
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var values = new object?[reader.FieldCount];
            for (var i = 0; i < values.Length; i++)
            {
                values[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }

            rows.Add(values);
        }

        return rows;
    }
}

public static class SchemaComparer
{
    /// <summary>Diferencias entre el modelo de EF (esperado) y el esquema desplegado (real); vacío si coinciden.</summary>
    public static IReadOnlyList<string> Differences(SchemaSnapshot ef, SchemaSnapshot database)
    {
        var differences = new List<string>();

        foreach (var table in ef.Tables.Except(database.Tables))
        {
            differences.Add($"Tabla solo en EF: {table}");
        }

        foreach (var table in database.Tables.Except(ef.Tables))
        {
            differences.Add($"Tabla solo en el .sqlproj: {table}");
        }

        foreach (var (column, efType) in ef.Columns)
        {
            if (!database.Columns.TryGetValue(column, out var dbType))
            {
                differences.Add($"Columna solo en EF: {column} ({efType})");
            }
            else if (!string.Equals(efType, dbType, StringComparison.OrdinalIgnoreCase))
            {
                differences.Add($"Columna {column}: EF espera '{efType}' pero el .sqlproj define '{dbType}'");
            }
        }

        foreach (var column in database.Columns.Keys.Except(ef.Columns.Keys))
        {
            differences.Add($"Columna solo en el .sqlproj: {column} ({database.Columns[column]})");
        }

        Diff(ef.PrimaryKeys, database.PrimaryKeys, "Clave primaria", differences);
        Diff(ef.ForeignKeys, database.ForeignKeys, "Clave foránea", differences);

        // Índices: todo índice de EF debe existir en el .sqlproj, salvo los de convención sobre claves foráneas, y
        // todo índice del .sqlproj debe estar en el modelo de EF.
        foreach (var index in ef.Indexes.Except(database.Indexes))
        {
            differences.Add($"Índice solo en EF: {index}");
        }

        foreach (var index in database.Indexes.Except(ef.Indexes).Except(ef.ConventionIndexes))
        {
            differences.Add($"Índice solo en el .sqlproj: {index}");
        }

        return differences;
    }

    private static void Diff(IEnumerable<string> ef, IEnumerable<string> database, string what, List<string> differences)
    {
        foreach (var item in ef.Except(database))
        {
            differences.Add($"{what} solo en EF: {item}");
        }

        foreach (var item in database.Except(ef))
        {
            differences.Add($"{what} solo en el .sqlproj: {item}");
        }
    }
}
