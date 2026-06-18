using Domain.Common;
using Domain.Enum;

namespace Domain.Models
{
    public class Tasks : BaseEntity
    {
        public required string Title { get; set; }
        public required string Description { get; set; }
        public DateTime DueAt { get; set; }

        public Guid AssignedToId { get; set; }
        public ApplicationUser AssignedTo { get; set; } = null!;

        public Guid? ContactId { get; set; }
        public Contact? Contact { get; set; }

        public Guid? DealId { get; set; }
        public  Deal? Deal { get; set; }


        public TaskStatusEnum Status { get; set; }
        public TaskPriorityEnum Priority { get; set; }

        public ICollection<TaskNote> Notes { get; set; } = new List<TaskNote>();
    }
}
