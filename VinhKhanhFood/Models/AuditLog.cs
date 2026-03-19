using System;
using System.Collections.Generic;

namespace VinhKhanhFood.Models;

public partial class AuditLog
{
    public int LogId { get; set; }

    public int? UserId { get; set; }

    public string? Action { get; set; }

    public DateTime? Timestamp { get; set; }

    public virtual AdminUser? User { get; set; }
}
