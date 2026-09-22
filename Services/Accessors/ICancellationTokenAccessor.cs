namespace Services.Accessors
{
    public interface ICancellationTokenAccessor
    {
        CancellationToken Token { get; }
    }
}
