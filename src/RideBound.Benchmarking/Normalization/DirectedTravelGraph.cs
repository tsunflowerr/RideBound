namespace RideBound.Benchmarking.Normalization;

internal sealed class DirectedTravelGraph
{
    private readonly IReadOnlyDictionary<int, IReadOnlyList<WeightedArc>> outgoing;
    private readonly IReadOnlyDictionary<int, int> componentByNode;

    public DirectedTravelGraph(
        IReadOnlyCollection<int> nodes,
        IReadOnlyCollection<WeightedArc> arcs)
    {
        if (nodes.Count == 0 || arcs.Count == 0)
        {
            throw new InvalidDataException("Network must contain nodes and directed arcs.");
        }

        var nodeSet = nodes.ToHashSet();
        var forward = nodes.ToDictionary(node => node, _ => new List<WeightedArc>());
        var reverse = nodes.ToDictionary(node => node, _ => new List<int>());

        foreach (var arc in arcs
            .OrderBy(value => value.From)
            .ThenBy(value => value.To)
            .ThenBy(value => value.TravelTimeMs))
        {
            if (!nodeSet.Contains(arc.From) || !nodeSet.Contains(arc.To))
            {
                throw new InvalidDataException("Network arc references an unregistered node.");
            }

            if (arc.From == arc.To || arc.TravelTimeMs <= 0)
            {
                throw new InvalidDataException(
                    "Network arcs must be positive and have distinct endpoints.");
            }

            forward[arc.From].Add(arc);
            reverse[arc.To].Add(arc.From);
        }

        outgoing = forward.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<WeightedArc>)pair.Value);
        componentByNode = FindStrongComponents(nodes, forward, reverse);
    }

    public bool Contains(int node) => componentByNode.ContainsKey(node);

    public bool IsStronglyConnected(int first, int second) =>
        componentByNode.TryGetValue(first, out var firstComponent)
        && componentByNode.TryGetValue(second, out var secondComponent)
        && firstComponent == secondComponent;

    public IReadOnlyDictionary<int, long> ShortestPathsFrom(
        int source,
        IReadOnlySet<int> targets)
    {
        if (!outgoing.ContainsKey(source))
        {
            throw new InvalidDataException($"Unknown source node '{source}'.");
        }

        var distances = new Dictionary<int, long> { [source] = 0 };
        var queue = new PriorityQueue<int, long>();
        queue.Enqueue(source, 0);
        var remaining = targets.Where(target => target != source).ToHashSet();

        while (queue.TryDequeue(out var node, out var distance))
        {
            if (!distances.TryGetValue(node, out var current) || current != distance)
            {
                continue;
            }

            remaining.Remove(node);

            if (remaining.Count == 0)
            {
                break;
            }

            foreach (var arc in outgoing[node])
            {
                long candidate;

                try
                {
                    candidate = checked(distance + arc.TravelTimeMs);
                }
                catch (OverflowException exception)
                {
                    throw new InvalidDataException(
                        "Shortest-path accumulation overflowed Int64.",
                        exception);
                }

                if (distances.TryGetValue(arc.To, out var existing)
                    && existing <= candidate)
                {
                    continue;
                }

                distances[arc.To] = candidate;
                queue.Enqueue(arc.To, candidate);
            }
        }

        if (remaining.Count != 0)
        {
            throw new InvalidDataException(
                $"Directed paths are missing from '{source}' to selected nodes.");
        }

        return distances;
    }

    private static IReadOnlyDictionary<int, int> FindStrongComponents(
        IReadOnlyCollection<int> nodes,
        IReadOnlyDictionary<int, List<WeightedArc>> forward,
        IReadOnlyDictionary<int, List<int>> reverse)
    {
        var visited = new HashSet<int>();
        var finishOrder = new List<int>(nodes.Count);

        foreach (var start in nodes.Order())
        {
            if (!visited.Add(start))
            {
                continue;
            }

            var stack = new Stack<(int Node, int NextIndex)>();
            stack.Push((start, 0));

            while (stack.Count != 0)
            {
                var (node, nextIndex) = stack.Pop();
                var edges = forward[node];

                if (nextIndex < edges.Count)
                {
                    stack.Push((node, nextIndex + 1));
                    var next = edges[nextIndex].To;

                    if (visited.Add(next))
                    {
                        stack.Push((next, 0));
                    }

                    continue;
                }

                finishOrder.Add(node);
            }
        }

        var result = new Dictionary<int, int>();
        var component = 0;

        for (var index = finishOrder.Count - 1; index >= 0; index--)
        {
            var start = finishOrder[index];

            if (result.ContainsKey(start))
            {
                continue;
            }

            var stack = new Stack<int>();
            stack.Push(start);
            result[start] = component;

            while (stack.TryPop(out var node))
            {
                foreach (var next in reverse[node].Order())
                {
                    if (result.TryAdd(next, component))
                    {
                        stack.Push(next);
                    }
                }
            }

            component++;
        }

        return result;
    }
}

internal readonly record struct WeightedArc(int From, int To, long TravelTimeMs);
