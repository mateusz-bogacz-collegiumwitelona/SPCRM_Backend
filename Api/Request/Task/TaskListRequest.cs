using Api.Request.List;

namespace Api.Request.Task
{
    public record TaskListRequest : BasicListRequest
    {
        public string? Status { get; init; }
        public string? Priority { get; init; }
    }
}
