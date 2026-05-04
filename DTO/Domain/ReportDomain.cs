namespace DTO.Domain
{
    public record ReportDomain
    {
        public string SupportEmail { get; init; }
        public string UserName { get; init; }
        public string UserSurname { get; init; }
        public string UserEmail { get; init; }
        public string Time { get; init; }
        public string Title { get; init; }
        public string Message { get; init; }
    }
}
