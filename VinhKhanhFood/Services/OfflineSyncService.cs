using System.Globalization;
using System.Linq;
using Microsoft.Maui.Storage;
using Newtonsoft.Json;
using SQLite;
using Microsoft.Maui.Networking;
using VinhKhanhFood.Models;

namespace VinhKhanhFood.Services;

public class OfflineSyncService
{
    private const string OfflineDatabaseFileName = "offline_cache.db3";
    private const string PoisCacheKey = "pois";
    private const string LastSyncPreferenceKey = "OfflinePoisLastSyncUtc";
    private static bool _offlineNoticeShown;
    private static readonly SemaphoreSlim _pendingActionsLock = new(1, 1);
    private readonly ApiService _apiService;
    private readonly SQLiteAsyncConnection _database;
    private readonly Task _initializationTask;

    public OfflineSyncService(ApiService apiService)
    {
        _apiService = apiService;
        _database = new SQLiteAsyncConnection(GetDatabasePath());
        _initializationTask = InitializeDatabaseAsync();
    }

    public async Task<int> SyncPoisAsync()
    {
        await _initializationTask;

        var pois = await _apiService.GetPoisAsync();
        foreach (var poi in pois)
        {
            try
            {
                var detailPoi = await _apiService.GetPoiByIdAsync(poi.Poiid);
                if (detailPoi != null)
                {
                    poi.Menus = detailPoi.Menus ?? poi.Menus;
                    poi.LocalThumbnailPath ??= detailPoi.LocalThumbnailPath;
                }
            }
            catch
            {
            }
        }

        foreach (var item in pois)
        {
            item.Introduction = item.Poilocalizations?.FirstOrDefault()?.Description
                                ?? "Chào mừng bạn đến với " + item.Name;
            item.Description = "Địa điểm tham quan hấp dẫn tại Vĩnh Khánh";
        }

        var json = JsonConvert.SerializeObject(pois);
        var syncTime = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);

        await _database.InsertOrReplaceAsync(new OfflineCacheEntry
        {
            CacheKey = PoisCacheKey,
            Payload = json,
            LastSyncUtc = syncTime
        });

