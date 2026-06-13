using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Authorization;
using SchoolPortal.Server.Services;
using Stripe;
using Stripe.Checkout;

namespace SchoolPortal.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BillingController : ControllerBase
{
    private readonly SchoolPortalDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IConfiguration _config;

    // Stripe price IDs — set these in your Stripe dashboard and put in appsettings
    private static readonly Dictionary<string, string> PriceLookup = new()
    {
        ["Basic"] = "price_basic_monthly",
        ["Pro"] = "price_pro_monthly",
        ["Enterprise"] = "price_enterprise_monthly"
    };

    public BillingController(SchoolPortalDbContext context, ICurrentUserService currentUser, IConfiguration config)
    {
        _context = context;
        _currentUser = currentUser;
        _config = config;
        StripeConfiguration.ApiKey = config["Stripe:SecretKey"] ?? "CHANGE_ME";
    }

    [HttpGet("subscription")]
    [Authorize]
    public async Task<IActionResult> GetSubscription()
    {
        var sub = await _context.Subscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SchoolId == _currentUser.SchoolId);

        if (sub == null)
            return Ok(new { Plan = "None", Status = "NoSubscription" });

        return Ok(new
        {
            sub.Plan,
            sub.Status,
            sub.TrialEndsAt,
            sub.CurrentPeriodEnd,
            sub.CreatedAt
        });
    }

    [HttpPost("checkout")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateCheckoutSession([FromBody] CreateCheckoutRequest request)
    {
        if (!PriceLookup.ContainsKey(request.Plan))
            return BadRequest("Invalid plan");

        var school = await _context.Schools.FindAsync(_currentUser.SchoolId);
        if (school == null) return NotFound();

        var frontendUrl = _config["FrontendUrl"] ?? "http://localhost:3000";

        var options = new SessionCreateOptions
        {
            Mode = "subscription",
            PaymentMethodTypes = new List<string> { "card" },
            LineItems = new List<SessionLineItemOptions>
            {
                new() { Price = _config[$"Stripe:Prices:{request.Plan}"] ?? PriceLookup[request.Plan], Quantity = 1 }
            },
            SuccessUrl = $"{frontendUrl}/settings?billing=success",
            CancelUrl = $"{frontendUrl}/settings?billing=cancelled",
            Metadata = new Dictionary<string, string>
            {
                ["schoolId"] = _currentUser.SchoolId.ToString(),
                ["plan"] = request.Plan
            },
            SubscriptionData = new SessionSubscriptionDataOptions
            {
                TrialPeriodDays = 14,
                Metadata = new Dictionary<string, string>
                {
                    ["schoolId"] = _currentUser.SchoolId.ToString(),
                    ["plan"] = request.Plan
                }
            }
        };

        var service = new SessionService();
        var session = await service.CreateAsync(options);

        return Ok(new { Url = session.Url, SessionId = session.Id });
    }

    [HttpPost("portal")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreatePortalSession()
    {
        var sub = await _context.Subscriptions
            .FirstOrDefaultAsync(s => s.SchoolId == _currentUser.SchoolId);

        if (sub?.StripeCustomerId == null)
            return BadRequest("No active subscription");

        var frontendUrl = _config["FrontendUrl"] ?? "http://localhost:3000";
        var options = new Stripe.BillingPortal.SessionCreateOptions
        {
            Customer = sub.StripeCustomerId,
            ReturnUrl = $"{frontendUrl}/settings"
        };

        var service = new Stripe.BillingPortal.SessionService();
        var session = await service.CreateAsync(options);

        return Ok(new { Url = session.Url });
    }

    // Stripe webhook handler
    [HttpPost("webhook")]
    [AllowAnonymous]
    [AnonymousJustification("Stripe billing webhook: invoked server-to-server by Stripe with no user JWT; authenticated instead by Stripe signature verification.")]
    public async Task<IActionResult> Webhook()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        var webhookSecret = _config["Stripe:WebhookSecret"] ?? "";

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(json,
                Request.Headers["Stripe-Signature"], webhookSecret);
        }
        catch
        {
            return BadRequest("Invalid signature");
        }

        switch (stripeEvent.Type)
        {
            case "customer.subscription.created":
            case "customer.subscription.updated":
                if (stripeEvent.Data.Object is Stripe.Subscription stripeSub)
                    await UpsertSubscription(stripeSub);
                break;

            case "customer.subscription.deleted":
                if (stripeEvent.Data.Object is Stripe.Subscription deletedSub)
                    await CancelSubscription(deletedSub);
                break;
        }

        return Ok();
    }

    private async Task UpsertSubscription(Stripe.Subscription stripeSub)
    {
        if (!stripeSub.Metadata.TryGetValue("schoolId", out var schoolIdStr)) return;
        if (!Guid.TryParse(schoolIdStr, out var schoolId)) return;
        var plan = stripeSub.Metadata.GetValueOrDefault("plan", "Basic")!;

        var sub = await _context.Subscriptions.FirstOrDefaultAsync(s => s.SchoolId == schoolId);
        if (sub == null)
        {
            sub = new Data.Entities.Subscription { SchoolId = schoolId, CreatedAt = DateTime.UtcNow };
            _context.Subscriptions.Add(sub);
        }

        sub.Plan = plan;
        sub.Status = stripeSub.Status;
        sub.StripeCustomerId = stripeSub.CustomerId;
        sub.StripeSubscriptionId = stripeSub.Id;
        sub.CurrentPeriodEnd = stripeSub.CurrentPeriodEnd;
        sub.TrialEndsAt = stripeSub.TrialEnd;
        sub.UpdatedAt = DateTime.UtcNow;

        // Update school features based on plan
        var school = await _context.Schools.FindAsync(schoolId);
        if (school != null)
        {
            school.Features.SmartReports = plan is "Pro" or "Enterprise";
            school.Features.Gradebook = plan is "Pro" or "Enterprise";
            school.Features.SchoolPay = plan is "Enterprise";
            school.Features.VirtualClassroom = plan is "Enterprise";
            school.Features.SkillsProfile = plan is "Enterprise";
            school.Features.SchoolChat = plan is "Enterprise";
        }

        await _context.SaveChangesAsync();
    }

    private async Task CancelSubscription(Stripe.Subscription stripeSub)
    {
        var sub = await _context.Subscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSub.Id);

        if (sub != null)
        {
            sub.Status = "Canceled";
            sub.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
}

public record CreateCheckoutRequest(string Plan);
