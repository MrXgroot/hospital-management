using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace HMS.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class AuthTests(HmsApiFactory factory)
{
    [Fact]
    public async Task Endpoints_WithoutToken_Return401()
    {
        var response = await factory.CreateClient().GetAsync("/api/patients");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401WithCode()
    {
        var response = await factory.CreateClient().PostJsonAsync("/api/auth/login", new { username = "admin", password = "wrong-password" });
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await response.JsonAsync()).GetProperty("code").GetString().ShouldBe("INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Nurse_CannotRegisterPatients()
    {
        var nurse = await factory.ClientForAsync("nurse");
        var response = await nurse.PostJsonAsync("/api/patients", NewPatient("Test", "9000000001"));
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Receptionist_CannotManageDoctors()
    {
        var reception = await factory.ClientForAsync("reception");
        var response = await reception.PatchAsync("/api/doctors/1/status", JsonContent(new { isActive = false }));
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    internal static object NewPatient(string firstName, string phone) => new
    {
        firstName, lastName = "Integration", gender = "Female", ageYears = 30, phone,
    };

    private static StringContent JsonContent(object body) =>
        new(JsonSerializer.Serialize(body, HmsApiFactory.Json), System.Text.Encoding.UTF8, "application/json");
}

[Collection(ApiCollection.Name)]
public sealed class PatientTests(HmsApiFactory factory)
{
    [Fact]
    public async Task ConcurrentRegistrations_GetUniqueSequentialMrns()
    {
        var reception = await factory.ClientForAsync("reception");
        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(i =>
            reception.PostJsonAsync("/api/patients", AuthTests.NewPatient($"Concurrent{i}", $"90100000{i:D2}"))));

        responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.Created);
        var mrns = new List<string>();
        foreach (var r in responses) mrns.Add((await r.JsonAsync()).GetProperty("mrn").GetString()!);
        mrns.Distinct().Count().ShouldBe(10);
        mrns.ShouldAllBe(m => m.Length == 11 && m.StartsWith('P'));
    }

    [Fact]
    public async Task DuplicatePhoneAndName_Returns409UntilConfirmed()
    {
        var reception = await factory.ClientForAsync("reception");
        (await reception.PostJsonAsync("/api/patients", AuthTests.NewPatient("Dup", "9020000000"))).StatusCode.ShouldBe(HttpStatusCode.Created);

        var duplicate = await reception.PostJsonAsync("/api/patients", AuthTests.NewPatient("Dup", "9020000000"));
        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var body = await duplicate.JsonAsync();
        body.GetProperty("code").GetString().ShouldBe("DUPLICATE_PATIENT");
        body.GetProperty("matches").GetArrayLength().ShouldBe(1);

        var confirmed = await reception.PostJsonAsync("/api/patients?confirmDuplicate=true", AuthTests.NewPatient("Dup", "9020000000"));
        confirmed.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Search_ByPhonePrefix_FindsPatient()
    {
        var reception = await factory.ClientForAsync("reception");
        await reception.PostJsonAsync("/api/patients", AuthTests.NewPatient("Searchable", "9030000000"));
        var result = await (await reception.GetAsync("/api/patients?search=903000")).JsonAsync();
        result.GetProperty("totalCount").GetInt32().ShouldBe(1);
    }
}

[Collection(ApiCollection.Name)]
public sealed class AppointmentTests(HmsApiFactory factory)
{
    [Fact]
    public async Task ConcurrentBookingsForSameSlot_OnlyOneSucceeds()
    {
        var reception = await factory.ClientForAsync("reception");
        var doctorId = await factory.DoctorIdAsync("dr.verma");
        var date = HmsApiFactory.NextWorkingDay();
        var patientIds = new List<long>();
        for (var i = 0; i < 5; i++)
            patientIds.Add(await (await reception.PostJsonAsync("/api/patients", AuthTests.NewPatient($"Slot{i}", $"90400000{i:D2}"))).IdOf());

        var responses = await Task.WhenAll(patientIds.Select(pid => reception.PostJsonAsync("/api/appointments", new
        {
            patientId = pid, doctorId, appointmentDate = date.ToString("yyyy-MM-dd"), startTime = "10:00",
        })));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        foreach (var r in responses.Where(r => r.StatusCode != HttpStatusCode.Created))
        {
            r.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            (await r.JsonAsync()).GetProperty("code").GetString().ShouldBe("SLOT_UNAVAILABLE");
        }
    }

    [Fact]
    public async Task CancelledSlot_CanBeBookedAgain()
    {
        var reception = await factory.ClientForAsync("reception");
        var doctorId = await factory.DoctorIdAsync("dr.iyer");
        var date = HmsApiFactory.NextWorkingDay(1).ToString("yyyy-MM-dd");
        var p1 = await (await reception.PostJsonAsync("/api/patients", AuthTests.NewPatient("Cancel1", "9050000001"))).IdOf();
        var p2 = await (await reception.PostJsonAsync("/api/patients", AuthTests.NewPatient("Cancel2", "9050000002"))).IdOf();

        var first = await (await reception.PostJsonAsync("/api/appointments", new { patientId = p1, doctorId, appointmentDate = date, startTime = "11:00" })).IdOf();
        (await reception.PostJsonAsync($"/api/appointments/{first}/cancel", new { reason = "Patient request" })).StatusCode.ShouldBe(HttpStatusCode.OK);

        var second = await reception.PostJsonAsync("/api/appointments", new { patientId = p2, doctorId, appointmentDate = date, startTime = "11:00" });
        second.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Booking_OutsideSchedule_Returns422()
    {
        var reception = await factory.ClientForAsync("reception");
        var doctorId = await factory.DoctorIdAsync("dr.iyer");
        var response = await reception.PostJsonAsync("/api/appointments", new
        {
            patientId = 1, doctorId, appointmentDate = HmsApiFactory.NextWorkingDay().ToString("yyyy-MM-dd"), startTime = "15:00",
        });
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.JsonAsync()).GetProperty("code").GetString().ShouldBe("SLOT_NOT_IN_SCHEDULE");
    }
}

[Collection(ApiCollection.Name)]
public sealed class ClinicalTests(HmsApiFactory factory)
{
    [Fact]
    public async Task FinalisedConsultation_AmendmentNeedsReason_AndHistoryIsKept()
    {
        var doctor = await factory.ClientForAsync("dr.sharma");
        var patientId = await factory.WithDbAsync(db => db.Patients.OrderBy(p => p.Id).Select(p => p.Id).FirstAsync());

        var consultation = await (await doctor.PostJsonAsync("/api/consultations", new { patientId })).JsonAsync();
        var id = consultation.GetProperty("id").GetInt64();

        var update = await doctor.PutJsonAsync($"/api/consultations/{id}", new
        {
            chiefComplaint = "Fever for 3 days", diagnosis = "Viral fever", symptoms = new[] { new { symptom = "Fever", durationValue = 3, durationUnit = "Days" } },
            rowVersion = consultation.GetProperty("rowVersion").GetInt32(),
        });
        update.StatusCode.ShouldBe(HttpStatusCode.OK);
        var finalised = await (await doctor.PostAsync($"/api/consultations/{id}/finalise", null)).JsonAsync();
        finalised.GetProperty("status").GetString().ShouldBe("Finalised");

        var noReason = await doctor.PutJsonAsync($"/api/consultations/{id}", new
        {
            chiefComplaint = "Fever for 3 days", diagnosis = "Dengue", symptoms = Array.Empty<object>(),
            rowVersion = finalised.GetProperty("rowVersion").GetInt32(),
        });
        noReason.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await noReason.JsonAsync()).GetProperty("code").GetString().ShouldBe("AMENDMENT_REASON_REQUIRED");

        var amended = await doctor.PutJsonAsync($"/api/consultations/{id}", new
        {
            chiefComplaint = "Fever for 3 days", diagnosis = "Dengue", symptoms = Array.Empty<object>(),
            rowVersion = finalised.GetProperty("rowVersion").GetInt32(), amendmentReason = "NS1 antigen positive",
        });
        amended.StatusCode.ShouldBe(HttpStatusCode.OK);

        var history = await (await doctor.GetAsync($"/api/consultations/{id}/history")).JsonAsync();
        var changeTypes = history.EnumerateArray().Select(r => r.GetProperty("changeType").GetString()).ToList();
        changeTypes.ShouldBe(["Amended", "Finalised", "Updated", "Created"]);
        var amendment = history[0];
        amendment.GetProperty("reason").GetString().ShouldBe("NS1 antigen positive");
        amendment.GetProperty("changes").EnumerateArray().ShouldContain(c => c.GetProperty("field").GetString() == "diagnosis");
    }

    [Fact]
    public async Task OtherDoctor_CannotEditConsultation()
    {
        var owner = await factory.ClientForAsync("dr.sharma");
        var other = await factory.ClientForAsync("dr.nair");
        var patientId = await factory.WithDbAsync(db => db.Patients.OrderBy(p => p.Id).Select(p => p.Id).FirstAsync());
        var consultation = await (await owner.PostJsonAsync("/api/consultations", new { patientId })).JsonAsync();

        var response = await other.PutJsonAsync($"/api/consultations/{consultation.GetProperty("id").GetInt64()}", new
        {
            chiefComplaint = "x", symptoms = Array.Empty<object>(), rowVersion = consultation.GetProperty("rowVersion").GetInt32(),
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
