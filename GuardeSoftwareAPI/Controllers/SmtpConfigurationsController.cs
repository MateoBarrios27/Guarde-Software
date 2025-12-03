using Microsoft.AspNetCore.Mvc;
using GuardeSoftwareAPI.Dao; // Ajusta namespace
using GuardeSoftwareAPI.Dtos.Communication;

[ApiController]
[Route("api/[controller]")]
public class SmtpConfigurationsController : ControllerBase
{
    private readonly CommunicationDao _dao; // Reutilizamos el DAO o crea uno nuevo

    public SmtpConfigurationsController(AccessDB accessDB)
    {
        _dao = new CommunicationDao(accessDB);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _dao.GetAllSmtpConfigsAsync();
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SmtpConfigurationDto dto)
    {
        var newId = await _dao.CreateSmtpConfigAsync(dto);
        dto.Id = newId;
        return Ok(dto);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] SmtpConfigurationDto dto)
    {
        dto.Id = id;
        await _dao.UpdateSmtpConfigAsync(dto);
        return Ok(dto);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _dao.DeleteSmtpConfigAsync(id);
        return NoContent();
    }
}