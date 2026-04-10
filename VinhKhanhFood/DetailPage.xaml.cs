using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Networking;
using VinhKhanhFood.Models;
using VinhKhanhFood.Services;

namespace VinhKhanhFood;

public partial class DetailPage : ContentPage
{
    private Poi _poi;
    private readonly ApiService _apiService = new ApiService();
    private readonly OfflineSyncService _offlineSyncService;
    private bool _isLoadingDetail;
    private bool _hasTrackedVisit;

    // Sửa constructor để nhận đối tượng Poi
    public DetailPage(Poi poi, bool autoSpeakOnAppear = false)
    {
        InitializeComponent();
        _poi = poi;
        _offlineSyncService = new OfflineSyncService(_apiService);
        BindingContext = _poi;

        //  Vừa mở trang lên là kiểm tra xem quán này đã thả tim chưa
        CheckFavoriteStatus();

        if (autoSpeakOnAppear)
        {
            Dispatcher.Dispatch(async () => await SpeakPoiAsync());
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_isLoadingDetail || _poi == null)
        {
            return;
        }

        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet && _poi.Menus != null && _poi.Menus.Any())
        {
            await TryTrackVisitAsync();
            return;
        }

        _isLoadingDetail = true;
        try
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                var cachedPois = await _offlineSyncService.LoadPoisAsync();
                var cachedPoi = cachedPois.FirstOrDefault(p => p.Poiid == _poi.Poiid);
                if (cachedPoi != null)
                {
                    cachedPoi.Introduction = cachedPoi.Poilocalizations?.FirstOrDefault()?.Description
                                             ?? _poi.Introduction
                                             ?? "Chào mừng bạn đến với " + cachedPoi.Name;
                    cachedPoi.Description = _poi.Description ?? "Địa điểm tham quan hấp dẫn tại Vĩnh Khánh";

                    if (cachedPoi.Menus == null || !cachedPoi.Menus.Any())
                    {
                        cachedPoi.Menus = _poi.Menus ?? new List<Menu>();
                    }

                    _poi = cachedPoi;
                    BindingContext = _poi;
                }

