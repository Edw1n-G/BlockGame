using Basics.Settings;

namespace BlockGame.Tests;

public class FastNoiseConcurrencyTests
{
    [Fact]
    [Trait("Category", "Threaded")]
    public async Task SharedNode_ConcurrentGenerationMatchesSerialReference()
    {
        using var noise = FastNoise.FromEncodedNodeTree(new GameSettings().Worldtype);
        Assert.NotNull(noise);
        TestContext.Current.TestOutputHelper?.WriteLine($"FastNoise active feature set: {noise.GetActiveFeatureSet()}");
        const int workers = 4, rounds = 30;
        float[] Generate(int i)
        {
            float[] values = new float[16 * 17 * 16];
            noise.GenUniformGrid3D(values, i * 16, -i * 16, i * -32, 16, 17, 16, 1, 1, 1, i + 101);
            return values;
        }
        var expected = Enumerable.Range(0, workers).Select(Generate).ToArray();
        using var start = new ManualResetEventSlim();
        var tasks = Enumerable.Range(0, workers).Select(i => Task.Run(() =>
        {
            start.Wait();
            for (int round = 0; round < rounds; round++) Assert.Equal(expected[i], Generate(i));
        })).ToArray();
        start.Set();
        await Task.WhenAll(tasks);
    }
}
