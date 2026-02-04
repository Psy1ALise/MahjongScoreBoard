using Microsoft.AspNetCore.Mvc;
using MahjongScoreBoard.Api.Models;

namespace MahjongScoreBoard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class YakuController : ControllerBase
{
    [HttpGet]
    public ActionResult<IEnumerable<YakuResponse>> GetAllYaku()
    {
        var yakuList = Enum.GetValues<Yaku>()
            .Select(y => new YakuResponse(
                y.ToString(),
                y.GetHanValue(),
                y.GetDescription()
            ));

        return Ok(yakuList);
    }
}

public record YakuResponse(
    string Name,
    int HanValue,
    string Description
);
