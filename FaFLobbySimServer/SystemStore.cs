using System.Diagnostics.CodeAnalysis;

namespace FaFLobbySimServer;

internal static class SystemStore
{
    private static readonly object Lock = new object();
    private static readonly Dictionary<string, List<OccupancyRecord>> Store
        = new Dictionary<string, List<OccupancyRecord>>(StringComparer.OrdinalIgnoreCase);

    public static void StoreLatest(string identifier, int occupied, int total)
    {
        if (occupied > total || total <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(occupied),$"Invalid occupied or total values ({occupied}, {total})");
        }

        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new ArgumentException("Invalid identifier", nameof(identifier));
        }

        lock (Lock)
        {
            if (!Store.TryGetValue(identifier, out var list))
            {
                list = new List<OccupancyRecord>();
                Store[identifier] = list;
            }

            Console.WriteLine("Storing occupancy record: " + identifier + " " + occupied + " of " + total);

            list.Add(new OccupancyRecord(occupied, total, DateTime.UtcNow));
        }
    }

    public static bool TryGetOccupancyRecords(string identifier, [NotNullWhen(true)] out IReadOnlyList<OccupancyRecord>? records)
    {
        records = null;
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return false;
        }

        lock (Lock)
        {
            if (!Store.TryGetValue(identifier, out var recordsA))
            {
                return false;
            }

            records = recordsA;
        }

        return true;
    }
}

public record OccupancyRecord(int Occupied, int Total, DateTime Added);