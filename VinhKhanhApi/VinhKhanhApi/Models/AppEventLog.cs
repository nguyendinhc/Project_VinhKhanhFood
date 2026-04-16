using System;

namespace VinhKhanhApi.Models;

public partial class AppEventLog
{
    public long EventId { get; set; }

    public string DeviceId { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public string? QrCode { get; set; }

    public int? Poiid { get; set; }

    public DateTime CreatedAt { get; set; }
}

