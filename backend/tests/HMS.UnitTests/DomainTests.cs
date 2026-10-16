using HMS.Application.Clinical;
using HMS.Application.Prescriptions;
using HMS.Domain.Appointments;
using HMS.Domain.Billing;
using HMS.Domain.Clinical;
using HMS.Domain.Common;
using HMS.Domain.Doctors;
using HMS.Domain.Patients;
using HMS.Domain.Prescriptions;

namespace HMS.UnitTests;

public class AppointmentTests
{
    private static readonly DateTime Now = new(2026, 10, 16, 5, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(AppointmentStatus.Booked, AppointmentStatus.Confirmed)]
    [InlineData(AppointmentStatus.Booked, AppointmentStatus.CheckedIn)]
    [InlineData(AppointmentStatus.Confirmed, AppointmentStatus.CheckedIn)]
    [InlineData(AppointmentStatus.CheckedIn, AppointmentStatus.InConsultation)]
    [InlineData(AppointmentStatus.InConsultation, AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.Confirmed, AppointmentStatus.NoShow)]
    public void TransitionTo_AllowedTransition_ChangesStatusAndRecordsHistory(AppointmentStatus from, AppointmentStatus to)
    {
        var a = new Appointment { Status = from };
        a.TransitionTo(to, Now, 1);
        a.Status.ShouldBe(to);
        a.History.Single().FromStatus.ShouldBe(from);
        a.History.Single().ToStatus.ShouldBe(to);
    }

    [Theory]
    [InlineData(AppointmentStatus.Completed, AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.Cancelled, AppointmentStatus.Booked)]
    [InlineData(AppointmentStatus.InConsultation, AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.CheckedIn, AppointmentStatus.NoShow)]
    [InlineData(AppointmentStatus.Booked, AppointmentStatus.Completed)]
    public void TransitionTo_InvalidTransition_Throws(AppointmentStatus from, AppointmentStatus to)
    {
        var a = new Appointment { Status = from };
        var ex = Should.Throw<BusinessRuleException>(() => a.TransitionTo(to, Now, 1));
        ex.Code.ShouldBe("INVALID_STATUS_TRANSITION");
        a.Status.ShouldBe(from);
    }

    [Fact]
    public void Reschedule_BookedAppointment_MovesSlotAndKeepsOldValuesInHistory()
    {
        var a = new Appointment { AppointmentDate = new(2026, 10, 20), StartTime = new(9, 0), EndTime = new(9, 15) };
        a.Reschedule(new(2026, 10, 21), new(10, 0), new(10, 15), Now, 1);

        a.AppointmentDate.ShouldBe(new DateOnly(2026, 10, 21));
        a.StartTime.ShouldBe(new TimeOnly(10, 0));
        a.RescheduleCount.ShouldBe((short)1);
        a.History.Single().OldDate.ShouldBe(new DateOnly(2026, 10, 20));
        a.History.Single().OldStartTime.ShouldBe(new TimeOnly(9, 0));
    }

    [Fact]
    public void Reschedule_CheckedInAppointment_Throws()
    {
        var a = new Appointment { Status = AppointmentStatus.CheckedIn };
        Should.Throw<BusinessRuleException>(() => a.Reschedule(new(2026, 10, 21), new(10, 0), new(10, 15), Now, 1));
    }

    [Fact]
    public void Cancel_SetsReasonAndReleasesSlot()
    {
        var a = new Appointment();
        a.Cancel("Patient unwell", Now, 1);
        a.Status.ShouldBe(AppointmentStatus.Cancelled);
        a.CancellationReason.ShouldBe("Patient unwell");
        a.HoldsSlot.ShouldBeFalse();
    }
}

public class SlotGeneratorTests
{
    private static readonly DateOnly Monday = new(2026, 10, 19);

