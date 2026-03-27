namespace CdrBilling.Domain.Services;

/// <summary>
/// A prefix trie that stores values at each inserted prefix node.
/// GetAllPrefixMatches returns all values whose key is a prefix of the given input.
/// O(k) lookup where k = length of input string.
/// </summary>
public sealed class PrefixTrie<TValue>
{
    private sealed class Node
    {
        public readonly Dictionary<char, Node> Children = new(4);
        public TValue? Value;
        public bool HasValue;
    }

    private readonly Node _root = new();

    public void Insert(string prefix, TValue value)
    {
        var node = _root;
        foreach (var ch in prefix)
        {
            if (!node.Children.TryGetValue(ch, out var child))
            {
                child = new Node();
                node.Children[ch] = child;
            }
            node = child;
        }
        node.Value = value;
        node.HasValue = true;
    }

    /// <summary>
    /// Returns all values stored at nodes that are prefixes of <paramref name="input"/>.
    /// E.g. if "7916" and "79" are inserted and input is "79161234567",
    /// both values are returned.
    /// </summary>
    public List<TValue> GetAllPrefixMatches(string input)
    {
        var results = new List<TValue>(4);
        var node = _root;

        if (node.HasValue)
            results.Add(node.Value!);

        foreach (var ch in input)
        {
            if (!node.Children.TryGetValue(ch, out var child))
                break;

            node = child;
            if (node.HasValue)
                results.Add(node.Value!);
        }

        return results;
    }
}
