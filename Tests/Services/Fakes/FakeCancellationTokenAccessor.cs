using Services.Accessors;

namespace Tests.Services.Fakes
{
    internal class FakeCancellationTokenAccessor : ICancellationTokenAccessor
    {
        public CancellationToken Token => CancellationToken.None;
    }
}
