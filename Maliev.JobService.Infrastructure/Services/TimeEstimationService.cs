using Maliev.JobService.Application.Abstractions;

namespace Maliev.JobService.Infrastructure.Services;

/// <summary>
/// Estimates manufacturing time using technology-specific throughput rates.
/// </summary>
public class TimeEstimationService : ITimeEstimationService
{
    // Minutes per cm³ by technology (conservative estimates matching PricingService MachineCapacityConfig)
    private static readonly Dictionary<string, decimal> MinutesPerCm3 = new(StringComparer.OrdinalIgnoreCase)
    {
        ["FDM"] = 12m,
        ["FFF"] = 12m,
        ["SLA"] = 8m,
        ["SLA_DLP"] = 8m,
        ["MSLA"] = 8m,
        ["DLP"] = 8m,
        ["SLS"] = 6m,
        ["MJF"] = 5m,
        ["CNC"] = 15m,
        ["CNC_MILL"] = 15m,
        ["CNC_TURN"] = 12m,
    };

    // Setup time in minutes by technology
    private static readonly Dictionary<string, int> SetupMinutes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["FDM"] = 15,
        ["FFF"] = 15,
        ["SLA"] = 30,
        ["SLA_DLP"] = 30,
        ["MSLA"] = 30,
        ["DLP"] = 30,
        ["SLS"] = 20,
        ["MJF"] = 20,
        ["CNC"] = 60,
        ["CNC_MILL"] = 60,
        ["CNC_TURN"] = 45,
    };

    /// <inheritdoc />
    public int EstimatePrintTimeMinutes(string technology, decimal volumeCm3)
    {
        var rate = MinutesPerCm3.GetValueOrDefault(technology.ToUpperInvariant(), 10m);
        return Math.Max(30, (int)Math.Ceiling(volumeCm3 * rate));
    }

    /// <inheritdoc />
    public int EstimateSetupTimeMinutes(string technology) =>
        SetupMinutes.GetValueOrDefault(technology.ToUpperInvariant(), 20);
}
