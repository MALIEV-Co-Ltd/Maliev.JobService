namespace Maliev.JobService.Application.Abstractions;

/// <summary>
/// Estimates manufacturing time for a given technology and part volume.
/// </summary>
public interface ITimeEstimationService
{
    /// <summary>
    /// Estimates the print/machining time for a part in minutes.
    /// </summary>
    /// <param name="technology">Manufacturing technology code (e.g. "FDM", "SLA", "CNC").</param>
    /// <param name="volumeCm3">Part volume in cubic centimetres.</param>
    int EstimatePrintTimeMinutes(string technology, decimal volumeCm3);

    /// <summary>
    /// Estimates the setup time required before production can begin in minutes.
    /// </summary>
    /// <param name="technology">Manufacturing technology code.</param>
    int EstimateSetupTimeMinutes(string technology);
}
