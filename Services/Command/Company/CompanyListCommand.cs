using Services.Command.List;

namespace Services.Command.Company
{
    public record CompanyListCommand : BasicListCommand
    {
        public required Guid UserId { get; init; }
        public bool? IsYour { get; init; }
        public DateTime? CreatedAtFrom { get; init; }
        public DateTime? CreatedAtTo { get; init; }
    }
}
