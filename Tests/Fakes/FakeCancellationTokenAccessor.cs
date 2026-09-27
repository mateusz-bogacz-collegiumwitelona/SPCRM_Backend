using Services.Accessors;

namespace Tests.Fakes
{
    internal class FakeCancellationTokenAccessor : ICancellationTokenAccessor
    {
        public CancellationToken Token => CancellationToken.None;
    }
}
