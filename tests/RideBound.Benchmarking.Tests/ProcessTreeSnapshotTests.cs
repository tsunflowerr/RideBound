using RideBound.Benchmarking.Execution;

namespace RideBound.Benchmarking.Tests;

public sealed class ProcessTreeSnapshotTests
{
    [Fact]
    public void Selection_excludes_stale_parent_links_created_by_pid_reuse()
    {
        var root = new ProcessTreeSnapshot.ProcessInstanceIdentity(100, 1_000);
        var snapshot = new[]
        {
            new ProcessTreeSnapshot.ProcessSnapshotEntry(100, 10, 1_000),
            new ProcessTreeSnapshot.ProcessSnapshotEntry(200, 100, 900),
            new ProcessTreeSnapshot.ProcessSnapshotEntry(300, 200, 1_100),
            new ProcessTreeSnapshot.ProcessSnapshotEntry(201, 100, 1_001),
            new ProcessTreeSnapshot.ProcessSnapshotEntry(301, 201, 1_002),
        };

        var selected = ProcessTreeSnapshot.SelectProcessTree(root, snapshot);

        Assert.Equal(
            [
                root,
                new ProcessTreeSnapshot.ProcessInstanceIdentity(201, 1_001),
                new ProcessTreeSnapshot.ProcessInstanceIdentity(301, 1_002),
            ],
            selected);
    }

    [Fact]
    public void Selection_does_not_follow_a_descendant_of_an_excluded_stale_process()
    {
        var root = new ProcessTreeSnapshot.ProcessInstanceIdentity(100, 1_000);
        var snapshot = new[]
        {
            new ProcessTreeSnapshot.ProcessSnapshotEntry(200, 100, 999),
            new ProcessTreeSnapshot.ProcessSnapshotEntry(300, 200, 1_001),
        };

        var selected = ProcessTreeSnapshot.SelectProcessTree(root, snapshot);

        Assert.Equal([root], selected);
    }

    [Fact]
    public void Selection_rejects_a_child_older_than_its_reused_intermediate_parent()
    {
        var root = new ProcessTreeSnapshot.ProcessInstanceIdentity(100, 1_000);
        var snapshot = new[]
        {
            new ProcessTreeSnapshot.ProcessSnapshotEntry(200, 100, 1_200),
            new ProcessTreeSnapshot.ProcessSnapshotEntry(300, 200, 1_100),
            new ProcessTreeSnapshot.ProcessSnapshotEntry(301, 200, 1_201),
        };

        var selected = ProcessTreeSnapshot.SelectProcessTree(root, snapshot);

        Assert.Equal(
            [
                root,
                new ProcessTreeSnapshot.ProcessInstanceIdentity(200, 1_200),
                new ProcessTreeSnapshot.ProcessInstanceIdentity(301, 1_201),
            ],
            selected);
    }
}
