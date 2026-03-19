using System;
using System.Collections.Generic;

namespace VinhKhanhFood.Models;

public partial class Poilocalization
{
    public int LocalId { get; set; }

    public int? Poiid { get; set; }

    public string LanguageCode { get; set; } = null!;

    public string? Description { get; set; }

    public string? AudioUrl { get; set; }

    public virtual Poi? Poi { get; set; }
}
