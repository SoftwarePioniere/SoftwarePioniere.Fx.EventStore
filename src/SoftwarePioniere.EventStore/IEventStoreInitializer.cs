using System.Threading;
using System.Threading.Tasks;

namespace SoftwarePioniere.EventStore
{
    public interface IEventStoreInitializer
    {
        Task InitializeAsync(CancellationToken cancellationToken = default(CancellationToken));

        int ExecutionOrder { get; }
    }
}
