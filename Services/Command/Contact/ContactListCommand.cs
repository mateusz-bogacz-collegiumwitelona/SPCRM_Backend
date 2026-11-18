using Services.Command.List;

namespace Services.Command.Contact
{
    public record ContactListCommand : BasicListCommand
    {
        public string? ComapnyName { get; init; }
        public bool? IsPrimary { get; init; }
        public Guid? OwnerId { get; init; }
    }
}
