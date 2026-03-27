using EasyLotteryAPI.Services;
using EasyLotteryDomain.Models.Overtime;
using Microsoft.AspNetCore.Mvc;

namespace EasyLotteryAPI.Controllers
{
    public sealed class OvertimeSupportEventRequest
    {
        public OvertimeSupportSource Source { get; set; }

        public string SourceLabel { get; set; } = "";

        public string DisplayName { get; set; } = "";

        public string Message { get; set; } = "";

        public decimal? Amount { get; set; }

        public string AmountDisplay { get; set; } = "";

        public string Currency { get; set; } = "TWD";

        public string AvatarUrl { get; set; } = "";

        public string Color { get; set; } = "#ff85b4";

        public string? ExternalId { get; set; }
    }

    [ApiController]
    [Route("api/overtime")]
    public class OvertimeController : ControllerBase
    {
        private readonly OvertimeFeedStore _store;

        public OvertimeController(OvertimeFeedStore store)
        {
            _store = store;
        }

        [HttpGet("events")]
        public ActionResult<IReadOnlyList<OvertimeSupportEvent>> GetEvents()
        {
            return Ok(_store.Snapshot());
        }

        [HttpPost("superchat")]
        public ActionResult<OvertimeSupportEvent> PostSuperChat([FromBody] OvertimeSupportEventRequest request)
        {
            var entry = _store.Add(new OvertimeSupportEvent
            {
                Source = OvertimeSupportSource.SuperChat,
                SourceLabel = string.IsNullOrWhiteSpace(request.SourceLabel) ? "YouTube SuperChat" : request.SourceLabel,
                DisplayName = request.DisplayName,
                Message = request.Message,
                Amount = request.Amount,
                AmountDisplay = request.AmountDisplay,
                Currency = request.Currency,
                AvatarUrl = request.AvatarUrl,
                Color = request.Color,
                ExternalId = request.ExternalId
            });

            return Ok(entry);
        }

        [HttpPost("ecpay")]
        public ActionResult<OvertimeSupportEvent> PostEcpay([FromBody] OvertimeSupportEventRequest request)
        {
            var entry = _store.Add(new OvertimeSupportEvent
            {
                Source = OvertimeSupportSource.EcpayDonate,
                SourceLabel = string.IsNullOrWhiteSpace(request.SourceLabel) ? "ECPay Donate" : request.SourceLabel,
                DisplayName = request.DisplayName,
                Message = request.Message,
                Amount = request.Amount,
                AmountDisplay = request.AmountDisplay,
                Currency = request.Currency,
                AvatarUrl = request.AvatarUrl,
                Color = request.Color,
                ExternalId = request.ExternalId
            });

            return Ok(entry);
        }

        [HttpDelete("events")]
        public IActionResult Clear()
        {
            _store.Clear();
            return NoContent();
        }
    }
}
