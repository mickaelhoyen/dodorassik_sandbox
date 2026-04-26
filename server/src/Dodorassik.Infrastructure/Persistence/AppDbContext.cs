using Dodorassik.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dodorassik.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Family> Families => Set<Family>();
    public DbSet<Hunt> Hunts => Set<Hunt>();
    public DbSet<HuntStep> HuntSteps => Set<HuntStep>();
    public DbSet<Clue> Clues => Set<Clue>();
    public DbSet<StepSubmission> Submissions => Set<StepSubmission>();
    public DbSet<HuntScore> HuntScores => Set<HuntScore>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Email).HasMaxLength(256).IsRequired();
            e.Property(u => u.DisplayName).HasMaxLength(128).IsRequired();
            e.Property(u => u.PasswordHash).HasMaxLength(512).IsRequired();
            e.HasOne(u => u.Family).WithMany(f => f.Members).HasForeignKey(u => u.FamilyId);
        });

        b.Entity<Family>(e =>
        {
            e.Property(f => f.Name).HasMaxLength(128).IsRequired();
        });

        b.Entity<Hunt>(e =>
        {
            e.Property(h => h.Name).HasMaxLength(256).IsRequired();
            e.Property(h => h.Description).HasMaxLength(4000);
            e.Property(h => h.RejectionReason).HasMaxLength(2000);
            e.HasOne(h => h.Creator).WithMany().HasForeignKey(h => h.CreatorId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(h => h.Steps).WithOne(s => s.Hunt!).HasForeignKey(s => s.HuntId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(h => h.Clues).WithOne(c => c.Hunt!).HasForeignKey(c => c.HuntId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(h => h.Status);
        });

        b.Entity<HuntStep>(e =>
        {
            e.Property(s => s.Title).HasMaxLength(256).IsRequired();
            e.Property(s => s.Description).HasMaxLength(4000);
            e.Property(s => s.ParamsJson).HasColumnType("jsonb");
            e.HasIndex(s => new { s.HuntId, s.Order });
        });

        b.Entity<Clue>(e =>
        {
            e.Property(c => c.Code).HasMaxLength(64).IsRequired();
            e.Property(c => c.Title).HasMaxLength(256);
            e.HasIndex(c => new { c.HuntId, c.Code }).IsUnique();
        });

        b.Entity<StepSubmission>(e =>
        {
            e.Property(s => s.PayloadJson).HasColumnType("jsonb");
            e.HasOne(s => s.Step).WithMany().HasForeignKey(s => s.HuntStepId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(s => s.Family).WithMany().HasForeignKey(s => s.FamilyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(s => s.SubmittedBy).WithMany().HasForeignKey(s => s.SubmittedById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(s => s.Team).WithMany().HasForeignKey(s => s.TeamId).OnDelete(DeleteBehavior.SetNull).IsRequired(false);
            e.HasIndex(s => new { s.HuntStepId, s.FamilyId });
        });

        b.Entity<HuntScore>(e =>
        {
            e.Ignore(s => s.Duration);
            e.HasOne(s => s.Hunt).WithMany(h => h.Scores).HasForeignKey(s => s.HuntId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(s => s.Family).WithMany(f => f.Scores).HasForeignKey(s => s.FamilyId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(s => new { s.HuntId, s.FamilyId }).IsUnique();
        });

        b.Entity<Team>(e =>
        {
            e.Property(t => t.Name).HasMaxLength(64).IsRequired();
            e.Property(t => t.Color).HasMaxLength(7); // #RRGGBB
            e.HasOne(t => t.Hunt).WithMany().HasForeignKey(t => t.HuntId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(t => t.Family).WithMany().HasForeignKey(t => t.FamilyId).OnDelete(DeleteBehavior.Cascade);
            // Multiple teams per family per hunt are allowed (e.g. "Garçons" vs "Filles").
            e.HasIndex(t => t.HuntId);
        });

        b.Entity<TeamMember>(e =>
        {
            e.HasKey(m => new { m.TeamId, m.UserId });
            e.HasOne(m => m.Team).WithMany(t => t.Members).HasForeignKey(m => m.TeamId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(m => m.User).WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
