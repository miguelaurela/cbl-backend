namespace CBL.IntegrationTests.Support;

public static class RepoPaths
{
    public static string Root { get; } = FindRoot();

    public static string Combine(params string[] parts) => Path.Combine([Root, .. parts]);

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
