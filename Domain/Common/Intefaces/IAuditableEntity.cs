namespace Domain.Common.Intefaces
{
    public interface IAuditableEntity
    {
        DateTime CreatedAt { get; set; }
        DateTime? UpdateAt { get; set; }
    }
}
