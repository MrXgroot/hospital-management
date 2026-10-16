using HMS.Domain.Patients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HMS.Infrastructure.Persistence.Configurations;

public sealed class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> b)
    {
        b.ToTable(t => t.HasCheckConstraint("CK_patients_age", "`date_of_birth` IS NOT NULL OR `age_years` IS NOT NULL"));
        b.Property(x => x.Mrn).HasMaxLength(12);
        b.Property(x => x.FirstName).HasMaxLength(80);
        b.Property(x => x.LastName).HasMaxLength(80);
        b.Property(x => x.BloodGroup).HasMaxLength(5);
        b.Property(x => x.Phone).HasMaxLength(16);
        b.Property(x => x.AlternatePhone).HasMaxLength(16);
        b.Property(x => x.Email).HasMaxLength(150);
        b.Property(x => x.AddressLine1).HasMaxLength(200);
        b.Property(x => x.AddressLine2).HasMaxLength(200);
        b.Property(x => x.City).HasMaxLength(80);
        b.Property(x => x.State).HasMaxLength(80);
        b.Property(x => x.PostalCode).HasMaxLength(12);
        b.Property(x => x.Country).HasMaxLength(60);
        b.Property(x => x.EmergencyContactName).HasMaxLength(150);
        b.Property(x => x.EmergencyContactPhone).HasMaxLength(16);
        b.Property(x => x.EmergencyContactRelation).HasMaxLength(40);
        b.Ignore(x => x.FullName);
        b.HasIndex(x => x.Mrn).IsUnique().HasDatabaseName("UX_patients_mrn");
        b.HasIndex(x => x.Phone).HasDatabaseName("IX_patients_phone");
        b.HasIndex(x => new { x.LastName, x.FirstName }).HasDatabaseName("IX_patients_last_name_first_name");
        b.HasIndex(x => x.FirstName).HasDatabaseName("IX_patients_first_name");
        b.HasMany(x => x.Allergies).WithOne().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Conditions).WithOne().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public sealed class PatientAllergyConfiguration : IEntityTypeConfiguration<PatientAllergy>
{
    public void Configure(EntityTypeBuilder<PatientAllergy> b)
    {
        b.Property(x => x.Allergen).HasMaxLength(100);
        b.Property(x => x.Reaction).HasMaxLength(200);
        b.Property(x => x.Severity).HasMaxLength(20);
    }
}

public sealed class PatientConditionConfiguration : IEntityTypeConfiguration<PatientCondition>
{
    public void Configure(EntityTypeBuilder<PatientCondition> b)
    {
        b.Property(x => x.ConditionName).HasMaxLength(150);
        b.Property(x => x.Notes).HasMaxLength(500);
    }
}
