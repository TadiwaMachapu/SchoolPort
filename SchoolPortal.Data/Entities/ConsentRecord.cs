namespace SchoolPortal.Data.Entities;

public class ConsentRecord
{
    public Guid ConsentRecordId { get; set; }
    public Guid SchoolId { get; set; }
    public Guid UserId { get; set; }
    public bool DataProcessing { get; set; }
    public bool MarketingCommunications { get; set; }
    public bool ThirdPartySharing { get; set; }
    public bool Photography { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual School School { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}
