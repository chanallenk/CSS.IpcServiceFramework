using System.Net;

namespace CSS.IpcServiceFramework.NamedPipeTests.Fixtures
{
    public class UnserializableObject : IPAddress
    {
        public static UnserializableObject Create()
            => new UnserializableObject(Loopback.GetAddressBytes());

        private UnserializableObject(byte[] address)
            : base(address)
        { }
    }
}
