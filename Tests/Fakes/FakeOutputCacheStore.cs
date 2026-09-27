using Microsoft.AspNetCore.OutputCaching;

namespace Tests.Fakes
{
    public class FakeOutputCacheStore : IOutputCacheStore
    {
        public List<string> EvictedTags { get; } = new();

        public ValueTask EvictByTagAsync(string tag, CancellationToken cancellationToken)
        {
            EvictedTags.Add(tag);
            return ValueTask.CompletedTask;
        }

        public ValueTask<byte[]?> GetAsync(string key, CancellationToken cancellationToken)
            => ValueTask.FromResult<byte[]?>(null);

        public ValueTask SetAsync(string key, byte[] value, string[]? tags, TimeSpan validFor, CancellationToken cancellationToken)
            => ValueTask.CompletedTask;
    }
}
