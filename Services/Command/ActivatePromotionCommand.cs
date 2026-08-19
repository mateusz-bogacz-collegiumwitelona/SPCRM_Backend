namespace Services.Command
{
    public record ActivatePromotionCommand
    {
        public required Guid Id { get; init; }
        public required DateTime EndDate { get; init; }
    }
}
