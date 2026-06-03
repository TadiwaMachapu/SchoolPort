namespace SchoolPortal.Data.Entities;

public class SeniorPhaseRequirement
{
    public Guid SeniorPhaseRequirementId { get; set; }
    public string FetSubjectName { get; set; } = null!;
    public string RequiredSeniorPhaseSubjectName { get; set; } = null!;
    public int? RecommendedMinPercent { get; set; }
    public string? Notes { get; set; }
}
