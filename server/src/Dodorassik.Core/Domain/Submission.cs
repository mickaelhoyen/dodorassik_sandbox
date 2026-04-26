namespace Dodorassik.Core.Domain;

/// <summary>
/// Per-step result submitted by a family while running a hunt. In offline
/// mode the client buffers these in <c>OfflineCache</c> and replays them
/// later — the server uses <see cref="ClientCreatedAtUtc"/> for timing so
/// that competitive runs stay fair regardless of when sync happens.
/// </summary>
public class StepSubmission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HuntStepId { get; set; }
    public HuntStep? Step { get; set; }

    public Guid FamilyId { get; set; }
    public Family? Family { get; set; }

    public Guid SubmittedById { get; set; }
    public User? SubmittedBy { get; set; }

    public bool Accepted { get; set; }
    public int AwardedPoints { get; set; }
    public string PayloadJson { get; set; } = "{}";

    /// <summary>
    /// The team that made this submission (competitive mode only; null for relaxed
    /// or pre-Phase-3 submissions).
    /// </summary>
    public Guid? TeamId { get; set; }
    public Team? Team { get; set; }

    public DateTime ClientCreatedAtUtc { get; set; }
    public DateTime ServerReceivedAtUtc { get; set; } = DateTime.UtcNow;
}
