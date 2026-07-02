using System;
using System.Collections.Concurrent;

namespace Resturant.API.Services
{
    public class DriverLocationInfo
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public static class DriverLocationTracker
    {
        private static readonly ConcurrentDictionary<int, DriverLocationInfo> _locations = new();

        public static void UpdateLocation(int orderId, double latitude, double longitude)
        {
            _locations[orderId] = new DriverLocationInfo
            {
                Latitude = latitude,
                Longitude = longitude,
                UpdatedAt = DateTime.UtcNow
            };
        }

        public static DriverLocationInfo? GetLocation(int orderId)
        {
            if (_locations.TryGetValue(orderId, out var loc))
            {
                // Expire cached coordinates after 15 minutes of inactivity
                if (DateTime.UtcNow - loc.UpdatedAt < TimeSpan.FromMinutes(15))
                {
                    return loc;
                }
                _locations.TryRemove(orderId, out _);
            }
            return null;
        }

        public static void ClearLocation(int orderId)
        {
            _locations.TryRemove(orderId, out _);
        }
    }
}
