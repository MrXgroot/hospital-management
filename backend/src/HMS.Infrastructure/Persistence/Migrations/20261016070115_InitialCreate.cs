using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    entity_type = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    entity_id = table.Column<long>(type: "bigint", nullable: false),
                    action = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    changes_json = table.Column<string>(type: "json", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    changed_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    changed_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_logs", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "clinical_record_revisions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    entity_type = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    entity_id = table.Column<long>(type: "bigint", nullable: false),
                    consultation_id = table.Column<long>(type: "bigint", nullable: false),
                    revision_number = table.Column<int>(type: "int", nullable: false),
                    change_type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    snapshot_json = table.Column<string>(type: "json", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    changed_fields = table.Column<string>(type: "json", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    changed_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    changed_by = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clinical_record_revisions", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "departments",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_departments", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "id_sequences",
                columns: table => new
                {
                    seq_key = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    current_value = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_id_sequences", x => x.seq_key);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "medicines",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    generic_name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    form = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    strength = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_medicines", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "patients",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    mrn = table.Column<string>(type: "varchar(12)", maxLength: 12, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    first_name = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    last_name = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    gender = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: true),
                    age_years = table.Column<short>(type: "smallint", nullable: true),
                    age_recorded_on = table.Column<DateOnly>(type: "date", nullable: true),
                    blood_group = table.Column<string>(type: "varchar(5)", maxLength: 5, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    phone = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    alternate_phone = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    email = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    address_line1 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    address_line2 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    city = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    state = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    postal_code = table.Column<string>(type: "varchar(12)", maxLength: 12, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    country = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    emergency_contact_name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    emergency_contact_phone = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    emergency_contact_relation = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    is_deleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_patients", x => x.id);
                    table.CheckConstraint("CK_patients_age", "`date_of_birth` IS NOT NULL OR `age_years` IS NOT NULL");
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "specializations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_specializations", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "doctors",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    doctor_code = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    first_name = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    last_name = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    gender = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    phone = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    email = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    qualification = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    registration_number = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    department_id = table.Column<long>(type: "bigint", nullable: false),
                    consultation_fee = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    years_of_experience = table.Column<short>(type: "smallint", nullable: true),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    is_deleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_doctors", x => x.id);
                    table.CheckConstraint("CK_doctors_fee", "`consultation_fee` >= 0");
                    table.ForeignKey(
                        name: "fk_doctors_departments_department_id",
                        column: x => x.department_id,
                        principalTable: "departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "patient_allergies",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    patient_id = table.Column<long>(type: "bigint", nullable: false),
                    allergen = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    reaction = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    severity = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_patient_allergies", x => x.id);
                    table.ForeignKey(
                        name: "fk_patient_allergies_patients_patient_id",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "patient_conditions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    patient_id = table.Column<long>(type: "bigint", nullable: false),
                    condition_name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    since_year = table.Column<short>(type: "smallint", nullable: true),
                    notes = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_patient_conditions", x => x.id);
                    table.ForeignKey(
                        name: "fk_patient_conditions_patients_patient_id",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "appointments",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    patient_id = table.Column<long>(type: "bigint", nullable: false),
                    doctor_id = table.Column<long>(type: "bigint", nullable: false),
                    appointment_date = table.Column<DateOnly>(type: "date", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time(6)", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time(6)", nullable: false),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    appointment_type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    reason_for_visit = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    queue_token = table.Column<short>(type: "smallint", nullable: true),
                    checked_in_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    cancelled_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    cancellation_reason = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    reschedule_count = table.Column<short>(type: "smallint", nullable: false),
                    follow_up_of_consultation_id = table.Column<long>(type: "bigint", nullable: true),
                    active_slot = table.Column<sbyte>(type: "tinyint", nullable: true, computedColumnSql: "(case when `status` in ('Cancelled','NoShow') then NULL else 1 end)", stored: true),
                    row_version = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_appointments", x => x.id);
                    table.CheckConstraint("CK_appointments_time", "`end_time` > `start_time`");
                    table.ForeignKey(
                        name: "fk_appointments_doctors_doctor_id",
                        column: x => x.doctor_id,
                        principalTable: "doctors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_appointments_patients_patient_id",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "doctor_leaves",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    doctor_id = table.Column<long>(type: "bigint", nullable: false),
                    from_date = table.Column<DateOnly>(type: "date", nullable: false),
                    to_date = table.Column<DateOnly>(type: "date", nullable: false),
                    reason = table.Column<string>(type: "varchar(250)", maxLength: 250, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_doctor_leaves", x => x.id);
                    table.CheckConstraint("CK_doctor_leaves_dates", "`to_date` >= `from_date`");
                    table.ForeignKey(
                        name: "fk_doctor_leaves_doctors_doctor_id",
                        column: x => x.doctor_id,
                        principalTable: "doctors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "doctor_schedules",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    doctor_id = table.Column<long>(type: "bigint", nullable: false),
                    day_of_week = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time(6)", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time(6)", nullable: false),
                    slot_duration_minutes = table.Column<short>(type: "smallint", nullable: false),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_doctor_schedules", x => x.id);
                    table.CheckConstraint("CK_doctor_schedules_slot", "`slot_duration_minutes` BETWEEN 5 AND 120");
                    table.CheckConstraint("CK_doctor_schedules_time", "`end_time` > `start_time`");
                    table.ForeignKey(
                        name: "fk_doctor_schedules_doctors_doctor_id",
                        column: x => x.doctor_id,
                        principalTable: "doctors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "doctor_specializations",
                columns: table => new
                {
                    doctor_id = table.Column<long>(type: "bigint", nullable: false),
                    specialization_id = table.Column<long>(type: "bigint", nullable: false),
                    is_primary = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_doctor_specializations", x => new { x.doctor_id, x.specialization_id });
                    table.ForeignKey(
                        name: "fk_doctor_specializations_doctors_doctor_id",
                        column: x => x.doctor_id,
                        principalTable: "doctors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_doctor_specializations_specializations_specialization_id",
                        column: x => x.specialization_id,
                        principalTable: "specializations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    username = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    email = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    password_hash = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    full_name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    role = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    doctor_id = table.Column<long>(type: "bigint", nullable: true),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    last_login_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                    table.ForeignKey(
                        name: "fk_users_doctors_doctor_id",
                        column: x => x.doctor_id,
                        principalTable: "doctors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "appointment_status_history",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    appointment_id = table.Column<long>(type: "bigint", nullable: false),
                    from_status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    to_status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    old_date = table.Column<DateOnly>(type: "date", nullable: true),
                    old_start_time = table.Column<TimeOnly>(type: "time(6)", nullable: true),
                    note = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    changed_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    changed_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_appointment_status_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_appointment_status_history_appointments_appointment_id",
                        column: x => x.appointment_id,
                        principalTable: "appointments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "consultations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    patient_id = table.Column<long>(type: "bigint", nullable: false),
                    doctor_id = table.Column<long>(type: "bigint", nullable: false),
                    appointment_id = table.Column<long>(type: "bigint", nullable: true),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    started_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    finalised_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    chief_complaint = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    history_of_present_illness = table.Column<string>(type: "text", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    examination_findings = table.Column<string>(type: "text", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    diagnosis = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    diagnosis_icd10 = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    clinical_notes = table.Column<string>(type: "text", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    advice = table.Column<string>(type: "text", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    row_version = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_consultations", x => x.id);
                    table.ForeignKey(
                        name: "fk_consultations_appointments_appointment_id",
                        column: x => x.appointment_id,
                        principalTable: "appointments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_consultations_doctors_doctor_id",
                        column: x => x.doctor_id,
                        principalTable: "doctors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_consultations_patients_patient_id",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "invoices",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    invoice_number = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    patient_id = table.Column<long>(type: "bigint", nullable: false),
                    appointment_id = table.Column<long>(type: "bigint", nullable: true),
                    invoice_date = table.Column<DateOnly>(type: "date", nullable: false),
                    subtotal = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    discount_amount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    total_amount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    paid_amount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    balance_due = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false, computedColumnSql: "(`total_amount` - `paid_amount`)", stored: true),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    cancel_reason = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    row_version = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_invoices", x => x.id);
                    table.ForeignKey(
                        name: "fk_invoices_appointments_appointment_id",
                        column: x => x.appointment_id,
                        principalTable: "appointments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_invoices_patients_patient_id",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    token_hash = table.Column<string>(type: "char(64)", fixedLength: true, maxLength: 64, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    expires_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    replaced_by_id = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_refresh_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "consultation_symptoms",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    consultation_id = table.Column<long>(type: "bigint", nullable: false),
                    symptom = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    duration_value = table.Column<short>(type: "smallint", nullable: true),
                    duration_unit = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    severity = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    sort_order = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_consultation_symptoms", x => x.id);
                    table.ForeignKey(
                        name: "fk_consultation_symptoms_consultations_consultation_id",
                        column: x => x.consultation_id,
                        principalTable: "consultations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "prescriptions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    prescription_number = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    consultation_id = table.Column<long>(type: "bigint", nullable: false),
                    patient_id = table.Column<long>(type: "bigint", nullable: false),
                    doctor_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    general_instructions = table.Column<string>(type: "text", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    follow_up_date = table.Column<DateOnly>(type: "date", nullable: true),
                    finalised_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    row_version = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_prescriptions", x => x.id);
                    table.ForeignKey(
                        name: "fk_prescriptions_consultations_consultation_id",
                        column: x => x.consultation_id,
                        principalTable: "consultations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_prescriptions_doctors_doctor_id",
                        column: x => x.doctor_id,
                        principalTable: "doctors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_prescriptions_patients_patient_id",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "vital_signs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    consultation_id = table.Column<long>(type: "bigint", nullable: false),
                    patient_id = table.Column<long>(type: "bigint", nullable: false),
                    recorded_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    recorded_by = table.Column<long>(type: "bigint", nullable: false),
                    temperature_c = table.Column<decimal>(type: "decimal(4,1)", precision: 4, scale: 1, nullable: true),
                    pulse_bpm = table.Column<short>(type: "smallint", nullable: true),
                    bp_systolic = table.Column<short>(type: "smallint", nullable: true),
                    bp_diastolic = table.Column<short>(type: "smallint", nullable: true),
                    respiratory_rate = table.Column<short>(type: "smallint", nullable: true),
                    spo2percent = table.Column<byte>(type: "tinyint unsigned", nullable: true),
                    weight_kg = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    height_cm = table.Column<decimal>(type: "decimal(5,1)", precision: 5, scale: 1, nullable: true),
                    bmi = table.Column<decimal>(type: "decimal(4,1)", precision: 4, scale: 1, nullable: true),
                    notes = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vital_signs", x => x.id);
                    table.ForeignKey(
                        name: "fk_vital_signs_consultations_consultation_id",
                        column: x => x.consultation_id,
                        principalTable: "consultations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vital_signs_patients_patient_id",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "invoice_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    invoice_id = table.Column<long>(type: "bigint", nullable: false),
                    description = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    item_type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    quantity = table.Column<short>(type: "smallint", nullable: false),
                    unit_price = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    line_total = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_invoice_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_invoice_items_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    invoice_id = table.Column<long>(type: "bigint", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    method = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    reference_no = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    paid_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    received_by = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payments", x => x.id);
                    table.CheckConstraint("CK_payments_amount", "`amount` > 0");
                    table.ForeignKey(
                        name: "fk_payments_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "prescription_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    prescription_id = table.Column<long>(type: "bigint", nullable: false),
                    medicine_id = table.Column<long>(type: "bigint", nullable: true),
                    medicine_name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    dosage = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    route = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    frequency_code = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    frequency_text = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    duration_value = table.Column<short>(type: "smallint", nullable: true),
                    duration_unit = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    quantity = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    instructions = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    sort_order = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_prescription_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_prescription_items_medicines_medicine_id",
                        column: x => x.medicine_id,
                        principalTable: "medicines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_prescription_items_prescriptions_prescription_id",
                        column: x => x.prescription_id,
                        principalTable: "prescriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateIndex(
                name: "IX_appointment_status_history_appointment_id",
                table: "appointment_status_history",
                column: "appointment_id");

            migrationBuilder.CreateIndex(
                name: "IX_appointments_date_status",
                table: "appointments",
                columns: new[] { "appointment_date", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_appointments_patient_id_date",
                table: "appointments",
                columns: new[] { "patient_id", "appointment_date" });

            migrationBuilder.CreateIndex(
                name: "UX_appointments_active_slot",
                table: "appointments",
                columns: new[] { "doctor_id", "appointment_date", "start_time", "active_slot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_appointments_queue_token",
                table: "appointments",
                columns: new[] { "doctor_id", "appointment_date", "queue_token" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_changed_at",
                table: "audit_logs",
                column: "changed_at");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_entity",
                table: "audit_logs",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "IX_clinical_record_revisions_consultation_id",
                table: "clinical_record_revisions",
                column: "consultation_id");

            migrationBuilder.CreateIndex(
                name: "UX_clinical_record_revisions_entity_rev",
                table: "clinical_record_revisions",
                columns: new[] { "entity_type", "entity_id", "revision_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_consultation_symptoms_consultation_id",
                table: "consultation_symptoms",
                column: "consultation_id");

            migrationBuilder.CreateIndex(
                name: "IX_consultations_doctor_id_started_at",
                table: "consultations",
                columns: new[] { "doctor_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "IX_consultations_patient_id_started_at",
                table: "consultations",
                columns: new[] { "patient_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "UX_consultations_appointment_id",
                table: "consultations",
                column: "appointment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_departments_name",
                table: "departments",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_doctor_leaves_doctor_id_dates",
                table: "doctor_leaves",
                columns: new[] { "doctor_id", "from_date", "to_date" });

            migrationBuilder.CreateIndex(
                name: "IX_doctor_schedules_doctor_id_day",
                table: "doctor_schedules",
                columns: new[] { "doctor_id", "day_of_week" });

            migrationBuilder.CreateIndex(
                name: "ix_doctor_specializations_specialization_id",
                table: "doctor_specializations",
                column: "specialization_id");

            migrationBuilder.CreateIndex(
                name: "IX_doctors_department_id_is_active",
                table: "doctors",
                columns: new[] { "department_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "UX_doctors_doctor_code",
                table: "doctors",
                column: "doctor_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_doctors_registration_number",
                table: "doctors",
                column: "registration_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoice_items_invoice_id",
                table: "invoice_items",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_patient_id",
                table: "invoices",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_status",
                table: "invoices",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "UX_invoices_appointment_id",
                table: "invoices",
                column: "appointment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_invoices_invoice_number",
                table: "invoices",
                column: "invoice_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_medicines_generic_name",
                table: "medicines",
                column: "generic_name");

            migrationBuilder.CreateIndex(
                name: "IX_medicines_name",
                table: "medicines",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_patient_allergies_patient_id",
                table: "patient_allergies",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_patient_conditions_patient_id",
                table: "patient_conditions",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "IX_patients_first_name",
                table: "patients",
                column: "first_name");

            migrationBuilder.CreateIndex(
                name: "IX_patients_last_name_first_name",
                table: "patients",
                columns: new[] { "last_name", "first_name" });

            migrationBuilder.CreateIndex(
                name: "IX_patients_phone",
                table: "patients",
                column: "phone");

            migrationBuilder.CreateIndex(
                name: "UX_patients_mrn",
                table: "patients",
                column: "mrn",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payments_invoice_id",
                table: "payments",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_payments_paid_at",
                table: "payments",
                column: "paid_at");

            migrationBuilder.CreateIndex(
                name: "ix_prescription_items_medicine_id",
                table: "prescription_items",
                column: "medicine_id");

            migrationBuilder.CreateIndex(
                name: "IX_prescription_items_prescription_id",
                table: "prescription_items",
                column: "prescription_id");

            migrationBuilder.CreateIndex(
                name: "ix_prescriptions_doctor_id",
                table: "prescriptions",
                column: "doctor_id");

            migrationBuilder.CreateIndex(
                name: "IX_prescriptions_patient_id",
                table: "prescriptions",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "UX_prescriptions_consultation_id",
                table: "prescriptions",
                column: "consultation_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_prescriptions_prescription_number",
                table: "prescriptions",
                column: "prescription_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_user_id",
                table: "refresh_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "UX_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_specializations_name",
                table: "specializations",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_users_doctor_id",
                table: "users",
                column: "doctor_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_users_username",
                table: "users",
                column: "username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vital_signs_consultation_id",
                table: "vital_signs",
                column: "consultation_id");

            migrationBuilder.CreateIndex(
                name: "IX_vital_signs_patient_id_recorded_at",
                table: "vital_signs",
                columns: new[] { "patient_id", "recorded_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "appointment_status_history");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "clinical_record_revisions");

            migrationBuilder.DropTable(
                name: "consultation_symptoms");

            migrationBuilder.DropTable(
                name: "doctor_leaves");

            migrationBuilder.DropTable(
                name: "doctor_schedules");

            migrationBuilder.DropTable(
                name: "doctor_specializations");

            migrationBuilder.DropTable(
                name: "id_sequences");

            migrationBuilder.DropTable(
                name: "invoice_items");

            migrationBuilder.DropTable(
                name: "patient_allergies");

            migrationBuilder.DropTable(
                name: "patient_conditions");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "prescription_items");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "vital_signs");

            migrationBuilder.DropTable(
                name: "specializations");

            migrationBuilder.DropTable(
                name: "invoices");

            migrationBuilder.DropTable(
                name: "medicines");

            migrationBuilder.DropTable(
                name: "prescriptions");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "consultations");

            migrationBuilder.DropTable(
                name: "appointments");

            migrationBuilder.DropTable(
                name: "doctors");

            migrationBuilder.DropTable(
                name: "patients");

            migrationBuilder.DropTable(
                name: "departments");
        }
    }
}