    [Fact]
    public void Generate_SplitsBlocksIntoSlots()
    {
        var schedules = new[]
        {
            new DoctorSchedule { DayOfWeek = 1, StartTime = new(9, 0), EndTime = new(10, 0), SlotDurationMinutes = 15 },
            new DoctorSchedule { DayOfWeek = 1, StartTime = new(17, 0), EndTime = new(17, 40), SlotDurationMinutes = 20 },
            new DoctorSchedule { DayOfWeek = 2, StartTime = new(9, 0), EndTime = new(12, 0), SlotDurationMinutes = 15 },
        };
        var slots = SlotGenerator.Generate(Monday, schedules, []);
        slots.Count.ShouldBe(6);
        slots[0].ShouldBe(new TimeSlot(new(9, 0), new(9, 15)));
        slots[^1].ShouldBe(new TimeSlot(new(17, 20), new(17, 40)));
    }

    [Fact]
    public void Generate_DropsPartialSlotAtEndOfBlock()
    {
        var schedules = new[] { new DoctorSchedule { DayOfWeek = 1, StartTime = new(9, 0), EndTime = new(9, 50), SlotDurationMinutes = 15 } };
        SlotGenerator.Generate(Monday, schedules, []).Count.ShouldBe(3);
    }

    [Fact]
    public void Generate_OnLeave_ReturnsNoSlots()
    {
        var schedules = new[] { new DoctorSchedule { DayOfWeek = 1, StartTime = new(9, 0), EndTime = new(13, 0) } };
        var leaves = new[] { new DoctorLeave { FromDate = Monday.AddDays(-1), ToDate = Monday.AddDays(1) } };
        SlotGenerator.Generate(Monday, schedules, leaves).ShouldBeEmpty();
    }

    [Fact]
    public void FindOverlaps_DetectsOverlappingBlocksOnSameDay()
    {
        var blocks = new[]
        {
            new DoctorSchedule { DayOfWeek = 1, StartTime = new(9, 0), EndTime = new(13, 0) },
            new DoctorSchedule { DayOfWeek = 1, StartTime = new(12, 0), EndTime = new(14, 0) },
            new DoctorSchedule { DayOfWeek = 2, StartTime = new(12, 0), EndTime = new(14, 0) },
        };
        SlotGenerator.FindOverlaps(blocks).Count.ShouldBe(1);
    }

    [Fact]
    public void FindOverlaps_AdjacentBlocks_AreValid()
    {
        var blocks = new[]
        {
            new DoctorSchedule { DayOfWeek = 1, StartTime = new(9, 0), EndTime = new(13, 0) },
            new DoctorSchedule { DayOfWeek = 1, StartTime = new(13, 0), EndTime = new(14, 0) },
        };
        SlotGenerator.FindOverlaps(blocks).ShouldBeEmpty();
    }
}

public class PatientTests
{
    private static readonly DateOnly Today = new(2026, 10, 16);

    [Theory]
    [InlineData(2000, 10, 16, 26)]
    [InlineData(2000, 10, 17, 25)]
    [InlineData(2026, 1, 1, 0)]
    public void CalculateAge_FromDateOfBirth(int y, int m, int d, int expected) =>
        Patient.CalculateAge(new DateOnly(y, m, d), null, null, Today).ShouldBe(expected);

    [Fact]
    public void CalculateAge_FromRecordedAge_CountsUpOverTime() =>
        Patient.CalculateAge(null, 40, new DateOnly(2024, 10, 1), Today).ShouldBe(42);

    [Theory]
    [InlineData("98765 43210", "9876543210")]
    [InlineData("+91 98765-43210", "+919876543210")]
    public void NormalisePhone_KeepsDigitsAndLeadingPlus(string input, string expected) =>
        Patient.NormalisePhone(input).ShouldBe(expected);

    [Fact]
    public void IdFormats_ProduceDocumentedFormats()
    {
        IdFormats.Mrn(2026, 123).ShouldBe("P2026000123");
        IdFormats.DoctorCode(12).ShouldBe("D0012");
        IdFormats.PrescriptionNumber(new DateOnly(2026, 10, 16), 7).ShouldBe("RX202610160007");
        IdFormats.LooksLikeMrn("p2026000123").ShouldBeTrue();
        IdFormats.LooksLikeMrn("9876543210").ShouldBeFalse();
    }
}

