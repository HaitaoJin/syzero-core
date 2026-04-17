using Microsoft.AspNetCore.Mvc;

namespace SyZero.AspNetCore.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class HealthController : SyZeroController
    {
        [HttpGet]
        public IActionResult Index()
        {
            return Ok();
        }
    }
}
