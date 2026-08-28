namespace Services.Command.Mailing
{
    public record MailingCommand
    {
        public required List<Guid> To { get; init; }
        public required List<MailingProductCommand> Products { get; init; }
        public required string Language { get; init; }
    }
}
