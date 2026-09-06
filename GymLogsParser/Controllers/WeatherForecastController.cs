using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymLogsParser.Controllers;

[ApiController]
[Route("api/auth")]
public class WeatherForecastController : ControllerBase
{
    
    [AllowAnonymous]
    [HttpPost("login")]
    public IEnumerable<int> Get()
    {
        return(Enumerable.Range(1, 5));
    }
}