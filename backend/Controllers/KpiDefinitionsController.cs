using backend.DTOs;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/kpi-definitions")]
    [Authorize]
    public class KpiDefinitionsController : ControllerBase
    {
        private readonly IKpiDefinitionService _service;

        public KpiDefinitionsController(IKpiDefinitionService service)
        {
            _service = service;
        }

        // GET /api/kpi-definitions
        [HttpGet]
        public async Task<ActionResult<List<KpiDefinitionDto>>> GetAll()
        {
            var result = await _service.GetAllAsync();
            return Ok(result);
        }

        // GET /api/kpi-definitions/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<KpiDefinitionDto>> GetById(int id)
        {
            var result = await _service.GetByIdAsync(id);
            return result is null ? NotFound() : Ok(result);
        }

        // POST /api/kpi-definitions
        [HttpPost]
        [Authorize(Policy = "AdminOnly")]
        public async Task<ActionResult<KpiDefinitionDto>> Create([FromBody] UpsertKpiDefinitionDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var result = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        // PUT /api/kpi-definitions/{id}
        [HttpPut("{id}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<ActionResult<KpiDefinitionDto>> Update(int id, [FromBody] UpsertKpiDefinitionDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var result = await _service.UpdateAsync(id, dto);
            return result is null ? NotFound() : Ok(result);
        }

        // DELETE /api/kpi-definitions/{id}
        [HttpDelete("{id}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _service.DeleteAsync(id);
            return deleted ? NoContent() : NotFound();
        }
    }
}
