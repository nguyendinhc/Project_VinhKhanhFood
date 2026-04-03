using System.Collections.Generic;
using System.Linq;
using VinhKhanhFood.Models;
using VinhKhanhFood.Services;

namespace VinhKhanhFood;

public partial class DetailPage : ContentPage
{
    private Poi _poi;
    private readonly ApiService _apiService = new ApiService();
    private bool _isLoadingDetail;

    // Sửa constructor để nhận đối tượng Poi
    public DetailPage(Poi poi, bool autoSpeakOnAppear = false)
    {
        InitializeComponent();
        _poi = poi;
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

        if (_poi.Menus != null && _poi.Menus.Any())
        {
            return;
        }

        _isLoadingDetail = true;
        try
        {
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
            string textToSpeak = "";

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
        // Lấy danh sách tên quán đã lưu trong máy
        string savedFavorites = Preferences.Default.Get("FavoritePois", "");

        if (savedFavorites.Contains(_poi.Name))
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
        // Lấy danh sách cũ ra
        string savedFavorites = Preferences.Default.Get("FavoritePois", "");
        List<string> favList = savedFavorites.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

        if (favList.Contains(_poi.Name))
        {
            // Nếu có tên trong danh sách rồi -> Bấm phát nữa là XÓA (Bỏ thích)
            favList.Remove(_poi.Name);
        }
        else
        {
            // Nếu chưa có -> THÊM vào danh sách
            favList.Add(_poi.Name);
        }

        // Đóng gói lại thành chuỗi và lưu ngược vào bộ nhớ máy
        string newFavorites = string.Join(",", favList);
        Preferences.Default.Set("FavoritePois", newFavorites);

        // Gọi lại hàm kiểm tra để đổi màu nút ngay lập tức
        CheckFavoriteStatus();
    }
}