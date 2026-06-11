using Maliev.JobService.Infrastructure.Persistence;
using Maliev.JobService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace Maliev.JobService.Tests.Unit;

public sealed class MigrationDiscoveryTests
{
    [Fact]
    public void JobDbContext_Migrations_IncludesProductionPlanningHoldsMigration()
    {
        var options = new DbContextOptionsBuilder<JobDbContext>()
            .UseNpgsql("Host=localhost;Database=jobservice;Username=postgres;Password=postgres")
            .Options;

        using var context = new JobDbContext(options);
        var migrationsAssembly = context.GetService<IMigrationsAssembly>();

        Assert.True(
            migrationsAssembly.Migrations.ContainsKey("20260506090000_AddProductionPlanningHolds"),
            "The production planning holds migration must be discoverable so startup migration creates its table.");
    }

    [Fact]
    public void JobDbContext_ProductionPlanningHoldsMigration_DoesNotCreatePhysicalXminColumn()
    {
        var options = new DbContextOptionsBuilder<JobDbContext>()
            .UseNpgsql("Host=localhost;Database=jobservice;Username=postgres;Password=postgres")
            .Options;

        using var context = new JobDbContext(options);
        var migrator = context.GetService<IMigrator>();

        var script = migrator.GenerateScript(
            fromMigration: "20260404000000_AddSchedulingIndexes",
            toMigration: "20260506090000_AddProductionPlanningHolds");

        Assert.Contains("CREATE TABLE public.production_planning_holds", script);
        Assert.DoesNotContain("\"xmin\" xid", script);
    }

    [Fact]
    public void JobDbContext_Model_HasUniqueOrderItemJobIndex()
    {
        var options = new DbContextOptionsBuilder<JobDbContext>()
            .UseNpgsql("Host=localhost;Database=jobservice;Username=postgres;Password=postgres")
            .Options;

        using var context = new JobDbContext(options);

        var jobEntity = context.Model.FindEntityType(typeof(Job));
        Assert.NotNull(jobEntity);

        var orderId = jobEntity.FindProperty(nameof(Job.OrderId));
        var orderItemId = jobEntity.FindProperty(nameof(Job.OrderItemId));
        Assert.NotNull(orderId);
        Assert.NotNull(orderItemId);

        var index = jobEntity.FindIndex([orderId, orderItemId]);
        Assert.NotNull(index);
        Assert.True(index.IsUnique);
    }
}
