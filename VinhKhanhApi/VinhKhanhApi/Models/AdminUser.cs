using System;
using System.Collections.Generic;

namespace VinhKhanhApi.Models;

public partial class AdminUser
{
    public int UserId { get; set; }

    public string UserName { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string? FullName { get; set; }

    public string? Email { get; set; }

    public int? RoleId { get; set; }

    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public virtual ICollection<PoiSubmission> PoiSubmissions { get; set; } = new List<PoiSubmission>();

    public virtual ICollection<UserFavorite> UserFavorites { get; set; } = new List<UserFavorite>();

    public virtual UserPreference? UserPreference { get; set; }

    public virtual Role? Role { get; set; }
}
