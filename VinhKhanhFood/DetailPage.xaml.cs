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
    }

    private async void OnSpeakClicked(object sender, EventArgs e)
    {
        if (BindingContext is Poi poi && !string.IsNullOrEmpty(poi.Introduction))
        {
            // Lệnh để máy tự đọc bài giới thiệu
            await TextToSpeech.Default.SpeakAsync(poi.Introduction, new SpeechOptions
            {
                Pitch = 1.0f,   // Độ cao giọng nói
                Volume = 1.0f    // Âm lượng
            });
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
                // Lệnh này sẽ tự động gọi App Bản đồ của điện thoại (Google Maps hoặc Apple Maps)
                await Map.Default.OpenAsync(location, options);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Lỗi", "Điện thoại của bạn chưa cài ứng dụng bản đồ!", "OK");
            }
        }
    }
}