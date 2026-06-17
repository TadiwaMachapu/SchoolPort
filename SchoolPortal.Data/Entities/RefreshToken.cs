namespace SchoolPortal.Data.Entities;

/// <summary>
/// A persisted refresh token (Sprint 1.5.0 Step 5, Option A). Only the SHA-256 hash of the
/// raw token is stored — the raw value is returned to the client once and never kept
/// server-side. On use the token is rotated: the presented row is revoked
/// (<see cref="RevokedAt"/> set, <see cref="ReplacedByTokenId"/> linked to its successor) and
/// a fresh pair is issued. Refresh re-reads the user's positions from the database at refresh
/// time, so a position added or revoked since the previous login propagates into the new
/// access token instead of waiting out the old token's TTL.
/// </summary>
public class RefreshToken
{
    public Guid RefreshTokenId { get; set; }
    public Guid UserId { get; set; }
    public Guid SchoolId { get; set; }            // tenant boundary (denormalised; no FK navigation)

    public string TokenHash { get; set; } = null!; // SHA-256 hex of the raw token, never the raw value

    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }        // set on rotation or logout
    public Guid? ReplacedByTokenId { get; set; }    // rotation chain (audit)

    public virtual User User { get; set; } = null!;
}