public class ClinicalTests
{
    [Fact]
    public void Bmi_IsComputedFromWeightAndHeight() =>
        VitalSigns.CalculateBmi(70m, 175m).ShouldBe(22.9m);

    [Fact]
    public void AbnormalWarnings_FlagOutOfRangeValues()
    {
        var v = new VitalSigns { TemperatureC = 39.2m, PulseBpm = 80, Spo2Percent = 91 };
        var warnings = v.AbnormalWarnings();
        warnings.Count.ShouldBe(2);
    }

    [Fact]
    public void Finalise_WithoutChiefComplaint_Throws()
    {
        var c = new Consultation();
        Should.Throw<BusinessRuleException>(() => c.Finalise(DateTime.UtcNow)).Code.ShouldBe("CHIEF_COMPLAINT_REQUIRED");
    }

    [Fact]
    public void Finalise_Twice_Throws()
    {
        var c = new Consultation { ChiefComplaint = "Fever" };
        c.Finalise(DateTime.UtcNow);
        Should.Throw<BusinessRuleException>(() => c.Finalise(DateTime.UtcNow)).Code.ShouldBe("CONSULTATION_FINALISED");
    }

    [Fact]
    public void Prescription_FinaliseWithoutItems_Throws() =>
        Should.Throw<BusinessRuleException>(() => new Prescription().Finalise(DateTime.UtcNow)).Code.ShouldBe("PRESCRIPTION_EMPTY");

    [Fact]
    public void RevisionDiff_ReportsChangedTopLevelFields()
    {
        var changes = ClinicalRevisionWriter.Diff(
            """{"diagnosis":"Viral fever","clinicalNotes":null,"status":"Draft"}""",
            """{"diagnosis":"Dengue","clinicalNotes":"NS1 positive","status":"Draft"}""");
        changes.Select(c => c.Field).ShouldBe(["diagnosis", "clinicalNotes"], ignoreOrder: true);
        changes.Single(c => c.Field == "diagnosis").OldValue.ShouldBe("Viral fever");
    }

    [Fact]
    public void AllergyWarnings_MatchOnBrandOrGenericName()
    {
        var items = new[]
        {
            new PrescriptionItem { MedicineId = 1, MedicineName = "Augmentin 625" },
            new PrescriptionItem { MedicineName = "Paracetamol 500 mg" },
        };
        var generics = new Dictionary<long, string?> { [1] = "Amoxicillin/Clavulanate" };
        var warnings = PrescriptionService.AllergyWarnings(["Amoxicillin"], items, generics);
        warnings.Count.ShouldBe(1);
        warnings[0].ShouldContain("Augmentin");
    }
}

public class InvoiceTests
{
    [Fact]
    public void MarkPaid_RecordsFullPaymentAndClosesInvoice()
    {
        var invoice = new Invoice();
        invoice.AddItem("Consultation", InvoiceItemType.ConsultationFee, 1, 500m);
        var payment = invoice.MarkPaid(PaymentMethod.UPI, "UPI123", DateTime.UtcNow, 1);

        payment.Amount.ShouldBe(500m);
        invoice.PaidAmount.ShouldBe(500m);
        invoice.Status.ShouldBe(InvoiceStatus.Paid);
    }

    [Fact]
    public void Cancel_PaidInvoice_Throws()
    {
        var invoice = new Invoice();
        invoice.AddItem("Consultation", InvoiceItemType.ConsultationFee, 1, 500m);
        invoice.MarkPaid(PaymentMethod.Cash, null, DateTime.UtcNow, 1);
        Should.Throw<BusinessRuleException>(() => invoice.Cancel("mistake")).Code.ShouldBe("INVOICE_ALREADY_PAID");
    }
}
