namespace THAN_NONG_SHOP.Services;
public static class ReviewUploads
{
    public static (string Extension,string ContentType,bool Video)? Inspect(IFormFile file)
    {
        var ext=Path.GetExtension(file.FileName).ToLowerInvariant();var video=ext==".mp4";
        if(file.Length<=0 || file.Length>(video?20L:5L)*1024*1024)return null;
        using var stream=file.OpenReadStream();Span<byte> header=stackalloc byte[12];var n=stream.Read(header);
        var type=ext switch {
            ".jpg" or ".jpeg" when n>=3 && header[0]==255 && header[1]==216 && header[2]==255 =>"image/jpeg",
            ".png" when n>=8 && header[..8].SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})=>"image/png",
            ".webp" when n>=12 && header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8)=>"image/webp",
            ".mp4" when n>=12 && header[4..8].SequenceEqual("ftyp"u8)=>"video/mp4",
            _=>null};
        return type==null || !string.Equals(type,file.ContentType,StringComparison.OrdinalIgnoreCase)?null:(ext,type,video);
    }
}
