/*
 * File: BbAnwController.cs
 * Handles BB ANW KPI platform data including headers and node-level KPI records.
 */

using System.Linq;
using System.Threading.Tasks;
using backend.Data;
using backend.DTOs;
using backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using backend.Helpers.Authorization;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/bb-anw")]
    [Authorize]
    public class BbAnwController : ControllerBase
    {
        // Database context
        private readonly AppDbContext _context;

        // Authorization service for page-level permissions
        private readonly IAuthorizationService _authorizationService;

        // Page identifier for BB ANW platform
        private const int PageId = 3;

        public BbAnwController(AppDbContext context, IAuthorizationService authorizationService)
        {
            _context = context;
            _authorizationService = authorizationService;
        }

        // =========================================================
        // PLATFORM KPI PAGE (FULL DATA: HEADERS + NODES)
        // =========================================================

        // GET: /api/bb-anw/headers
        // Retrieve simplified header rows for the admin page
        [HttpGet("headers")]
        public async Task<IActionResult> GetHeaders()
        {
            var authResult = await _authorizationService.AuthorizeAsync(User, PageId, "ViewPagePolicy");
            if (!authResult.Succeeded) return Forbid();

            var data = await _context.BbAnwKpis
                .AsNoTracking()
                .OrderBy(x => x.Id)
                .ToListAsync();

            var headers = data.Select(ToHeaderDto).ToList();

            return Ok(headers);
        }

        // GET: /api/bb-anw
        // Retrieve full KPI records including node data
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int? month, [FromQuery] int? year)
        {
            var authResult = await _authorizationService.AuthorizeAsync(User, PageId, "ViewPagePolicy");
            if (!authResult.Succeeded) return Forbid();

            var query = _context.BbAnwKpis
                .AsNoTracking()
                .AsQueryable();

            query = month.HasValue && year.HasValue
                ? query.Include(x => x.Nodes.Where(n => n.Month == month.Value && n.Year == year.Value))
                : query.Include(x => x.Nodes);

            var data = await query
                .OrderBy(x => x.Id)
                .ToListAsync();

            var result = data.Select(ToDto).ToList();

            return Ok(result);
        }

        // GET: /api/bb-anw/{id}
        // Retrieve a single KPI record
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var authResult = await _authorizationService.AuthorizeAsync(User, PageId, "ViewPagePolicy");
            if (!authResult.Succeeded) return Forbid();

            var item = await _context.BbAnwKpis
                .AsNoTracking()
                .Include(x => x.Nodes)
                .Where(x => x.Id == id)
                .FirstOrDefaultAsync();

            if (item == null) return NotFound();
            return Ok(ToDto(item));
        }

        // =========================================================
        // CREATE KPI ROWS
        // Supports both platform and admin routes
        // =========================================================

        [HttpPost("add")]
        public async Task<IActionResult> Add([FromBody] BbAnwDto dto)
        {
            var authResult = await CanEditAsync();
            if (!authResult.Succeeded) return Forbid();

            if (!ModelState.IsValid) return BadRequest(ModelState);

            var entity = new BbAnwKpi
            {
                NetworkEngineerKpi = (dto.NetworkEngineerKpi ?? string.Empty).Trim(),
                Division = string.IsNullOrWhiteSpace(dto.Division) ? null : dto.Division.Trim(),
                Section = string.IsNullOrWhiteSpace(dto.Section) ? null : dto.Section.Trim(),
                KpiPercent = dto.KpiPercent,
                Nodes = MapNodes(dto.Nodes)
            };

            if (string.IsNullOrWhiteSpace(entity.NetworkEngineerKpi))
                return BadRequest("NetworkEngineerKpi is required.");

            _context.BbAnwKpis.Add(entity);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, ToDto(entity));
        }

        [HttpPost("add-header")]
        public async Task<IActionResult> AddHeader([FromBody] BbAnwHeaderDto dto)
        {
            var authResult = await CanEditAsync();
            if (!authResult.Succeeded) return Forbid();

            if (!ModelState.IsValid) return BadRequest(ModelState);

            var entity = new BbAnwKpi
            {
                NetworkEngineerKpi = (dto.NetworkEngineerKpi ?? string.Empty).Trim(),
                Division = string.IsNullOrWhiteSpace(dto.Division) ? null : dto.Division.Trim(),
                Section = string.IsNullOrWhiteSpace(dto.Section) ? null : dto.Section.Trim(),
                KpiPercent = dto.KpiPercent
            };

            if (string.IsNullOrWhiteSpace(entity.NetworkEngineerKpi))
                return BadRequest("NetworkEngineerKpi is required.");

            _context.BbAnwKpis.Add(entity);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, ToHeaderDto(entity));
        }

        // =========================================================
        // UPDATE KPI ROWS
        // Supports both platform and admin routes
        // =========================================================

        [HttpPut("update/{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] BbAnwDto dto)
        {
            var authResult = await CanEditAsync();
            if (!authResult.Succeeded) return Forbid();

            if (!ModelState.IsValid) return BadRequest(ModelState);

            var entity = await _context.BbAnwKpis
                .Include(x => x.Nodes)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (entity == null) return NotFound();

            entity.NetworkEngineerKpi = (dto.NetworkEngineerKpi ?? string.Empty).Trim();
            entity.Division = string.IsNullOrWhiteSpace(dto.Division) ? null : dto.Division.Trim();
            entity.Section = string.IsNullOrWhiteSpace(dto.Section) ? null : dto.Section.Trim();
            entity.KpiPercent = dto.KpiPercent;

            if (dto.Nodes != null)
            {
                var dtoNodes = MapNodes(dto.Nodes);
                foreach (var dtoNode in dtoNodes)
                {
                    var existing = entity.Nodes.FirstOrDefault(n =>
                        n.NodeCode == dtoNode.NodeCode &&
                        n.Month == dtoNode.Month &&
                        n.Year == dtoNode.Year);

                    if (existing != null)
                    {
                        existing.UnavailableMinutes = dtoNode.UnavailableMinutes;
                        existing.TotalMinutes = dtoNode.TotalMinutes;
                        existing.TotalNodes = dtoNode.TotalNodes;
                    }
                    else
                    {
                        entity.Nodes.Add(dtoNode);
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(entity.NetworkEngineerKpi))
                return BadRequest("NetworkEngineerKpi is required.");

            await _context.SaveChangesAsync();

            return Ok(ToDto(entity));
        }

        [HttpPut("update-header/{id:int}")]
        public async Task<IActionResult> UpdateHeader(int id, [FromBody] BbAnwHeaderDto dto)
        {
            var authResult = await CanEditAsync();
            if (!authResult.Succeeded) return Forbid();

            if (!ModelState.IsValid) return BadRequest(ModelState);

            var entity = await _context.BbAnwKpis.FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null) return NotFound();

            entity.NetworkEngineerKpi = (dto.NetworkEngineerKpi ?? string.Empty).Trim();
            entity.Division = string.IsNullOrWhiteSpace(dto.Division) ? null : dto.Division.Trim();
            entity.Section = string.IsNullOrWhiteSpace(dto.Section) ? null : dto.Section.Trim();
            entity.KpiPercent = dto.KpiPercent;

            if (string.IsNullOrWhiteSpace(entity.NetworkEngineerKpi))
                return BadRequest("NetworkEngineerKpi is required.");

            await _context.SaveChangesAsync();

            return Ok(ToHeaderDto(entity));
        }

        // =========================================================
        // DELETE KPI ROW
        // =========================================================

        [HttpDelete("delete/{id:int}")]
        [HttpDelete("delete-header/{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var authResult = await CanEditAsync();
            if (!authResult.Succeeded) return Forbid();

            var entity = await _context.BbAnwKpis.FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null) return NotFound();

            _context.BbAnwKpis.Remove(entity);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Deleted" });
        }

        private async Task<Microsoft.AspNetCore.Authorization.AuthorizationResult> CanEditAsync()
        {
            if (User.IsInRole("Admin") || User.IsInRole("SuperAdmin"))
                return Microsoft.AspNetCore.Authorization.AuthorizationResult.Success();

            return await _authorizationService.AuthorizeAsync(User, PageId, "EditPlatformKpiPolicy");
        }

        private static BbAnwDto ToDto(BbAnwKpi x) => new()
        {
            Id = x.Id,
            NetworkEngineerKpi = x.NetworkEngineerKpi,
            Division = x.Division,
            Section = x.Section,
            KpiPercent = x.KpiPercent,
            Nodes = x.Nodes.Select(n => new BbAnwNodeDto
            {
                NodeCode = n.NodeCode,
                UnavailableMinutes = n.UnavailableMinutes,
                TotalMinutes = n.TotalMinutes,
                TotalNodes = n.TotalNodes,
                Month = n.Month,
                Year = n.Year
            }).ToList()
        };

        private static BbAnwHeaderDto ToHeaderDto(BbAnwKpi x) => new()
        {
            Id = x.Id,
            NetworkEngineerKpi = x.NetworkEngineerKpi,
            Division = x.Division,
            Section = x.Section,
            KpiPercent = x.KpiPercent
        };

        private static List<BbAnwKpiNode> MapNodes(List<BbAnwNodeDto>? nodes)
        {
            return (nodes ?? new List<BbAnwNodeDto>())
                .Where(n => !string.IsNullOrWhiteSpace(n.NodeCode))
                .Select(n => new BbAnwKpiNode
                {
                    NodeCode = n.NodeCode.Trim(),
                    UnavailableMinutes = n.UnavailableMinutes,
                    TotalMinutes = n.TotalMinutes,
                    TotalNodes = n.TotalNodes,
                    Month = n.Month,
                    Year = n.Year
                })
                .ToList();
        }
    }
}
