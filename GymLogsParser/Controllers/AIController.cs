using Core.Models;
using Infrastructure.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GymLogsParser.Controllers;

[ApiController]
[Route("api/ai")]
public sealed class AiController(IDeepSeekService deepSeek) : ControllerBase
{
    [HttpPost("parse-workout")]
    public async Task<ActionResult<ParseWorkoutResponse>> ParseWorkout([FromBody] ParseWorkoutRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Text)) { return BadRequest(new { error = "Workout text is required." }); }

        try
        {
            var result = await deepSeek.ParseWorkoutAsync( request.Text, request.BarbellWeightsArePerSide, cancellationToken);
            return Ok(new ParseWorkoutResponse
            {
                Workout = result.Workout,
                Usage = new AiUsageResponse
                {
                    CacheHit = result.CacheHit,
                    CacheHitTokens = result.CacheHitTokens,
                    CacheMissTokens = result.CacheMissTokens,
                    InputTokens = result.InputTokens,
                    OutputTokens = result.OutputTokens
                }
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {  throw; }
        catch (HttpRequestException ex) { return StatusCode(StatusCodes.Status502BadGateway, new{error = "The AI provider request failed.",detail = ex.Message}); }
        catch (Exception ex) {  return StatusCode( StatusCodes.Status500InternalServerError, new { error = "Workout parsing failed.", detail = ex.Message }); }
    }
}

public sealed class ParseWorkoutRequest
{
    public string Text { get; set; } = string.Empty;
    public bool BarbellWeightsArePerSide { get; set; }
}

public sealed class ParseWorkoutResponse
{
    public WorkoutLog Workout { get; set; } = new();
    public AiUsageResponse Usage { get; set; } = new();
}

public sealed class AiUsageResponse
{
    public bool CacheHit { get; set; }
    public int CacheHitTokens { get; set; }
    public int CacheMissTokens { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
}