                return;
            }

            var detailPoi = await _apiService.GetPoiByIdAsync(_poi.Poiid);
            if (detailPoi == null)
            {
                return;
            }

            detailPoi.Introduction = detailPoi.Poilocalizations?.FirstOrDefault()?.Description
                                     ?? _poi.Introduction
                                     ?? "Chào mừng bạn đến với " + detailPoi.Name;
            detailPoi.Description = _poi.Description ?? "Địa điểm tham quan hấp dẫn tại Vĩnh Khánh";

            if (detailPoi.Menus == null || !detailPoi.Menus.Any())
            {
                detailPoi.Menus = _poi.Menus ?? new List<Menu>();
            }

            _poi = detailPoi;
            BindingContext = _poi;
            await TryTrackVisitAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Lỗi", "Không thể tải chi tiết địa điểm: " + ex.Message, "OK");
        }
        finally
        {
            _isLoadingDetail = false;
        }
    }

    private async void OnSpeakClicked(object sender, EventArgs e)
    {
        await SpeakPoiAsync();
    }

    private async Task SpeakPoiAsync()
    {
        try
        {
            if (_poi == null)
            {
                return;
            }

            // 1. Lấy mã ngôn ngữ người dùng đã chọn ở trang Settings (mặc định là 'vi')
            string currentLangCode = Preferences.Default.Get("AppLanguage", "vi");

            // 2. Tìm đoạn giới thiệu đúng ngôn ngữ trong dữ liệu trả về từ API
            string? textToSpeak = null;

            if (_poi.Poilocalizations != null && _poi.Poilocalizations.Any())
            {
                // Tìm dòng có mã ngôn ngữ khớp với cài đặt
                var localizedData = _poi.Poilocalizations
                    .FirstOrDefault(x => x.LanguageCode.ToLower() == currentLangCode.ToLower());

                textToSpeak = localizedData?.Description;
            }

            // Nếu không tìm thấy tiếng nước ngoài, dùng mặc định (tiếng Việt)
            if (string.IsNullOrEmpty(textToSpeak))
            {
                textToSpeak = _poi.Introduction ?? "Xin chào";
            }

            // 3. Cấu hình giọng đọc (Locale) cho máy
            var locales = await TextToSpeech.Default.GetLocalesAsync();

            // Tìm giọng đọc khớp với mã ngôn ngữ (VD: ja-JP, en-US...)
            var selectedLocale = locales.FirstOrDefault(l =>
                l.Language.ToLower().StartsWith(currentLangCode.ToLower()));

            // 4. Phát loa thuyết minh
            await TextToSpeech.Default.SpeakAsync(textToSpeak, new SpeechOptions
            {
                Locale = selectedLocale,
                Pitch = 1.0f,
                Volume = 1.0f
            });
        }
        catch (Exception ex)
        {
            await DisplayAlert("Lỗi", "Không thể phát thuyết minh: " + ex.Message, "OK");
        }
    }

    private async void OnNavigateClicked(object sender, EventArgs e)
    {
        var poi = BindingContext as Poi;
        if (poi != null)
        {
            Preferences.Default.Set("PendingRoutePoiId", poi.Poiid);
            await Shell.Current.GoToAsync("//main");
        }
    }

    // ==========================================
    // PHẦN CODE MỚI THÊM: XỬ LÝ NÚT THẢ TIM
    // ==========================================

    private void CheckFavoriteStatus()
    {
        var favoritePoiIds = GetFavoritePoiIds();
        var legacyFavorites = Preferences.Default.Get("FavoritePois", "");

        if (favoritePoiIds.Contains(_poi.Poiid) || legacyFavorites.Contains(_poi.Name))
        {
            // Đã tim rồi thì hiện tim đỏ, nền hồng
            btnFavorite.Text = "❤️ Đã Yêu thích";
            btnFavorite.BackgroundColor = Color.FromArgb("#FFEBEE");
        }
        else
        {
            // Chưa tim thì hiện tim trắng, nền trong suốt
            btnFavorite.Text = "🤍 Thêm vào Yêu thích";
            btnFavorite.BackgroundColor = Colors.Transparent;
        }
    }

    private void OnFavoriteClicked(object sender, EventArgs e)
    {
        var favoritePoiIds = GetFavoritePoiIds();
        var legacyFavorites = Preferences.Default.Get("FavoritePois", "");
        var legacyList = legacyFavorites.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

        var isFavorite = favoritePoiIds.Contains(_poi.Poiid);
        if (isFavorite)
        {
            favoritePoiIds.Remove(_poi.Poiid);
            legacyList.Remove(_poi.Name);
        }
        else
        {
            favoritePoiIds.Add(_poi.Poiid);
            if (!legacyList.Contains(_poi.Name))
            {
                legacyList.Add(_poi.Name);
            }
        }

        SaveFavoritePoiIds(favoritePoiIds);
        Preferences.Default.Set("FavoritePois", string.Join(",", legacyList));

        var token = Preferences.Default.Get("AuthToken", string.Empty);
        if (!string.IsNullOrWhiteSpace(token))
        {
            _ = _offlineSyncService.EnqueueFavoriteActionAsync(_poi.Poiid, !isFavorite);
            _ = _offlineSyncService.ProcessPendingActionsAsync();
        }

        CheckFavoriteStatus();
    }

    private static HashSet<int> GetFavoritePoiIds()
    {
        var saved = Preferences.Default.Get("FavoritePoiIds", string.Empty);
        var values = saved.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var result = new HashSet<int>();
        foreach (var value in values)
        {
            if (int.TryParse(value, out var id))
            {
                result.Add(id);
            }
        }

        return result;
    }

    private static void SaveFavoritePoiIds(HashSet<int> favoritePoiIds)
    {
        var serialized = string.Join(",", favoritePoiIds.OrderBy(id => id));
        if (string.IsNullOrWhiteSpace(serialized))
        {
            Preferences.Default.Remove("FavoritePoiIds");
            return;
        }

        Preferences.Default.Set("FavoritePoiIds", serialized);
    }

    private async Task TryTrackVisitAsync()
    {
        if (_hasTrackedVisit || _poi == null || _poi.Poiid <= 0)
        {
            return;
        }

        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            return;
        }

        try
        {
            await _apiService.TrackPoiVisitAsync(_poi.Poiid);
            _hasTrackedVisit = true;
        }
        catch
        {
        }
    }

    private void Dummy()
    {
    }
}
