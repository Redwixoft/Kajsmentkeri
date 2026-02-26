using Kajsmentkeri.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kajsmentkeri.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Championship> Championships => Set<Championship>();
    public DbSet<ChampionshipScoringRules> ChampionshipScoringRules => Set<ChampionshipScoringRules>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<Prediction> Predictions => Set<Prediction>();
    public DbSet<PredictionAuditLog> PredictionAuditLogs => Set<PredictionAuditLog>();
    public DbSet<ChampionshipWinnerPrediction> ChampionshipWinnerPredictions => Set<ChampionshipWinnerPrediction>();
    public DbSet<PercentagePrediction> PercentagePredictions => Set<PercentagePrediction>();
    public DbSet<ChampionshipParticipation> ChampionshipParticipations => Set<ChampionshipParticipation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // FK between Championship → ScoringRules (1:1)
        modelBuilder.Entity<Championship>()
            .HasOne(c => c.ScoringRules)
            .WithOne(sr => sr.Championship)
            .HasForeignKey<ChampionshipScoringRules>(sr => sr.ChampionshipId)
            .OnDelete(DeleteBehavior.Restrict);

        // Championship → Match (1:N)
        modelBuilder.Entity<Match>()
            .HasOne(m => m.Championship)
            .WithMany(c => c.Matches)
            .HasForeignKey(m => m.ChampionshipId)
            .OnDelete(DeleteBehavior.Restrict);

        // Prediction → Match (many predictions per match)
        modelBuilder.Entity<Prediction>()
            .HasOne(p => p.Match)
            .WithMany(m => m.Predictions)
            .HasForeignKey(p => p.MatchId)
            .OnDelete(DeleteBehavior.Restrict);

        // No AppUser nav – but we still reference UserId
        // So enforce one prediction per user per match
        modelBuilder.Entity<Prediction>()
            .HasIndex(p => new { p.UserId, p.MatchId })
            .IsUnique();

        // ChampionshipWinnerPrediction
        modelBuilder.Entity<ChampionshipWinnerPrediction>()
            .HasOne(p => p.Championship)
            .WithMany()
            .HasForeignKey(p => p.ChampionshipId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ChampionshipWinnerPrediction>()
            .HasIndex(p => new { p.UserId, p.ChampionshipId })
            .IsUnique();

        // PercentagePrediction
        modelBuilder.Entity<PercentagePrediction>()
            .HasIndex(p => p.UserId)
            .IsUnique();

        // ChampionshipParticipation
        modelBuilder.Entity<ChampionshipParticipation>()
            .HasOne(p => p.Championship)
            .WithMany()
            .HasForeignKey(p => p.ChampionshipId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ChampionshipParticipation>()
            .HasIndex(p => new { p.UserId, p.ChampionshipId })
            .IsUnique();

        // PredictionAuditLog — queried by MatchId in audit log lookups
        modelBuilder.Entity<PredictionAuditLog>()
            .HasIndex(p => p.MatchId);
    }
}
