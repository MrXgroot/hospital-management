namespace HMS.Domain.Common;

public enum Gender { Male, Female, Other, Unknown }

public enum DurationUnit { Hours, Days, Weeks, Months }

public class IdSequence
{
    public string SeqKey { get; set; } = "";
    public long CurrentValue { get; set; }
}

public class AuditLog : Entity
{
    public string EntityType { get; set; } = "";
    public long EntityId { get; set; }
    public string Action { get; set; } = "";
    public string? ChangesJson { get; set; }
    public DateTime ChangedAt { get; set; }
    public long? ChangedBy { get; set; }
}

/// <summary>Formats for human-readable identifiers and the sequence keys used to generate them.</summary>
public static class IdFormats
{
    public static string PatientSequenceKey(int year) => $"PATIENT:{year}";
    public static string Mrn(int year, long seq) => $"P{year}{seq:D6}";

    public const string DoctorSequenceKey = "DOCTOR";
    public static string DoctorCode(long seq) => $"D{seq:D4}";

    public static string PrescriptionSequenceKey(DateOnly date) => $"RX:{date:yyyyMMdd}";
    public static string PrescriptionNumber(DateOnly date, long seq) => $"RX{date:yyyyMMdd}{seq:D4}";

    public static string InvoiceSequenceKey(DateOnly date) => $"INV:{date:yyyyMMdd}";
    public static string InvoiceNumber(DateOnly date, long seq) => $"INV{date:yyyyMMdd}{seq:D4}";

    public static bool LooksLikeMrn(string value) =>
        value.Length == 11 && (value[0] == 'P' || value[0] == 'p') && value.Skip(1).All(char.IsDigit);
}
