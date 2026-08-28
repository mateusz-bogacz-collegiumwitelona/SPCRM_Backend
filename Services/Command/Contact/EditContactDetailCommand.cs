namespace Services.Command.Contact
{
    public record EditContactDetailCommand
    {
        public Guid? ContactDetailId { get; set; }
        public string? Label { get; init; }
        public string? Value { get; init; }
        public bool? IsPrimary { get; set; }
        public string? Type { get; set; }
    }
}
