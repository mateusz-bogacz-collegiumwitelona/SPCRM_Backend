namespace Services.Command.List
{
    public record SimpleListCommand : PaggedCommand
    {
        public string? SearchTerm { get; init; }
    }
}
