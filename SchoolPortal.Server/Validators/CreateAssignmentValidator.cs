using FluentValidation;
using SchoolPortal.Shared.DTOs.Assignments;

namespace SchoolPortal.Server.Validators;

public class CreateAssignmentValidator : AbstractValidator<CreateAssignmentRequest>
{
    public CreateAssignmentValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters");

        RuleFor(x => x.DueAt)
            .GreaterThan(DateTime.UtcNow).WithMessage("Due date must be in the future");

        RuleFor(x => x.MaxMarks)
            .GreaterThan(0).WithMessage("MaxMarks must be greater than 0");

        RuleFor(x => x.ClassSubjectId)
            .GreaterThan(0).WithMessage("ClassSubjectId is required");
    }
}
