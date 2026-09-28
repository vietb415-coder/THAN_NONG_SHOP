using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using THAN_NONG_SHOP.Data;
namespace THAN_NONG_SHOP.Controllers;
public sealed class ReviewMediaController(THAN_NONG_SHOP_DbContext db,IWebHostEnvironment env):Controller
{
    [HttpGet]
    public async Task<IActionResult> File(int id,CancellationToken ct) {
        var media=await db.ReviewMedia.AsNoTracking().FirstOrDefaultAsync(m=>m.Id==id,ct);if(media==null)return NotFound();
        var path=Path.Combine(env.ContentRootPath,"App_Data","review-media",Path.GetFileName(media.FileName));
        if(!System.IO.File.Exists(path))return NotFound();
        Response.Headers.XContentTypeOptions="nosniff";
        return PhysicalFile(path,media.ContentType,enableRangeProcessing:true);
    }
}
