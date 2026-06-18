using Domain.Common;

namespace Domain.Models
{
    public class TaskNote : Note
    {
        public Guid TaskId { get; set; }
        public Tasks Task { get; set; } = null!;
    }
}
