using FluentValidation;
using SchoolPortal.Shared.DTOs.Announcements;

namespace SchoolPortal.Server.Validators;

public class CreateAnnouncementValidator : AbstractValidator<CreateAnnouncementRequest>
{
    public CreateAnnouncementValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Content is required");

        RuleFor(x => x.Audience)
            .NotEmpty().WithMessage("Audience is required")
            .Must(x => new[] { "All", "Grade", "Class" }.Contains(x))
            .WithMessage("Audience must be 'All', 'Grade', or 'Class'");

        RuleFor(x => x.AudienceValue)
            .NotEmpty().When(x => x.Audience != "All")
            .WithMessage("AudienceValue is required when Audience is not 'All'");
    }
}
