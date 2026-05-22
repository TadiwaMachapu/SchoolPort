namespace SchoolPortal.Data.Entities;

public class Subscription
{
    public Guid SubscriptionId { get; set; }
    public Guid SchoolId { get; set; }
    public string Plan { get; set; } = null!; // Basic, Pro, Enterprise
    public string Status { get; set; } = null!; // Active, Canceled, PastDue, Trialing
    public string? StripeCustomerId { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public DateTime? TrialEndsAt { get; set; }
    public DateTime? CurrentPeriodEnd { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public virtual School School { get; set; } = null!;
}
