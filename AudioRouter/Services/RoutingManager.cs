using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AudioRouter.Models;

namespace AudioRouter.Services
{
    public class RoutingManager : IDisposable
    {
        private readonly Dictionary<Guid, AudioRouterEngine> _activeRoutes;
        private readonly AudioDeviceEnumerator _deviceEnumerator;

        public event EventHandler<AudioRoute>? OnRouteStarted;
        public event EventHandler<AudioRoute>? OnRouteStopped;
        public event EventHandler<string>? OnRouteError;

        public RoutingManager()
        {
            _activeRoutes = new Dictionary<Guid, AudioRouterEngine>();
            _deviceEnumerator = new AudioDeviceEnumerator();
        }

        public async Task<bool> StartRouteAsync(AudioRoute route)
        {
            if (_activeRoutes.ContainsKey(route.Id))
            {
                return false; // Route already active
            }

            try
            {
                var engine = new AudioRouterEngine(route, _deviceEnumerator);

                engine.OnError += (sender, error) =>
                {
                    OnRouteError?.Invoke(this, $"{route}: {error}");
                };

                engine.OnStopped += async (sender, args) =>
                {
                    await StopRouteAsync(route.Id);
                    OnRouteStopped?.Invoke(this, route);
                };

                _activeRoutes[route.Id] = engine;
                await engine.StartAsync();

                OnRouteStarted?.Invoke(this, route);
                return true;
            }
            catch (Exception ex)
            {
                OnRouteError?.Invoke(this, $"Failed to start route {route}: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> StopRouteAsync(Guid routeId)
        {
            if (!_activeRoutes.TryGetValue(routeId, out var engine))
            {
                return false;
            }

            try
            {
                await engine.StopAsync();
                engine.Dispose();
                _activeRoutes.Remove(routeId);
                return true;
            }
            catch (Exception ex)
            {
                OnRouteError?.Invoke(this, $"Error stopping route: {ex.Message}");
                return false;
            }
        }

        public async Task StopAllRoutesAsync()
        {
            var routeIds = _activeRoutes.Keys.ToList();
            foreach (var routeId in routeIds)
            {
                await StopRouteAsync(routeId);
            }
        }

        public List<AudioRoute> GetActiveRoutes()
        {
            return _activeRoutes.Values
                .Select(e => e.GetType().GetField("_route",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance)?
                    .GetValue(e) as AudioRoute)
                .Where(r => r != null)
                .Cast<AudioRoute>()
                .ToList();
        }

        public bool IsRouteActive(Guid routeId)
        {
            return _activeRoutes.ContainsKey(routeId);
        }

        public void Dispose()
        {
            Task.Run(async () => await StopAllRoutesAsync()).Wait();
            _deviceEnumerator?.Dispose();
        }
    }
}
