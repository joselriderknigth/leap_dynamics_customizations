using System;
using Microsoft.Extensions.Configuration;

public class SeedDataConfig
{
    public Guid OrganizationId { get; init; }
    public Guid HospitalId { get; init; }
    public Guid SupplierId { get; init; }
    public Guid PhysicianId { get; init; }
    public Guid ProcedureId { get; init; }
    public Guid ProductId { get; init; }
    public Guid SubjectId { get; init; }
    public Guid UnitGroupId { get; init; }
    public Guid UnitId { get; init; }

    public static SeedDataConfig Load(IConfiguration config) =>
        new()
        {
            OrganizationId = Guid.Parse(
                config["ORGANIZATION_ID"] ?? throw new Exception("Missing ORGANIZATION_ID")
            ),
            HospitalId = Guid.Parse(
                config["DEFAULT_HOSPITAL_ID"] ?? throw new Exception("Missing DEFAULT_HOSPITAL_ID")
            ),
            PhysicianId = Guid.Parse(
                config["DEFAULT_PHYSICIAN_ID"]
                    ?? throw new Exception("Missing DEFAULT_PHYSICIAN_ID")
            ),
            ProcedureId = Guid.Parse(
                config["DEFAULT_PROCEDURE_ID"]
                    ?? throw new Exception("Missing DEFAULT_PROCEDURE_ID")
            ),
            SubjectId = Guid.Parse(
                config["DEFAULT_SUBJECT_ID"] ?? throw new Exception("Missing DEFAULT_SUBJECT_ID")
            ),
            UnitGroupId = Guid.Parse(
                config["DEFAULT_UNIT_GROUP_ID"]
                    ?? throw new Exception("Missing DEFAULT_UNIT_GROUP_ID")
            ),
            UnitId = Guid.Parse(
                config["DEFAULT_UNIT_ID"] ?? throw new Exception("Missing DEFAULT_UNIT_ID")
            ),
        };
}
