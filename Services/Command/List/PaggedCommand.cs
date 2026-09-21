namespace Services.Command.List
{
    public record PaggedCommand
    {
        public int? PageNumber { get; init; }
        public int? PageSize { get; init; }
    }
}
