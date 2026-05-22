namespace SchoolPortal.Data.Entities;

public class MessageThread
{
    public Guid ThreadId { get; set; }
    public Guid SchoolId { get; set; }
    public string? Subject { get; set; }
    public string Type { get; set; } = null!; // Direct, ClassDiscussion
    public Guid? ClassId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastMessageAt { get; set; }

    public virtual School School { get; set; } = null!;
    public virtual Class? Class { get; set; }
    public virtual ICollection<ThreadParticipant> Participants { get; set; } = new List<ThreadParticipant>();
    public virtual ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}

public class ThreadParticipant
{
    public Guid ParticipantId { get; set; }
    public Guid ThreadId { get; set; }
    public Guid UserId { get; set; }
    public DateTime JoinedAt { get; set; }
    public DateTime? LastReadAt { get; set; }

    public virtual MessageThread Thread { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}

public class ChatMessage
{
    public Guid MessageId { get; set; }
    public Guid ThreadId { get; set; }
    public Guid SenderUserId { get; set; }
    public string Content { get; set; } = null!;
    public DateTime SentAt { get; set; }
    public bool IsDeleted { get; set; }

    public virtual MessageThread Thread { get; set; } = null!;
    public virtual User Sender { get; set; } = null!;
}
