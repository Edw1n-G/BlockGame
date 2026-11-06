using Basics.Game;
using Basics.Game.Logic;
using Basics.Game.Logic.TerrainManaging;
using Basics.Game.Utilities;

namespace BlockGame.Tests;

public class SchedulerShutdownTests
{
    private sealed class TestJob(Action<JobContext> execute, Action? discarded = null) : IJob
    {
        public void Execute(JobContext context) => execute(context);
        public void Discard(JobContext? context) => discarded?.Invoke();
    }

    [Fact]
    public async Task Stop_DiscardsQueuedJobs_AndWaitsForInFlightWork()
    {
        using var session = new EngineSession(new EngineContext(new GameSettings(), startJobScheduler: true, workerCores: 1));
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        int ran = 0, discarded = 0;
        session.JobScheduler.EnqueueLow(new TestJob(_ => { entered.Set(); release.Wait(); }));
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            session.JobScheduler.EnqueueLow(new TestJob(_ => Interlocked.Increment(ref ran), () => Interlocked.Increment(ref discarded)));
            Assert.False(session.JobScheduler.Stop(TimeSpan.FromMilliseconds(50)));
            Assert.Equal(1, discarded);
            Assert.False(session.JobScheduler.EnqueueLow(new TestJob(_ => { })));
            Assert.Equal(0, ran);
        }
        finally { release.Set(); }
        Assert.True(await Task.Run(() => session.JobScheduler.Stop(TimeSpan.FromSeconds(5))));
        Assert.True(session.JobScheduler.Stop());
    }

    [Fact]
    public void OldMeshRequest_CannotRemoveNewMarker()
    {
        using var session = new EngineSession(new GameSettings());
        var provider = session.ChunkProvider;
        var coord = new ChunkCoord(0, 0, 0, 0);
        provider.QueueMesh(coord);
        Assert.True(provider.PendingMeshRequests.TryDequeue(out var old));
        provider.UnloadChunk(coord);
        provider.QueueMesh(coord);
        Assert.True(provider.PendingMeshRequests.TryDequeue(out var current));
        Assert.NotEqual(old.Generation, current.Generation);
        new MeshgenerateJob(old, provider).Discard(null);
        Assert.Equal(current.Generation, provider.QueuedForMeshing[coord]);
        Assert.True(provider.CompleteMeshRequest(current));
    }

    [Fact]
    public void MissingChunk_ClearsItsRequestMarker()
    {
        using var session = new EngineSession(new GameSettings());
        var coord = new ChunkCoord(1, 0, 0, 0);
        session.ChunkProvider.QueueMesh(coord);
        Assert.True(session.ChunkProvider.PendingMeshRequests.TryDequeue(out var request));
        var context = new JobContext(session.TerrainGenerator, session.ChunkProvider, session.BlockRegistry);
        new MeshgenerateJob(request, session.ChunkProvider).Execute(context);
        Assert.False(session.ChunkProvider.QueuedForMeshing.ContainsKey(coord));
    }

    [Fact]
    public void FailedMesh_ClearsItsMarker()
    {
        using var session = new EngineSession(new GameSettings());
        var coord = new ChunkCoord(0, 0, 0, 3);
        session.ChunkProvider.Chunkdata[coord] = new ChunkData(coord);
        session.ChunkProvider.QueueMesh(coord);
        Assert.True(session.ChunkProvider.PendingMeshRequests.TryDequeue(out var request));
        var context = new JobContext(session.TerrainGenerator, session.ChunkProvider, session.BlockRegistry);
        Assert.Throws<InvalidOperationException>(() => new MeshgenerateJob(request, session.ChunkProvider).Execute(context));
        Assert.False(session.ChunkProvider.QueuedForMeshing.ContainsKey(coord));
    }

    [Fact]
    public void ShutdownTimeout_DoesNotDisposeProviderWhileJobRuns()
    {
        using var session = new EngineSession(new EngineContext(new GameSettings(), startJobScheduler: true, workerCores: 1));
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var coord = new ChunkCoord(0, 0, 0, 0);
        session.ChunkProvider.Chunkdata[coord] = new ChunkData(coord);
        session.JobScheduler.EnqueueLow(new TestJob(_ => { entered.Set(); release.Wait(); }));
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            Assert.False(session.Context.Shutdown(TimeSpan.FromMilliseconds(50)));
            Assert.True(session.ChunkProvider.Chunkdata.ContainsKey(coord));
        }
        finally { release.Set(); }
        Assert.True(session.Context.Shutdown(TimeSpan.FromSeconds(5)));
        Assert.Empty(session.ChunkProvider.Chunkdata);
    }

    [Fact]
    public async Task WorkerFailure_IsRecordedAndWorkerContinues()
    {
        using var session = new EngineSession(new EngineContext(new GameSettings(), startJobScheduler: true, workerCores: 1));
        using var finished = new ManualResetEventSlim();
        session.JobScheduler.EnqueueLow(new TestJob(_ => throw new InvalidOperationException("test failure")));
        session.JobScheduler.EnqueueLow(new TestJob(_ => finished.Set()));
        Assert.True(await Task.Run(() => finished.Wait(TimeSpan.FromSeconds(5))));
        Assert.Contains(session.JobScheduler.Failures, error => error is InvalidOperationException && error.Message == "test failure");
    }
}
