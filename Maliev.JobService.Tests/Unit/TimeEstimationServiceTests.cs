using Maliev.JobService.Infrastructure.Services;
using Xunit;

namespace Maliev.JobService.Tests.Unit;

public class TimeEstimationServiceTests
{
    private readonly TimeEstimationService _sut = new();

    // ── EstimatePrintTimeMinutes ──────────────────────────────────────────────

    [Theory]
    [InlineData("FDM", 10, 120)]   // 10 cm³ × 12 min/cm³ = 120
    [InlineData("SLA", 10, 80)]    // 10 cm³ × 8  min/cm³ = 80
    [InlineData("SLS", 10, 60)]    // 10 cm³ × 6  min/cm³ = 60
    [InlineData("MJF", 10, 50)]    // 10 cm³ × 5  min/cm³ = 50
    [InlineData("CNC", 10, 150)]   // 10 cm³ × 15 min/cm³ = 150
    [InlineData("CNC_MILL", 10, 150)]
    [InlineData("CNC_TURN", 10, 120)]   // 10 cm³ × 12 min/cm³ = 120
    [InlineData("SLA_DLP", 10, 80)]
    [InlineData("MSLA", 10, 80)]
    [InlineData("DLP", 10, 80)]
    [InlineData("FFF", 10, 120)]
    public void EstimatePrintTimeMinutes_KnownTechnology_ReturnsCorrectValue(
        string technology, decimal volumeCm3, int expected)
    {
        var result = _sut.EstimatePrintTimeMinutes(technology, volumeCm3);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void EstimatePrintTimeMinutes_UnknownTechnology_UsesDefaultRate()
    {
        // Unknown → falls back to 10 min/cm³; 10 cm³ × 10 = 100 min
        var result = _sut.EstimatePrintTimeMinutes("LASER_EXOTIC", 10m);
        Assert.Equal(100, result);
    }

    [Fact]
    public void EstimatePrintTimeMinutes_VerySmallVolume_EnforcesMinimumFloor()
    {
        // 0.1 cm³ × 12 = 1.2 → ceil = 2, but floor is 30
        var result = _sut.EstimatePrintTimeMinutes("FDM", 0.1m);
        Assert.Equal(30, result);
    }

    [Fact]
    public void EstimatePrintTimeMinutes_ZeroVolume_Returns30()
    {
        var result = _sut.EstimatePrintTimeMinutes("FDM", 0m);
        Assert.Equal(30, result);
    }

    [Fact]
    public void EstimatePrintTimeMinutes_IsCaseInsensitive()
    {
        var lower = _sut.EstimatePrintTimeMinutes("fdm", 10m);
        var upper = _sut.EstimatePrintTimeMinutes("FDM", 10m);
        Assert.Equal(upper, lower);
    }

    [Fact]
    public void EstimatePrintTimeMinutes_FractionalVolume_CeilsResult()
    {
        // 1.5 cm³ × 12 min/cm³ = 18 (exact) → 18
        var result = _sut.EstimatePrintTimeMinutes("FDM", 1.5m);
        Assert.Equal(30, result); // 18 < 30 floor
    }

    [Fact]
    public void EstimatePrintTimeMinutes_LargeVolume_ExceedsFloor()
    {
        // 100 cm³ × 12 = 1200 min
        var result = _sut.EstimatePrintTimeMinutes("FDM", 100m);
        Assert.Equal(1200, result);
    }

    // ── EstimateSetupTimeMinutes ──────────────────────────────────────────────

    [Theory]
    [InlineData("FDM", 15)]
    [InlineData("FFF", 15)]
    [InlineData("SLA", 30)]
    [InlineData("SLA_DLP", 30)]
    [InlineData("MSLA", 30)]
    [InlineData("DLP", 30)]
    [InlineData("SLS", 20)]
    [InlineData("MJF", 20)]
    [InlineData("CNC", 60)]
    [InlineData("CNC_MILL", 60)]
    [InlineData("CNC_TURN", 45)]
    public void EstimateSetupTimeMinutes_KnownTechnology_ReturnsCorrectValue(
        string technology, int expected)
    {
        var result = _sut.EstimateSetupTimeMinutes(technology);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void EstimateSetupTimeMinutes_UnknownTechnology_Returns20()
    {
        var result = _sut.EstimateSetupTimeMinutes("WIRE_EDM");
        Assert.Equal(20, result);
    }

    [Fact]
    public void EstimateSetupTimeMinutes_IsCaseInsensitive()
    {
        Assert.Equal(
            _sut.EstimateSetupTimeMinutes("CNC"),
            _sut.EstimateSetupTimeMinutes("cnc"));
    }
}
