using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SoftwarePioniere.EventStore;

namespace SoftwarePioniere.DomainModel.Services.EventStore
{
    public class ProjectionReader : IProjectionReader
    {
        private readonly ILogger _logger;
        private readonly EventStoreConnectionProvider _provider;

        public ProjectionReader(EventStoreConnectionProvider provider, ILoggerFactory loggerFactory)
        {
            if (loggerFactory == null) throw new ArgumentNullException(nameof(loggerFactory));
            _logger = loggerFactory.CreateLogger(GetType());
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));

        }

        public async Task<T> GetStateAsync<T>(string name, string partitionId = null)
        {
            _logger.LogDebug("GetStateAsync {Type} {ProjectionName} {PartitionId}", typeof(T).Name, name, partitionId);

            var json = await LoadJsonAsync(name, partitionId);
            return JsonConvert.DeserializeObject<T>(json);
        }

        public async Task<T> GetStateAsyncAnonymousType<T>(string name, T anonymousTypeObject, string partitionId = null)
        {
            _logger.LogDebug("GetStateAsyncAnonymousType {Type} {ProjectionName} {PartitionId}", typeof(T).Name, name, partitionId);

            var json = await LoadJsonAsync(name, partitionId);
            return JsonConvert.DeserializeAnonymousType(json, anonymousTypeObject);
        }

        private async Task<string> LoadJsonAsync(string name, string partitionId)
        {
            var manager = _provider.CreateProjectionsManager();

            string json;

            if (string.IsNullOrEmpty(partitionId))
            {
                json = await manager.GetStateAsync(name, _provider.OpsCredentials);
            }
            else
            {
                json = await manager.GetPartitionStateAsync(name, partitionId, _provider.OpsCredentials);
            }

            _logger.LogTrace("State {StateJson}", json);
            return json;
        }
    }
}
