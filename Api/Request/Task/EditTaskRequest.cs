namespace Api.Request.Task
{
    public record EditTaskRequest
    {
        public string? Title { get; init; }
        public string? Description { get; init; }
    }
}
