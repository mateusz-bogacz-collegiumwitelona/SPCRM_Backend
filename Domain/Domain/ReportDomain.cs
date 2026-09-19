namespace Domain.Comunication
{
    public record ReportDomain
    {
        public required string SupportEmail { get; init; }
        public required string UserName { get; init; }
        public required string UserSurname { get; init; }
        public required string UserEmail { get; init; }
        public required string Time { get; init; }
        public required string Title { get; init; }
        public required string Message { get; init; }
    }
}