        Preferences.Default.Set(LastSyncPreferenceKey, syncTime);
        return pois.Count;
    }

    public async Task<List<Poi>> LoadPoisAsync()
    {
        await _initializationTask;

        var entry = await _database.FindAsync<OfflineCacheEntry>(PoisCacheKey);
        if (entry == null || string.IsNullOrWhiteSpace(entry.Payload))
        {
            return new List<Poi>();
        }

        return JsonConvert.DeserializeObject<List<Poi>>(entry.Payload) ?? new List<Poi>();
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

    public async Task EnqueueFavoriteActionAsync(int poiId, bool isFavorite)
    {
        var payload = new FavoriteActionPayload
        {
            PoiId = poiId,
            IsFavorite = isFavorite
        };

        await EnqueueActionAsync("favorite", $"favorite:{poiId}", payload);
    }

    public async Task EnqueueLanguageActionAsync(string languageCode)
    {
        var payload = new LanguageActionPayload
        {
            LanguageCode = languageCode
        };

        await EnqueueActionAsync("language", "language", payload);
    }

    public async Task<int> ProcessPendingActionsAsync()
    {
        await _initializationTask;

        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            return 0;
        }

        var token = Preferences.Default.Get("AuthToken", string.Empty);
        if (string.IsNullOrWhiteSpace(token))
        {
            return 0;
        }

        await _pendingActionsLock.WaitAsync();
        try
        {
            var actions = await LoadPendingActionsAsync();
            if (actions.Count == 0)
            {
                await SyncAccountPreferencesAsync(syncFavorites: true, syncLanguage: true);
                return 0;
            }

            var remaining = new List<PendingActionEntry>();
            var processedCount = 0;

            foreach (var action in actions)
            {
                try
                {
                    if (string.Equals(action.Type, "favorite", StringComparison.OrdinalIgnoreCase))
                    {
                        var payload = JsonConvert.DeserializeObject<FavoriteActionPayload>(action.Payload);
                        if (payload != null)
                        {
                            await _apiService.SetFavoriteAsync(payload.PoiId, payload.IsFavorite);
                            processedCount++;
                            continue;
                        }
                    }
                    else if (string.Equals(action.Type, "language", StringComparison.OrdinalIgnoreCase))
                    {
                        var payload = JsonConvert.DeserializeObject<LanguageActionPayload>(action.Payload);
                        if (payload != null)
                        {
                            await _apiService.SetPreferredLanguageAsync(payload.LanguageCode);
                            processedCount++;
                            continue;
                        }
                    }

                    remaining.Add(action);
                }
                catch
                {
                    remaining.Add(action);
                }
            }

            await SavePendingActionsAsync(remaining);
            var shouldSyncFavorites = !remaining.Any(entry => string.Equals(entry.Type, "favorite", StringComparison.OrdinalIgnoreCase));
            var shouldSyncLanguage = !remaining.Any(entry => string.Equals(entry.Type, "language", StringComparison.OrdinalIgnoreCase));
            await SyncAccountPreferencesAsync(shouldSyncFavorites, shouldSyncLanguage);
            return processedCount;
        }
        finally
        {
            _pendingActionsLock.Release();
        }
    }

    public async Task SyncAccountPreferencesAsync()
    {
        await SyncAccountPreferencesAsync(syncFavorites: true, syncLanguage: true);
    }

    private async Task InitializeDatabaseAsync()
    {
        await _database.CreateTableAsync<OfflineCacheEntry>();
        await _database.CreateTableAsync<PendingActionEntry>();
    }

    private async Task EnqueueActionAsync(string type, string key, object payload)
    {
        await _initializationTask;

        await _pendingActionsLock.WaitAsync();
        try
        {
            await _database.InsertOrReplaceAsync(new PendingActionEntry
            {
                Type = type,
                Key = key,
                Payload = JsonConvert.SerializeObject(payload),
                CreatedUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            });
        }
        finally
        {
            _pendingActionsLock.Release();
        }
    }

    private async Task SyncAccountPreferencesAsync(bool syncFavorites, bool syncLanguage)
    {
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            return;
        }

        var token = Preferences.Default.Get("AuthToken", string.Empty);
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        if (syncFavorites)
        {
            await SyncFavoriteIdsFromServerAsync();
        }

        if (syncLanguage)
        {
            await SyncLanguageFromServerAsync();
        }
    }

    private async Task SyncFavoriteIdsFromServerAsync()
    {
        try
        {
            var favoriteIds = await _apiService.GetFavoritePoiIdsAsync();
            if (favoriteIds == null || favoriteIds.Count == 0)
            {
                Preferences.Default.Remove("FavoritePoiIds");
                Preferences.Default.Remove("FavoritePois");
                return;
            }

            var serialized = string.Join(",", favoriteIds.OrderBy(id => id));
            Preferences.Default.Set("FavoritePoiIds", serialized);
            Preferences.Default.Remove("FavoritePois");
        }
        catch
        {
        }
    }

    private async Task SyncLanguageFromServerAsync()
    {
        try
        {
            var languageCode = await _apiService.GetPreferredLanguageAsync();
            if (string.IsNullOrWhiteSpace(languageCode))
            {
                return;
            }

            Preferences.Default.Set("AppLanguage", languageCode);
        }
        catch
        {
        }
    }

    private async Task<List<PendingActionEntry>> LoadPendingActionsAsync()
    {
        return await _database.Table<PendingActionEntry>()
            .OrderBy(x => x.CreatedUtc)
            .ToListAsync();
    }

    private async Task SavePendingActionsAsync(List<PendingActionEntry> actions)
    {
        await _database.DeleteAllAsync<PendingActionEntry>();
        if (actions.Count > 0)
        {
            await _database.InsertAllAsync(actions);
        }
    }

    private static string GetDatabasePath()
    {
        return Path.Combine(FileSystem.AppDataDirectory, OfflineDatabaseFileName);
    }

    private class OfflineCacheEntry
    {
        [PrimaryKey]
        public string CacheKey { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
        public string LastSyncUtc { get; set; } = string.Empty;
    }

    private class PendingActionEntry
    {
        [PrimaryKey]
        public string Key { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
        public string CreatedUtc { get; set; } = string.Empty;
    }

    private class FavoriteActionPayload
    {
        public int PoiId { get; set; }
        public bool IsFavorite { get; set; }
    }

    private class LanguageActionPayload
    {
        public string LanguageCode { get; set; } = string.Empty;
    }
}
