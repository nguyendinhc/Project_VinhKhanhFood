using System;
using System.Collections.Generic;
using System.IO;

namespace VinhKhanhFood.Models;

public partial class Poi
{
    public int Poiid { get; set; }
    public string Name { get; set; } = null!;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int? Radius { get; set; }
    public string? Thumbnail { get; set; }
    public string? Status { get; set; }
    public string? LocalThumbnailPath { get; set; }

    // --- THÊM 2 DÒNG NÀY VÀO ĐỂ HẾT LỖI ĐỎ ---
    public string Description { get; set; } = "Thông tin địa điểm";
    public string Introduction { get; set; } = "Nội dung thuyết minh đang được tải...";

    // Dòng này để tự động tạo link ảnh từ Thumbnail
    public string ImageUrl
        => !string.IsNullOrWhiteSpace(LocalThumbnailPath) && File.Exists(LocalThumbnailPath)
            ? LocalThumbnailPath
            : (string.IsNullOrWhiteSpace(Thumbnail) ? "dotnet_bot.png" : Thumbnail);
    // ------------------------------------------

    public virtual ICollection<Menu> Menus { get; set; } = new List<Menu>();
    public virtual ICollection<PoiSubmission> PoiSubmissions { get; set; } = new List<PoiSubmission>();

    public virtual ICollection<Poilocalization> Poilocalizations { get; set; } = new List<Poilocalization>();

    public virtual ICollection<VisitLog> VisitLogs { get; set; } = new List<VisitLog>();
}
