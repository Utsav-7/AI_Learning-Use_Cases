using Microsoft.EntityFrameworkCore;

namespace SmartAutoFill.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<DocumentEntity> Documents => Set<DocumentEntity>();
    public DbSet<ExtractedFieldEntity> ExtractedFields => Set<ExtractedFieldEntity>();
    public DbSet<AuditLogEntity> AuditLogs => Set<AuditLogEntity>();
    public DbSet<MappingExampleEntity> MappingExamples => Set<MappingExampleEntity>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<ExtractedFieldEntity>()
            .Property(f => f.Confidence)
            .HasColumnType("decimal(5,4)");

        b.Entity<ExtractedFieldEntity>()
            .HasOne(f => f.Document)
            .WithMany(d => d.Fields)
            .HasForeignKey(f => f.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<MappingExampleEntity>()
            .HasIndex(e => e.DocCategory);
    }
}
