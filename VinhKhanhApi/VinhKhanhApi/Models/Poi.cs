using System;
using System.Collections.Generic;

namespace VinhKhanhApi.Models;

public partial class Poi
{
    public int Poiid { get; set; }

    public string Name { get; set; } = null!;

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public int? Radius { get; set; }

    public string? Thumbnail { get; set; }

    public string? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<Menu> Menus { get; set; } = new List<Menu>();

    public virtual ICollection<PoiSubmission> PoiSubmissions { get; set; } = new List<PoiSubmission>();

    public virtual ICollection<Poilocalization> Poilocalizations { get; set; } = new List<Poilocalization>();

    public virtual ICollection<VisitLog> VisitLogs { get; set; } = new List<VisitLog>();
}
