using Domain.Common;
using Domain.Enum;

namespace Domain
{
    public class Tasks : BaseEntity
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime DueAt { get; set; }

        public Guid AssignedToId { get; set; }
        public ApplicationUser AssignedTo { get; set; } = null!;

        public Guid? ContactId { get; set; }
        public Contact Contact { get; set; }

        public Guid? DealId { get; set; }
        public Deal Deal { get; set; }


        public TaskStatusEnum Status { get; set; }
        public TaskPriorityEnum Priority { get; set; }

    }
}
