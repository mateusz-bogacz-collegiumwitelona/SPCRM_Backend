using Domain.Common;

namespace Domain.Models
{
    public class DealNote : Note
    {
        public Guid DealId { get; set; }
        public Deal Deal { get; set; } = null!;
    }
}
