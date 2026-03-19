using System;
using System.Collections.Generic;

namespace VinhKhanhFood.Models;

public partial class Menu
{
    public int MenuId { get; set; }

    public int? Poiid { get; set; }

    public string FoodName { get; set; } = null!;

    public decimal? Price { get; set; }

    public string? Image { get; set; }

    public virtual Poi? Poi { get; set; }
}
