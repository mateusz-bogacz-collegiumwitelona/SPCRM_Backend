namespace Api.Request.Task
{
    public record UserTaskListRequest
    {
        public int? PageNumber { get; init; }
        public int? PageSize { get; init; }
        public string? SearchTerm { get; init; }
        public string? Status { get; init; }
        public string? Priority { get; init; }
        public string? SortBy { get; init; }
        public bool SortDescending { get; init; } = false; 
    }
}
