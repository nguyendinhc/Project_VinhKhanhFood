using System;
using System.Collections.Generic;

namespace VinhKhanhApi.Models;

public partial class PoiSubmission
{
    public int SubmissionId { get; set; }

    public int? Poiid { get; set; }

    public int? UserId { get; set; }

    public string? DataJson { get; set; }

    public int? Status { get; set; }

    public string? AdminNote { get; set; }

    public virtual Poi? Poi { get; set; }

    public virtual AdminUser? User { get; set; }
}
