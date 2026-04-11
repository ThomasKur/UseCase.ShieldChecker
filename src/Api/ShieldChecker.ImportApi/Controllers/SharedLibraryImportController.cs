using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web.Resource;
using ShieldChecker.DataAccess;
using ShieldChecker.DataAccess.Models;
using System.ComponentModel.DataAnnotations;

namespace ShieldChecker.ImportApi.Controllers
{
    /// <summary>
    /// Provides a bulk-upsert endpoint for importing Atomic Red Team tests into the shared library.
    /// Callers must hold the Import.SharedLibrary AppRole issued by ShieldChecker-ImportApi.
    /// </summary>
    [ApiController]
    [Authorize]
    [RequiredScope(RequiredScopesConfigurationKey = "AzureAd:Scopes")]
    [Route("api/sharedlibrary")]
    public class SharedLibraryImportController : ControllerBase
    {
        private readonly ShieldCheckerContext _context;
        private readonly ILogger<SharedLibraryImportController> _logger;

        public SharedLibraryImportController(ShieldCheckerContext context, ILogger<SharedLibraryImportController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Bulk-upsert shared test definitions. Existing entries (matched by ExternalId)
        /// are updated when the request contains Update=true; otherwise they are skipped.
        /// </summary>
        [HttpPost("import")]
        public async Task<IActionResult> Import([FromBody] SharedLibraryImportRequest request, CancellationToken ct)
        {
            if (request?.Tests == null || request.Tests.Count == 0)
                return BadRequest("No tests provided.");

            // Use the first UserInfo row as the system submitter (same as the import script)
            var systemUser = await _context.UserInfo.OrderBy(u => u.Id).FirstOrDefaultAsync(ct);
            if (systemUser == null)
                return BadRequest("Database not initialised: no UserInfo record found.");

            var stats = new ImportStats();

            foreach (var dto in request.Tests)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(dto.ExternalId))
                        dto.ExternalId = Guid.NewGuid().ToString();

                    var existing = await _context.SharedTestLibrary
                        .FirstOrDefaultAsync(e => e.ExternalId == dto.ExternalId, ct);

                    if (existing != null)
                    {
                        if (!request.Update)
                        {
                            stats.Skipped++;
                            continue;
                        }

                        existing.Name = Truncate(dto.Name, 150);
                        existing.MitreTechnique = Truncate(dto.MitreTechnique, 16);
                        existing.Description = dto.Description ?? string.Empty;
                        existing.ScriptTest = dto.ScriptTest ?? string.Empty;
                        existing.ScriptPrerequisites = string.Empty;
                        existing.ScriptCleanup = string.Empty;
                        existing.OperatingSystem = dto.OperatingSystem;
                        existing.ExecutorSystemType = dto.ExecutorSystemType;
                        existing.ExecutorUserType = ExecutorUserType.System;
                        existing.ElevationRequired = false;

                        stats.Updated++;
                    }
                    else
                    {
                        var entry = new SharedTestDefinition
                        {
                            Name = Truncate(dto.Name, 150),
                            MitreTechnique = Truncate(dto.MitreTechnique, 16),
                            Description = dto.Description ?? string.Empty,
                            ExpectedAlertTitle = "Unknown",
                            ScriptTest = dto.ScriptTest ?? string.Empty,
                            ScriptPrerequisites = string.Empty,
                            ScriptCleanup = string.Empty,
                            ElevationRequired = false,
                            OperatingSystem = dto.OperatingSystem,
                            ExecutorSystemType = dto.ExecutorSystemType,
                            ExecutorUserType = ExecutorUserType.System,
                            ExternalId = dto.ExternalId,
                            Status = SharedTestStatus.Approved,
                            SubmittedBy = systemUser,
                            SubmittedAt = DateTime.UtcNow
                        };
                        _context.SharedTestLibrary.Add(entry);
                        stats.Inserted++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing test with ExternalId {ExternalId}.", dto.ExternalId);
                    stats.Errors++;
                }
            }

            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Import complete – Inserted: {Inserted}, Updated: {Updated}, Skipped: {Skipped}, Errors: {Errors}",
                stats.Inserted, stats.Updated, stats.Skipped, stats.Errors);

            return Ok(stats);
        }

        private static string Truncate(string? value, int maxLength)
            => string.IsNullOrEmpty(value) ? string.Empty
                : value.Length <= maxLength ? value
                : value[..maxLength];
    }

    // ── Request / Response DTOs ────────────────────────────────────────────────

    public sealed class SharedLibraryImportRequest
    {
        /// <summary>When true, existing Approved entries are updated with the supplied values.</summary>
        public bool Update { get; set; }

        [Required]
        public List<SharedTestImportDto> Tests { get; set; } = new();
    }

    public sealed class SharedTestImportDto
    {
        [StringLength(150)]
        public required string Name { get; set; }

        [StringLength(16)]
        public string MitreTechnique { get; set; } = string.Empty;

        public string? Description { get; set; }
        public string? ScriptTest { get; set; }

        /// <summary>
        /// Atomic Red Team GUID (auto_generated_guid) used as a stable deduplication key.
        /// If omitted a new GUID is generated.
        /// </summary>
        [StringLength(256)]
        public string? ExternalId { get; set; }

        public ShieldChecker.DataAccess.Models.OperatingSystem OperatingSystem { get; set; }
        public ExecutorSystemType ExecutorSystemType { get; set; }
    }

    public sealed class ImportStats
    {
        public int Inserted { get; set; }
        public int Updated { get; set; }
        public int Skipped { get; set; }
        public int Errors { get; set; }
    }
}
