using System;
using System.Collections.Generic;
using System.IO;

namespace VinhKhanhFood.Models;

public partial class Menu
{
    public int MenuId { get; set; }

    public int? Poiid { get; set; }

    public string FoodName { get; set; } = null!;

    public decimal? Price { get; set; }

    public string? Image { get; set; }

    public string? LocalImagePath { get; set; }

    public string ImageUrl
        => !string.IsNullOrWhiteSpace(LocalImagePath) && File.Exists(LocalImagePath)
            ? LocalImagePath
            : (string.IsNullOrWhiteSpace(Image) ? "dotnet_bot.png" : Image);

    public virtual Poi? Poi { get; set; }
}
