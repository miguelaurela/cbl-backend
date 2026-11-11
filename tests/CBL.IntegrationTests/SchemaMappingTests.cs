using CBL.Domain.Users;
using CBL.Infrastructure.Persistence;
using CBL.IntegrationTests.Support;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CBL.IntegrationTests;

/// <summary>
/// Constitución, principio II: el .sqlproj es la fuente de verdad y EF solo lo mapea. Esta prueba compara el modelo de EF
/// con el esquema REALMENTE desplegado desde el .dacpac y falla ante cualquier diferencia. Solo corre en CI
/// (necesita SQL Server real).
/// </summary>
[Trait("Category", "Integration")]
public sealed class SchemaMappingTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    [SqlServerFact(RequiresSqlServer = true)]
    public async Task The_ef_model_matches_the_schema_deployed_from_the_sqlproj()
    {
        using var scope = Factory.Services.CreateScope();
        var efSchema = SchemaSnapshot.FromEfModel(scope.ServiceProvider.GetRequiredService<CblDbContext>());
        var dbSchema = await SchemaSnapshot.FromDatabaseAsync(TestEnvironment.SqlConnectionString!);

        SchemaComparer.Differences(efSchema, dbSchema).Should().BeEmpty(
            "el mapeo de EF (OnModelCreating) debe coincidir con db/CBL.Database; el .sqlproj manda");
    }

    [SqlServerFact(RequiresSqlServer = true)]
    public async Task The_deployed_schema_has_the_four_tables_and_the_post_deployment_seed()
    {
        await using var connection = new SqlConnection(TestEnvironment.SqlConnectionString);
        await connection.OpenAsync();

        (await ScalarAsync(connection, "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'"))
            .Should().Be(4);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CblDbContext>();

        var users = await db.Users.AsNoTracking().OrderBy(u => u.Email).ToListAsync();
        users.Select(u => (u.Email, u.Role, u.IsActive)).Should().Equal(
            (TestUsers.AdminEmail, Role.Admin, true),
            (TestUsers.BorrowerEmail, Role.Borrower, true));

        var parameters = await db.SystemParameters.AsNoTracking().ToDictionaryAsync(p => p.Key, p => p.Value);
        parameters.Should().Contain("LoanAmountMin", "1000").And.Contain("LoanAmountMax", "100000");
    }

    [SqlServerFact(RequiresSqlServer = true)]
    public async Task The_check_constraints_of_the_sqlproj_reject_invalid_rows_even_without_the_api()
    {
        await using var connection = new SqlConnection(TestEnvironment.SqlConnectionString);
        await connection.OpenAsync();

        async Task<bool> InsertFailsAsync(string amount, string term, string status)
        {
            var sql = $"""
                INSERT INTO [dbo].[Loans] ([Id],[BorrowerId],[Amount],[TermMonths],[Status],[CreatedAt])
                VALUES (NEWID(), '{TestUsers.BorrowerId}', {amount}, {term}, '{status}', SYSUTCDATETIME())
                """;

            try
            {
                await using var command = new SqlCommand(sql, connection);
                await command.ExecuteNonQueryAsync();

                return false;
            }
            catch (SqlException ex) when (ex.Number == 547)
            {
                return true;   // violación de restricción CHECK o de clave foránea
            }
        }

        (await InsertFailsAsync("0", "12", "Pending")).Should().BeTrue("CK_Loans_Amount");
        (await InsertFailsAsync("1000", "0", "Pending")).Should().BeTrue("CK_Loans_TermMonths");
        (await InsertFailsAsync("1000", "61", "Pending")).Should().BeTrue("CK_Loans_TermMonths");
        (await InsertFailsAsync("1000", "12", "Cancelled")).Should().BeTrue("CK_Loans_Status");
        (await InsertFailsAsync("1000", "12", "Pending")).Should().BeFalse("una fila válida sí se acepta");
    }

    private static async Task<int> ScalarAsync(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection);

        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }
}

/// <summary>Pruebas del propio comparador (sin base de datos): un cambio de longitud en el mapeo tiene que detectarse.</summary>
public class SchemaComparerTests
{
    private static SchemaSnapshot Baseline()
    {
        var snapshot = new SchemaSnapshot();
        snapshot.Tables.Add("Users");
        snapshot.Columns["Users.Email"] = "nvarchar(256), NOT NULL";
        snapshot.Columns["Users.IsActive"] = "bit, NOT NULL";
        snapshot.PrimaryKeys.Add("Users(Id)");
        snapshot.Indexes.Add("UNIQUE Users(Email)");

        return snapshot;
    }

    [Fact]
    public void Identical_schemas_have_no_differences() =>
        SchemaComparer.Differences(Baseline(), Baseline()).Should().BeEmpty();

    [Fact]
    public void A_changed_column_length_in_the_mapping_is_detected()
    {
        var ef = Baseline();
        ef.Columns["Users.Email"] = "nvarchar(300), NOT NULL";

        SchemaComparer.Differences(ef, Baseline()).Should().ContainSingle().Which.Should().Contain("Users.Email");
    }

    [Fact]
    public void A_changed_nullability_is_detected()
    {
        var ef = Baseline();
        ef.Columns["Users.IsActive"] = "bit, NULL";

        SchemaComparer.Differences(ef, Baseline()).Should().ContainSingle().Which.Should().Contain("Users.IsActive");
    }

    [Fact]
    public void Missing_and_extra_columns_tables_and_indexes_are_detected()
    {
        var ef = Baseline();
        ef.Columns["Users.Nueva"] = "int, NOT NULL";
        ef.Tables.Add("Otra");
        var database = Baseline();
        database.Indexes.Add("Users(FullName)");

        var differences = SchemaComparer.Differences(ef, database);

        differences.Should().Contain(d => d.Contains("Users.Nueva"))
            .And.Contain(d => d.Contains("Tabla solo en EF: Otra"))
            .And.Contain(d => d.Contains("Índice solo en el .sqlproj: Users(FullName)"));
    }

    [Fact]
    public void Foreign_key_indexes_created_by_ef_convention_are_tolerated_but_unexpected_ones_are_not()
    {
        var ef = Baseline();
        ef.ConventionIndexes.Add("Users(OwnerId)");
        var withConvention = Baseline();

        SchemaComparer.Differences(ef, withConvention).Should().BeEmpty();

        var database = Baseline();
        database.Indexes.Add("Users(OwnerId)");
        SchemaComparer.Differences(ef, database).Should().BeEmpty("el índice de convención de EF coincide con uno del .sqlproj");

        database.Indexes.Add("Users(Otro)");
        SchemaComparer.Differences(ef, database).Should().ContainSingle().Which.Should().Contain("Users(Otro)");
    }
}
