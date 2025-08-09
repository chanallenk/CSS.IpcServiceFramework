using System;

namespace CSS.IpcServiceFramework.Hosting
{
    public interface IIpcHostBuilder
    {
        IIpcHostBuilder AddIpcEndpoint(Func<IServiceProvider, IIpcEndpoint> factory);
    }
}
