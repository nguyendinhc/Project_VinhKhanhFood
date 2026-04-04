using System;

namespace VinhKhanhApi.Models;

public partial class UserFavorite
{
    public int UserId { get; set; }

    public int Poiid { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual AdminUser User { get; set; } = null!;

    public virtual Poi Poi { get; set; } = null!;
}
