namespace SchoolPortal.Data.Entities;

public class Fee
{
    public Guid FeeId { get; set; }
    public Guid SchoolId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public decimal AmountZar { get; set; }
    public DateTime DueDate { get; set; }
    public Guid? TermId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public virtual School School { get; set; } = null!;
    public virtual Term? Term { get; set; }
    public virtual ICollection<FeePayment> Payments { get; set; } = new List<FeePayment>();
}
