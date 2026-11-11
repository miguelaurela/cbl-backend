using System.Net;
using System.Text.Json;
using CBL.IntegrationTests.Support;
using YamlDotNet.RepresentationModel;

namespace CBL.IntegrationTests;

/// <summary>
/// Swagger solo en Development y contrato: el documento OpenAPI que genera la API debe coincidir con el contrato escrito
/// en specs/001-gestion-prestamos/contracts/openapi.yaml, que es lo que consume el frontend. No necesitan base de datos.
/// </summary>
public class OpenApiTests
{
    private static readonly string[] HttpMethods = ["get", "post", "put", "delete"];

    private static async Task<JsonDocument> GeneratedDocumentAsync()
    {
        using var factory = new CblWebApplicationFactory();
        var response = await factory.CreateClient().GetAsync("/swagger/v1/swagger.json");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();

        // Para CI: se exporta el documento generado como artefacto (T107).
        var output = Environment.GetEnvironmentVariable("CBL_OPENAPI_OUT");
        if (!string.IsNullOrWhiteSpace(output))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            await File.WriteAllTextAsync(output, json);
        }

        return JsonDocument.Parse(json);
    }

    /// <summary>"METHOD /ruta" → códigos de estado documentados.</summary>
    private static Dictionary<string, SortedSet<string>> GeneratedOperations(JsonDocument document)
    {
        var operations = new Dictionary<string, SortedSet<string>>();

        foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject())
        {
            foreach (var method in path.Value.EnumerateObject().Where(m => HttpMethods.Contains(m.Name)))
            {
                var codes = method.Value.GetProperty("responses").EnumerateObject().Select(r => r.Name).ToList();
                operations[$"{method.Name.ToUpperInvariant()} {path.Name}"] = new SortedSet<string>(codes);
            }
        }

        return operations;
    }

    private static Dictionary<string, SortedSet<string>> ContractOperations()
    {
        var yaml = new YamlStream();
        using var reader = new StreamReader(RepoPaths.Combine("specs", "001-gestion-prestamos", "contracts", "openapi.yaml"));
        yaml.Load(reader);

        var root = (YamlMappingNode)yaml.Documents[0].RootNode;
        var paths = (YamlMappingNode)root.Children[new YamlScalarNode("paths")];
        var operations = new Dictionary<string, SortedSet<string>>();

        foreach (var (pathKey, pathValue) in paths.Children)
        {
            foreach (var (methodKey, methodValue) in ((YamlMappingNode)pathValue).Children)
            {
                var method = ((YamlScalarNode)methodKey).Value!;

                if (!HttpMethods.Contains(method))
                {
                    continue;   // parameters, summary, etc.
                }

                var responses = (YamlMappingNode)((YamlMappingNode)methodValue).Children[new YamlScalarNode("responses")];
                operations[$"{method.ToUpperInvariant()} {((YamlScalarNode)pathKey).Value}"] =
                    new SortedSet<string>(responses.Children.Keys.Select(k => ((YamlScalarNode)k).Value!));
            }
        }

        return operations;
    }

    [Fact]
    public async Task In_development_swagger_serves_the_document_with_the_bearer_scheme()
    {
        using var document = await GeneratedDocumentAsync();

        document.RootElement.GetProperty("components").GetProperty("securitySchemes").TryGetProperty("Bearer", out var bearer)
            .Should().BeTrue();
        bearer.GetProperty("scheme").GetString().Should().Be("bearer");
        bearer.GetProperty("bearerFormat").GetString().Should().Be("JWT");

        using var factory = new CblWebApplicationFactory();
        (await factory.CreateClient().GetAsync("/swagger/index.html")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Outside_development_swagger_is_not_served()
    {
        using var factory = new CblWebApplicationFactory(environment: "Production");
        var client = factory.CreateClient();

        foreach (var route in new[] { "/swagger/v1/swagger.json", "/swagger/index.html", "/swagger" })
        {
            var response = await client.GetAsync(route);

            // Sin endpoint, la política de respaldo (todo autenticado) responde 401; nunca sirve la documentación.
            response.StatusCode.Should().BeOneOf(new[] { HttpStatusCode.Unauthorized, HttpStatusCode.NotFound }, route);
            (await response.Content.ReadAsStringAsync()).Should().NotContain("openapi").And.NotContain("swagger-ui");
        }
    }

    [Fact]
    public async Task The_generated_document_has_exactly_the_operations_of_the_contract()
    {
        using var document = await GeneratedDocumentAsync();

        var generated = GeneratedOperations(document).Keys.ToList();
        var contract = ContractOperations().Keys.ToList();

        generated.Except(contract).Should().BeEmpty("la API no debe exponer operaciones que el contrato no documenta");
        contract.Except(generated).Should().BeEmpty("el contrato no debe documentar operaciones que la API no implementa");
    }

    [Fact]
    public async Task Every_operation_documents_the_same_status_codes_as_the_contract()
    {
        using var document = await GeneratedDocumentAsync();
        var generated = GeneratedOperations(document);
        var contract = ContractOperations();

        var differences = contract
            .Where(c => generated.ContainsKey(c.Key) && !c.Value.SetEquals(generated[c.Key]))
            .Select(c => $"{c.Key}: contrato [{string.Join(",", c.Value)}] vs API [{string.Join(",", generated[c.Key])}]")
            .ToList();

        differences.Should().BeEmpty("los códigos de estado documentados deben coincidir entre el contrato y el código");
    }
}
