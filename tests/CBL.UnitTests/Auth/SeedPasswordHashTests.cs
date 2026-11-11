using System.Text.RegularExpressions;
using CBL.Infrastructure.Persistence;
using CBL.Infrastructure.Security;
using CBL.UnitTests.Support;

namespace CBL.UnitTests.Auth;

/// <summary>La contraseña "123" del seed es solo de ejemplo, pero debe verificarse con el mismo hasher que usa la API.</summary>
public partial class SeedPasswordHashTests
{
    private readonly PasswordHasherAdapter _hasher = new();

    [Fact]
    public void Hashes_in_Seed_sql_verify_the_sample_password()
    {
        var sql = File.ReadAllText(RepoPaths.Combine("db", "CBL.Database", "PostDeployment", "Seed.sql"));
        var hashes = HashLiteral().Matches(sql).Select(m => m.Groups[1].Value).ToList();

        hashes.Should().HaveCount(2, "usuario@test.com y admin@test.com");
        hashes.Should().AllSatisfy(hash =>
        {
            _hasher.Verify(hash, "123").Should().BeTrue();
            _hasher.Verify(hash, "124").Should().BeFalse();
        });
    }

    [Fact]
    public void Seed_sql_and_the_in_memory_seeder_use_the_same_hash()
    {
        var sql = File.ReadAllText(RepoPaths.Combine("db", "CBL.Database", "PostDeployment", "Seed.sql"));

        sql.Should().Contain(SeedData.SamplePasswordHash);
    }

    [Fact]
    public void Seed_sql_and_the_in_memory_seeder_use_the_same_ids_and_emails()
    {
        var sql = File.ReadAllText(RepoPaths.Combine("db", "CBL.Database", "PostDeployment", "Seed.sql"));

        sql.Should().Contain(SeedData.BorrowerId.ToString()).And.Contain(SeedData.AdminId.ToString())
            .And.Contain(SeedData.BorrowerEmail).And.Contain(SeedData.AdminEmail);
    }

    [GeneratedRegex("N'(AQAAAA[^']+)'")]
    private static partial Regex HashLiteral();
}
