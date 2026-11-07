using BlockGame.Benchmarks.Comparison;

namespace BlockGame.Tests;

public class PortableBundleTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("benchmark-bundle-test-");

    [Fact]
    public void PackThenVerifyPasses()
    {
        string zip = Pack();
        PortableBundle.Verify(zip);
    }

    [Fact]
    public void ModifiedZipFailsVerify()
    {
        string zip = Pack();
        File.AppendAllText(zip, "tampered");
        var failure = Assert.Throws<InvalidDataException>(() => PortableBundle.Verify(zip));
        Assert.Equal("bundle hash mismatch", failure.Message);
    }

    [Fact]
    public void MissingHashFileFailsVerify()
    {
        string zip = Pack();
        File.Delete(zip + ".sha256");
        var failure = Assert.Throws<InvalidDataException>(() => PortableBundle.Verify(zip));
        Assert.Equal("bundle hash file missing", failure.Message);
    }

    private string Pack()
    {
        var run = _directory.CreateSubdirectory("run");
        File.WriteAllText(Path.Combine(run.FullName, "benchmark-run.json"), "{}");
        return PortableBundle.Pack(run.FullName, Path.Combine(_directory.FullName, "run.zip"));
    }

    public void Dispose() => _directory.Delete(recursive: true);
}
