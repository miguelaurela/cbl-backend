using System.Xml.Linq;

namespace CBL.UnitTests.Architecture;

/// <summary>Constitución, principio I: las referencias solo apuntan hacia adentro y el dominio no depende de nada.</summary>
public class ProjectReferencesTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void Domain_has_no_project_or_package_references()
    {
        References("src/CBL.Domain/CBL.Domain.csproj").Should().BeEmpty();
    }

    [Theory]
    [InlineData("src/CBL.Application/CBL.Application.csproj", new[] { "CBL.Domain" })]
    [InlineData("src/CBL.Infrastructure/CBL.Infrastructure.csproj", new[] { "CBL.Application" })]
    [InlineData("src/CBL.Api/CBL.Api.csproj", new[] { "CBL.Application", "CBL.Infrastructure" })]
    public void Production_projects_reference_only_inner_layers(string project, string[] allowed)
    {
        var projectReferences = References(project, "ProjectReference");

        projectReferences.Should().BeEquivalentTo(allowed);
    }

    private static string[] References(string relativePath, params string[] kinds)
    {
        var document = XDocument.Load(Path.Combine(Root, relativePath));
        var names = kinds.Length == 0 ? new[] { "ProjectReference", "PackageReference" } : kinds;

        return document.Descendants()
            .Where(e => names.Contains(e.Name.LocalName))
            .Select(e => (string?)e.Attribute("Include") ?? string.Empty)
            .Select(include => Path.GetFileNameWithoutExtension(include.Replace('\\', '/')))
            .ToArray();
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CBL.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("No se encontró CBL.sln.");
    }
}
