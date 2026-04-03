using System.Globalization;
using Microsoft.Maui.Storage;
using Newtonsoft.Json;
using VinhKhanhFood.Models;

namespace VinhKhanhFood.Services;

public class OfflineSyncService
{
    private const string PoisCacheFileName = "offline_pois.json";
    private const string LastSyncPreferenceKey = "OfflinePoisLastSyncUtc";
    private static bool _offlineNoticeShown;
    private readonly ApiService _apiService;

    public OfflineSyncService(ApiService apiService)
    {
        _apiService = apiService;
    }

    public async Task<int> SyncPoisAsync()
    {
        var pois = await _apiService.GetPoisAsync();
        foreach (var item in pois)
        {
            item.Introduction = item.Poilocalizations?.FirstOrDefault()?.Description
                                ?? "Chào mừng bạn đến với " + item.Name;
            item.Description = "Địa điểm tham quan hấp dẫn tại Vĩnh Khánh";
        }

        var json = JsonConvert.SerializeObject(pois);
        var filePath = GetPoisCachePath();
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await File.WriteAllTextAsync(filePath, json);
        Preferences.Default.Set(LastSyncPreferenceKey, DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        return pois.Count;
    }

    public async Task<List<Poi>> LoadPoisAsync()
    {
        var filePath = GetPoisCachePath();
        if (!File.Exists(filePath))
        {
            return new List<Poi>();
        }

        var json = await File.ReadAllTextAsync(filePath);
        return JsonConvert.DeserializeObject<List<Poi>>(json) ?? new List<Poi>();
    }

    public static string? GetLastSyncDisplayText()
    {
        var value = Preferences.Default.Get(LastSyncPreferenceKey, string.Empty);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            return null;
        }

        return parsed.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);
    }

    public static bool ShouldShowOfflineNotice()
    {
        if (_offlineNoticeShown)
        {
            return false;
        }

        _offlineNoticeShown = true;
        return true;
    }

    private static string GetPoisCachePath()
    {
        return Path.Combine(FileSystem.AppDataDirectory, PoisCacheFileName);
    }
}
