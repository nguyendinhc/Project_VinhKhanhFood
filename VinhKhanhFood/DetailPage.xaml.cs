using VinhKhanhFood.Models;

namespace VinhKhanhFood;

public partial class DetailPage : ContentPage
{
    private Poi _poi;

    // Sửa constructor để nhận đối tượng Poi
    public DetailPage(Poi poi)
    {
        InitializeComponent();
        _poi = poi;
        BindingContext = _poi;

        //  Vừa mở trang lên là kiểm tra xem quán này đã thả tim chưa
        CheckFavoriteStatus();
    }

    private async void OnSpeakClicked(object sender, EventArgs e)
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
            // Tạo tọa độ của quán
            var location = new Location(poi.Latitude, poi.Longitude);

            // Cấu hình mở Google Maps ở chế độ dẫn đường (Driving/Walking)
            var options = new MapLaunchOptions
            {
                Name = poi.Name,
                NavigationMode = NavigationMode.Driving
            };

            try
            {
                // Lệnh này sẽ tự động gọi App Bản đồ của điện thoại
                await Map.Default.OpenAsync(location, options);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Lỗi", "Điện thoại của bạn chưa cài ứng dụng bản đồ!", "OK");
            }
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