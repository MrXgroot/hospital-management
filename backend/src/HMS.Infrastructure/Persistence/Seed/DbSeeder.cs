using HMS.Application.Common;
using HMS.Domain.Appointments;
using HMS.Domain.Billing;
using HMS.Domain.Common;
using HMS.Domain.Doctors;
using HMS.Domain.Identity;
using HMS.Domain.Patients;
using HMS.Domain.Prescriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HMS.Infrastructure.Persistence.Seed;

public sealed class SeedOptions
{
    public const string Section = "Seed";
    public bool MigrateOnStartup { get; set; }
    /// <summary>Password for the initial 'admin' user. Required the first time the database is seeded.</summary>
    public string? AdminPassword { get; set; }
    /// <summary>Adds demo doctors, staff users, patients and today's appointments (development only).</summary>
    public bool DemoData { get; set; }
    public string DemoUserPassword { get; set; } = "Demo@1234";
}

public sealed class DbSeeder(
    HmsDbContext db, IPasswordHasher hasher, ISequenceGenerator sequences, IClock clock,
    IOptions<SeedOptions> options, ILogger<DbSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var o = options.Value;
        if (o.MigrateOnStartup)
        {
            logger.LogInformation("Applying database migrations");
            await db.Database.MigrateAsync(ct);
        }

        await SeedReferenceDataAsync(ct);

        if (!await db.Users.AnyAsync(ct))
        {
            if (string.IsNullOrWhiteSpace(o.AdminPassword))
                throw new InvalidOperationException("Seed:AdminPassword must be set to create the initial admin user.");
            db.Users.Add(new User { Username = "admin", FullName = "System Administrator", Role = Role.Admin, PasswordHash = hasher.Hash(o.AdminPassword) });
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Created initial admin user 'admin'");
        }

        if (o.DemoData && !await db.Doctors.IgnoreQueryFilters().AnyAsync(ct))
            await SeedDemoDataAsync(o.DemoUserPassword, ct);
    }

    private async Task SeedReferenceDataAsync(CancellationToken ct)
    {
        if (!await db.Departments.AnyAsync(ct))
        {
            db.Departments.AddRange(
                new Department { Name = "General Medicine", Description = "Primary care and internal medicine" },
                new Department { Name = "Pediatrics", Description = "Care for infants, children and adolescents" },
                new Department { Name = "Orthopedics", Description = "Bones, joints and muscles" },
                new Department { Name = "Cardiology", Description = "Heart and blood vessels" },
                new Department { Name = "Dermatology", Description = "Skin, hair and nails" },
                new Department { Name = "ENT", Description = "Ear, nose and throat" },
                new Department { Name = "Obstetrics & Gynecology", Description = "Women's health and pregnancy" });
        }

        if (!await db.Specializations.AnyAsync(ct))
        {
            db.Specializations.AddRange(new[]
            {
                "General Physician", "Internal Medicine", "Pediatrician", "Neonatology", "Orthopedic Surgeon",
                "Sports Medicine", "Cardiologist", "Interventional Cardiology", "Dermatologist", "Cosmetology",
                "ENT Specialist", "Gynecologist", "Obstetrician", "Diabetology",
            }.Select(n => new Specialization { Name = n }));
        }

        if (!await db.Medicines.AnyAsync(ct))
        {
            db.Medicines.AddRange(
                Med("Paracetamol", "Paracetamol", "Tablet", "500 mg"),
                Med("Paracetamol", "Paracetamol", "Tablet", "650 mg"),
                Med("Ibuprofen", "Ibuprofen", "Tablet", "400 mg"),
                Med("Amoxicillin", "Amoxicillin", "Capsule", "500 mg"),
                Med("Amoxicillin + Clavulanic Acid", "Amoxicillin/Clavulanate", "Tablet", "625 mg"),
                Med("Azithromycin", "Azithromycin", "Tablet", "500 mg"),
                Med("Cetirizine", "Cetirizine", "Tablet", "10 mg"),
                Med("Levocetirizine", "Levocetirizine", "Tablet", "5 mg"),
                Med("Montelukast", "Montelukast", "Tablet", "10 mg"),
                Med("Pantoprazole", "Pantoprazole", "Tablet", "40 mg"),
                Med("Omeprazole", "Omeprazole", "Capsule", "20 mg"),
                Med("Ondansetron", "Ondansetron", "Tablet", "4 mg"),
                Med("Domperidone", "Domperidone", "Tablet", "10 mg"),
                Med("Metformin", "Metformin", "Tablet", "500 mg"),
                Med("Amlodipine", "Amlodipine", "Tablet", "5 mg"),
                Med("Losartan", "Losartan", "Tablet", "50 mg"),
                Med("Atorvastatin", "Atorvastatin", "Tablet", "10 mg"),
                Med("Salbutamol Inhaler", "Salbutamol", "Inhaler", "100 mcg"),
                Med("ORS Sachet", "Oral Rehydration Salts", "Powder", "21 g"),
                Med("Vitamin D3", "Cholecalciferol", "Capsule", "60000 IU"),
                Med("Diclofenac Gel", "Diclofenac", "Gel", "1%"),
                Med("Paracetamol Syrup", "Paracetamol", "Syrup", "250 mg/5 ml"));
        }

        await db.SaveChangesAsync(ct);
    }

    private static Medicine Med(string name, string generic, string form, string strength) =>
        new() { Name = name, GenericName = generic, Form = form, Strength = strength };

    private async Task SeedDemoDataAsync(string password, CancellationToken ct)
    {
        logger.LogInformation("Seeding demo data");
        var departments = await db.Departments.ToDictionaryAsync(d => d.Name, ct);
        var specs = await db.Specializations.ToDictionaryAsync(s => s.Name, ct);
        var hash = hasher.Hash(password);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var doctorSeeds = new[]
        {
            (First: "Anil", Last: "Sharma", Gender.Male, Dept: "General Medicine", Spec: "General Physician", Fee: 500m, Qual: "MBBS, MD (Medicine)", User: "dr.sharma"),
            (First: "Priya", Last: "Nair", Gender.Female, Dept: "Pediatrics", Spec: "Pediatrician", Fee: 600m, Qual: "MBBS, DCH", User: "dr.nair"),
            (First: "Rahul", Last: "Verma", Gender.Male, Dept: "Orthopedics", Spec: "Orthopedic Surgeon", Fee: 800m, Qual: "MBBS, MS (Ortho)", User: "dr.verma"),
            (First: "Meena", Last: "Iyer", Gender.Female, Dept: "Cardiology", Spec: "Cardiologist", Fee: 1000m, Qual: "MBBS, MD, DM (Cardiology)", User: "dr.iyer"),
        };

        var doctors = new List<Doctor>();
        var i = 0;
        foreach (var s in doctorSeeds)
        {
            i++;
            var doctor = new Doctor
            {
                DoctorCode = IdFormats.DoctorCode(await sequences.NextAsync(IdFormats.DoctorSequenceKey, ct)),
                FirstName = s.First, LastName = s.Last, Gender = s.Item3, Phone = $"98400000{i:D2}",
                Email = $"{s.User}@hospital.local", Qualification = s.Qual, RegistrationNumber = $"KMC-{10230 + i}",
                DepartmentId = departments[s.Dept].Id, ConsultationFee = s.Fee, YearsOfExperience = (short)(5 + i * 3),
                Specializations = [new DoctorSpecialization { SpecializationId = specs[s.Spec].Id, IsPrimary = true }],
            };
            // Mon-Sat mornings for everyone; evenings Mon/Wed/Fri. Sunday morning for the general physician.
            for (byte day = 1; day <= 6; day++)
            {
                doctor.Schedules.Add(new DoctorSchedule { DayOfWeek = day, StartTime = new(9, 0), EndTime = new(13, 0), SlotDurationMinutes = 15 });
                if (day is 1 or 3 or 5)
                    doctor.Schedules.Add(new DoctorSchedule { DayOfWeek = day, StartTime = new(17, 0), EndTime = new(20, 0), SlotDurationMinutes = 15 });
            }
            if (i == 1)
                doctor.Schedules.Add(new DoctorSchedule { DayOfWeek = 0, StartTime = new(9, 0), EndTime = new(12, 0), SlotDurationMinutes = 15 });
            doctors.Add(doctor);
            db.Doctors.Add(doctor);
        }
        await db.SaveChangesAsync(ct);

        foreach (var (doctor, s) in doctors.Zip(doctorSeeds))
            db.Users.Add(new User { Username = s.User, FullName = doctor.FullName, Role = Role.Doctor, DoctorId = doctor.Id, PasswordHash = hash });
        db.Users.Add(new User { Username = "reception", FullName = "Kavya Reddy", Role = Role.Receptionist, PasswordHash = hash });
        db.Users.Add(new User { Username = "nurse", FullName = "Lakshmi Menon", Role = Role.Nurse, PasswordHash = hash });

        var patientSeeds = new (string First, string Last, Gender Gender, int BirthYear, string Phone, string City)[]
        {
            ("Ramesh", "Kumar", Gender.Male, 1978, "9876543210", "Bengaluru"),
            ("Sunita", "Devi", Gender.Female, 1985, "9876543211", "Chennai"),
            ("Arjun", "Patel", Gender.Male, 2016, "9876543212", "Bengaluru"),
            ("Fatima", "Sheikh", Gender.Female, 1992, "9876543213", "Hyderabad"),
            ("Vikram", "Singh", Gender.Male, 1960, "9876543214", "Mysuru"),
            ("Ananya", "Rao", Gender.Female, 2001, "9876543215", "Bengaluru"),
            ("Joseph", "Thomas", Gender.Male, 1970, "9876543216", "Kochi"),
            ("Deepa", "Krishnan", Gender.Female, 1988, "9876543217", "Chennai"),
        };
        var year = clock.Today.Year;
        var patients = new List<Patient>();
        foreach (var p in patientSeeds)
        {
            var patient = new Patient
            {
                Mrn = IdFormats.Mrn(year, await sequences.NextAsync(IdFormats.PatientSequenceKey(year), ct)),
                FirstName = p.First, LastName = p.Last, Gender = p.Gender, DateOfBirth = new DateOnly(p.BirthYear, 6, 15),
                Phone = p.Phone, City = p.City, State = "Karnataka", Country = "India",
            };
            patients.Add(patient);
            db.Patients.Add(patient);
        }
        patients[0].Allergies.Add(new PatientAllergy { Allergen = "Penicillin", Reaction = "Rash", Severity = "Moderate" });
        patients[4].Conditions.Add(new PatientCondition { ConditionName = "Type 2 Diabetes", SinceYear = 2015 });
        patients[4].Conditions.Add(new PatientCondition { ConditionName = "Hypertension", SinceYear = 2018 });
        await db.SaveChangesAsync(ct);

        await SeedTodayAppointmentsAsync(doctors, patients, ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>A few appointments today (if it is a working day) so the dashboard and queue have data.</summary>
    private async Task SeedTodayAppointmentsAsync(List<Doctor> doctors, List<Patient> patients, CancellationToken ct)
    {
        var today = clock.Today;
        var now = clock.UtcNow;
        var receptionId = await db.Users.Where(u => u.Username == "reception").Select(u => u.Id).SingleAsync(ct);

        for (var i = 0; i < 6; i++)
        {
            var doctor = doctors[i % 2]; // Dr. Sharma and Dr. Nair
            var slots = SlotGenerator.Generate(today, doctor.Schedules, []);
            if (slots.Count <= i / 2) continue;
            var slot = slots[i / 2];

            var appointment = new Appointment
            {
                PatientId = patients[i].Id, DoctorId = doctor.Id, AppointmentDate = today,
                StartTime = slot.Start, EndTime = slot.End, ReasonForVisit = i % 2 == 0 ? "Fever and cough" : "Routine check-up",
            };
            appointment.History.Add(new AppointmentStatusHistory { ToStatus = AppointmentStatus.Booked, ChangedAt = now, ChangedBy = receptionId });
            db.Appointments.Add(appointment);

            if (i < 3)
            {
                appointment.TransitionTo(AppointmentStatus.CheckedIn, now, receptionId);
                appointment.QueueToken = (short)(i / 2 + 1);
                appointment.CheckedInAt = now;
                var invoice = new Invoice
                {
                    InvoiceNumber = IdFormats.InvoiceNumber(today, await sequences.NextAsync(IdFormats.InvoiceSequenceKey(today), ct)),
                    PatientId = patients[i].Id, Appointment = appointment, InvoiceDate = today,
                };
                invoice.AddItem($"Consultation fee - {doctor.FullName}", InvoiceItemType.ConsultationFee, 1, doctor.ConsultationFee);
                if (i == 0) invoice.MarkPaid(PaymentMethod.UPI, "DEMO-UPI-001", now, receptionId);
                db.Invoices.Add(invoice);
            }
        }
        await db.SaveChangesAsync(ct);
    }
}
