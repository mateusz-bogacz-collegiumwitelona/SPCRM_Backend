namespace Api.Request.Task
{
    public class ChangeTaskStatusRequest
    {
        public required Guid TaskId { get; init; }
        public required string Status { get; init; }
    }
}
