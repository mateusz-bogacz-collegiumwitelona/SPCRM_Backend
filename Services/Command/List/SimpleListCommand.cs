namespace Services.Command.List
{
    public record SimpleListCommand
    {
        public int? PageNumber { get; init; }
        public int? PageSize { get; init; }
        public string? SearchTerm { get; init; }
    }
}
