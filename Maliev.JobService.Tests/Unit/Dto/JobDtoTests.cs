using System.Text.Json;
using Maliev.JobService.Api.DTOs;
using Maliev.JobService.Domain.Entities;
using Xunit;

namespace Maliev.JobService.Tests.Unit.Dto;

public sealed class JobDtoTests
{
    [Fact]
    public void FromEntity_ExposesLockedProductionSnapshotsForJobTicket()
    {
        const string materialSnapshotJson = """{"materialId":"mat-pa12","materialName":"PA12 Nylon"}""";
        const string configurationSnapshotJson = """{"quantity":2,"finish":"dyed-black"}""";
        var job = new Job
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            OrderId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            OrderItemId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            MaterialId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            MaterialSnapshotJson = materialSnapshotJson,
            ConfigurationSnapshotJson = configurationSnapshotJson,
            Technology = "SLS",
            VolumeCm3 = 12.5m,
            EstimatedPrintTimeMinutes = 90,
            Priority = 3,
            Status = JobStatus.Pending,
            CreatedAt = new DateTime(2026, 6, 18, 8, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 6, 18, 8, 5, 0, DateTimeKind.Utc)
        };

        var dto = JobDto.FromEntity(job);
        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(materialSnapshotJson, dto.MaterialSnapshotJson);
        Assert.Equal(configurationSnapshotJson, dto.ConfigurationSnapshotJson);
        Assert.Contains("\"materialSnapshotJson\":", json, StringComparison.Ordinal);
        Assert.Contains("\"configurationSnapshotJson\":", json, StringComparison.Ordinal);
    }
}
