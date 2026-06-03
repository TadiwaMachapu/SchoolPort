namespace SchoolPortal.Data.Entities;

public class FeePayment
{
    public Guid FeePaymentId { get; set; }
    public Guid FeeId { get; set; }
    public Guid StudentId { get; set; }
    public Guid SchoolId { get; set; }
    public decimal AmountPaidZar { get; set; }
    public DateTime PaidAt { get; set; }
    public Guid RecordedByUserId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual Fee Fee { get; set; } = null!;
    public virtual Student Student { get; set; } = null!;
    public virtual User RecordedByUser { get; set; } = null!;
    public virtual School School { get; set; } = null!;
}
