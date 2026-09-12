namespace Services.Command.Task
{
    public record ExtendTaskDueDateCommand
    {
        public required Guid TaskId { get; init; }
        public required Guid UserId { get; init; }
        public required DateTime NewDueDate { get; init; }
    }
}
