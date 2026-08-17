namespace RideBound.Benchmarking.Tests;

internal sealed class TestDirectory : IDisposable
{
    public TestDirectory()
    {
        Root = Path.Combine(
            Path.GetTempPath(),
            "ridebound-wp6-tests",
            Guid.NewGuid().ToString("N"));
        RepositoryRoot = Path.Combine(Root, "repository");
        CacheRoot = Path.Combine(Root, "cache");
        ExtractionRoot = Path.Combine(Root, "extraction");
        Directory.CreateDirectory(RepositoryRoot);
    }

    public string Root { get; }

    public string RepositoryRoot { get; }

    public string CacheRoot { get; }

    public string ExtractionRoot { get; }

    public void Dispose()
    {
        if (!Directory.Exists(Root))
        {
            return;
        }

        foreach (var file in Directory.GetFiles(Root, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(Root, recursive: true);
    }
}
