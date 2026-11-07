using BlockGame.Benchmarks.Workloads;

namespace BlockGame.Tests;

public class SelectionMovementWorkloadTests
{
    public static IEnumerable<object[]> Cases => SelectionMovementWorkload.Cases.Select(id => new object[] {id});
    [Theory]
    [MemberData(nameof(Cases))]
    public void ReferencePathReturnsToSameSetAndDoesEquivalentWork(string id)
    {
        var workload = new SelectionRebuildWorkload(id);
        var before = workload.Snapshot();
        long first = workload.Run();
        Assert.True(first > 0); Assert.True(before.SetEquals(workload.Snapshot()));
        Assert.Equal(first,workload.Run()); Assert.True(before.SetEquals(workload.Snapshot()));
        var candidate = new SelectionDeltaWorkload(id);
        for (int i = 0; i < 4; i++)
        {
            Assert.Equal(first,candidate.Run());
            Assert.True(before.SetEquals(candidate.Snapshot()));
        }
    }
}
