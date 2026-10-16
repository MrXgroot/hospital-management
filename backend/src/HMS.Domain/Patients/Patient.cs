using HMS.Domain.Common;

namespace HMS.Domain.Patients;

public class Patient : AuditableEntity, ISoftDelete, IAudited
{
    public string Mrn { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public Gender Gender { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public short? AgeYears { get; set; }
    public DateOnly? AgeRecordedOn { get; set; }
    public string? BloodGroup { get; set; }
    public string Phone { get; set; } = "";
    public string? AlternatePhone { get; set; }
    public string? Email { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? EmergencyContactRelation { get; set; }
    public bool IsDeleted { get; set; }

    public List<PatientAllergy> Allergies { get; set; } = [];
    public List<PatientCondition> Conditions { get; set; } = [];

    public string FullName => $"{FirstName} {LastName}";

    public int? AgeOn(DateOnly today) => CalculateAge(DateOfBirth, AgeYears, AgeRecordedOn, today);

    public static int? CalculateAge(DateOnly? dob, short? ageYears, DateOnly? recordedOn, DateOnly today)
    {
        if (dob is { } d)
            return YearsBetween(d, today);
        if (ageYears is { } a)
            return a + (recordedOn is { } r ? YearsBetween(r, today) : 0);
        return null;
    }

    private static int YearsBetween(DateOnly from, DateOnly to)
    {
        var years = to.Year - from.Year;
        if (to < from.AddYears(years)) years--;
        return Math.Max(years, 0);
    }

    /// <summary>Keeps a leading '+' and the digits only.</summary>
    public static string NormalisePhone(string phone)
    {
        var trimmed = phone.Trim();
        var digits = new string(trimmed.Where(char.IsDigit).ToArray());
        return trimmed.StartsWith('+') ? "+" + digits : digits;
    }
}

public class PatientAllergy : AuditableEntity
{
    public long PatientId { get; set; }
    public string Allergen { get; set; } = "";
    public string? Reaction { get; set; }
    public string? Severity { get; set; }
}

public class PatientCondition : AuditableEntity
{
    public long PatientId { get; set; }
    public string ConditionName { get; set; } = "";
    public short? SinceYear { get; set; }
    public string? Notes { get; set; }
}
