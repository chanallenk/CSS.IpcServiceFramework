using System;
using System.Threading;
using System.Threading.Tasks;

namespace CSS.IpcServiceFramework.Hosting
{
    public interface IIpcEndpoint: IDisposable
    {
        Task ExecuteAsync(CancellationToken stoppingToken);
    }
}
