using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Services;

namespace SchoolPortal.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FeesController : ControllerBase
{
    private readonly SchoolPortalDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public FeesController(SchoolPortalDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetFees()
    {
        var fees = await _context.Fees
            .AsNoTracking()
            .Where(f => f.SchoolId == _currentUser.SchoolId)
            .Include(f => f.Payments)
            .Include(f => f.Term).ThenInclude(t => t!.AcademicYear)
            .OrderBy(f => f.DueDate)
            .Select(f => new
            {
                f.FeeId,
                f.Name,
                f.Description,
                f.AmountZar,
                f.DueDate,
                TermLabel = f.Term != null ? $"Term {f.Term.TermNumber} {f.Term.AcademicYear.Year}" : null,
                TotalCollected = f.Payments.Sum(p => p.AmountPaidZar),
                PaymentCount = f.Payments.Count
            })
            .ToListAsync();

        return Ok(fees);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateFee([FromBody] FeeRequest request)
    {
        var fee = new Fee
        {
            SchoolId = _currentUser.SchoolId,
            Name = request.Name,
            Description = request.Description,
            AmountZar = request.AmountZar,
            DueDate = request.DueDate,
            TermId = request.TermId,
            CreatedAt = DateTime.UtcNow
        };
        _context.Fees.Add(fee);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetFee), new { id = fee.FeeId }, new { fee.FeeId, fee.Name, fee.AmountZar, fee.DueDate });
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetFee(Guid id)
    {
        var fee = await _context.Fees
            .AsNoTracking()
            .Where(f => f.FeeId == id && f.SchoolId == _currentUser.SchoolId)
            .FirstOrDefaultAsync();

        if (fee == null) return NotFound();
        return Ok(fee);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateFee(Guid id, [FromBody] FeeRequest request)
    {
        var fee = await _context.Fees.FirstOrDefaultAsync(f => f.FeeId == id && f.SchoolId == _currentUser.SchoolId);
        if (fee == null) return NotFound();

        fee.Name = request.Name;
        fee.Description = request.Description;
        fee.AmountZar = request.AmountZar;
        fee.DueDate = request.DueDate;
        fee.TermId = request.TermId;
        fee.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(new { fee.FeeId, fee.Name, fee.AmountZar, fee.DueDate });
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteFee(Guid id)
    {
        var fee = await _context.Fees.FirstOrDefaultAsync(f => f.FeeId == id && f.SchoolId == _currentUser.SchoolId);
        if (fee == null) return NotFound();
        _context.Fees.Remove(fee);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id}/payments")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetPayments(Guid id)
    {
        var payments = await _context.FeePayments
            .AsNoTracking()
            .Where(p => p.FeeId == id && p.SchoolId == _currentUser.SchoolId)
            .Include(p => p.Student).ThenInclude(s => s.User)
            .Include(p => p.RecordedByUser)
            .OrderByDescending(p => p.PaidAt)
            .Select(p => new
            {
                p.FeePaymentId,
                p.FeeId,
                p.AmountPaidZar,
                p.PaidAt,
                p.Notes,
                StudentName = $"{p.Student.User.FirstName} {p.Student.User.LastName}",
                p.Student.StudentNumber,
                RecordedBy = $"{p.RecordedByUser.FirstName} {p.RecordedByUser.LastName}"
            })
            .ToListAsync();

        return Ok(payments);
    }

    [HttpPost("{id}/payments")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RecordPayment(Guid id, [FromBody] RecordPaymentRequest request)
    {
        var fee = await _context.Fees
            .FirstOrDefaultAsync(f => f.FeeId == id && f.SchoolId == _currentUser.SchoolId);
        if (fee == null) return NotFound();

        var payment = new FeePayment
        {
            FeeId = id,
            StudentId = request.StudentId,
            SchoolId = _currentUser.SchoolId,
            AmountPaidZar = request.AmountPaidZar,
            PaidAt = request.PaidAt ?? DateTime.UtcNow,
            RecordedByUserId = _currentUser.UserId,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        };
        _context.FeePayments.Add(payment);
        await _context.SaveChangesAsync();
        return Ok(new { payment.FeePaymentId, payment.AmountPaidZar, payment.PaidAt });
    }

    // Student/Parent: view their own outstanding and paid fees
    [HttpGet("my-statement")]
    [Authorize(Roles = "Student,Parent")]
    public async Task<IActionResult> GetMyStatement()
    {
        var schoolId = _currentUser.SchoolId;
        Guid? studentId = null;

        if (_currentUser.Role == "Student")
        {
            studentId = await _context.Students
                .Where(s => s.UserId == _currentUser.UserId)
                .Select(s => (Guid?)s.StudentId)
                .FirstOrDefaultAsync();
        }
        else if (_currentUser.Role == "Parent")
        {
            studentId = await _context.Students
                .Where(s => s.ParentUserId == _currentUser.UserId && s.SchoolId == schoolId)
                .Select(s => (Guid?)s.StudentId)
                .FirstOrDefaultAsync();
        }

        if (studentId == null) return NotFound("Learner record not found");

        var fees = await _context.Fees
            .AsNoTracking()
            .Where(f => f.SchoolId == schoolId)
            .OrderBy(f => f.DueDate)
            .Select(f => new
            {
                f.FeeId,
                f.Name,
                f.Description,
                f.AmountZar,
                f.DueDate,
                AmountPaid = f.Payments
                    .Where(p => p.StudentId == studentId.Value)
                    .Sum(p => p.AmountPaidZar)
            })
            .ToListAsync();

        var statement = fees.Select(f => new
        {
            f.FeeId,
            f.Name,
            f.Description,
            f.AmountZar,
            f.DueDate,
            f.AmountPaid,
            Balance = f.AmountZar - f.AmountPaid,
            IsPaid = f.AmountPaid >= f.AmountZar
        });

        return Ok(statement);
    }
}

public record FeeRequest(string Name, string? Description, decimal AmountZar, DateTime DueDate, Guid? TermId);
public record RecordPaymentRequest(Guid StudentId, decimal AmountPaidZar, DateTime? PaidAt, string? Notes);
