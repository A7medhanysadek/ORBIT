using System.Collections.Concurrent;

namespace OrbitBackend.Services
{
    /// <summary>
    /// Thread-safe in-memory tracker for real-time viewer counts per stream.
    /// Uses SignalR connection tracking — viewers are counted when they join/leave stream groups.
    /// 
    /// Registered as a Singleton so all SignalR hub instances and controllers share the same state.
    /// </summary>
    public class ViewerTracker
    {
        // streamId → set of connectionIds
        private readonly ConcurrentDictionary<int, ConcurrentDictionary<string, byte>> _viewers = new();

        /// <summary>
        /// Adds a viewer (connection) to a stream.
        /// </summary>
        public void AddViewer(int streamId, string connectionId)
        {
            var connections = _viewers.GetOrAdd(streamId, _ => new ConcurrentDictionary<string, byte>());
            connections.TryAdd(connectionId, 0);
        }

        /// <summary>
        /// Removes a viewer (connection) from a stream.
        /// </summary>
        public void RemoveViewer(int streamId, string connectionId)
        {
            if (_viewers.TryGetValue(streamId, out var connections))
            {
                connections.TryRemove(connectionId, out _);

                // Clean up empty entries
                if (connections.IsEmpty)
                    _viewers.TryRemove(streamId, out _);
            }
        }

        /// <summary>
        /// Removes a viewer from all streams (used on disconnect).
        /// </summary>
        public void RemoveViewerFromAll(string connectionId)
        {
            foreach (var kvp in _viewers)
            {
                kvp.Value.TryRemove(connectionId, out _);
                if (kvp.Value.IsEmpty)
                    _viewers.TryRemove(kvp.Key, out _);
            }
        }

        /// <summary>
        /// Gets the current viewer count for a stream.
        /// </summary>
        public int GetViewerCount(int streamId)
        {
            return _viewers.TryGetValue(streamId, out var connections)
                ? connections.Count
                : 0;
        }

        /// <summary>
        /// Gets viewer counts for multiple streams at once.
        /// </summary>
        public Dictionary<int, int> GetViewerCounts(IEnumerable<int> streamIds)
        {
            var result = new Dictionary<int, int>();
            foreach (var id in streamIds)
            {
                result[id] = GetViewerCount(id);
            }
            return result;
        }
    }
}
