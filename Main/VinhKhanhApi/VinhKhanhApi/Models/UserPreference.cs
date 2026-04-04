using System;

namespace VinhKhanhApi.Models;

public partial class UserPreference
{
    public int UserId { get; set; }

    public string PreferredLanguage { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; }

    public virtual AdminUser User { get; set; } = null!;
}
