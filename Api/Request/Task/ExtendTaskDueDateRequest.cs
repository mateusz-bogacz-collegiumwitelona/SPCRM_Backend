namespace Api.Request.Task
{
    public record ExtendTaskDueDateRequest
    {
        public required DateTime NewDueDate { get; init; }
    }
}
