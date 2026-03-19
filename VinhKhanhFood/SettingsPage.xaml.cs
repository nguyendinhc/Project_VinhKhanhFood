namespace VinhKhanhFood;

public partial class SettingsPage : ContentPage
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    // Hàm này tự chạy khi mở trang Cài đặt lên
    protected override void OnAppearing()
    {
        base.OnAppearing();

        string lang = Preferences.Default.Get("AppLanguage", "vi");
        switch (lang)
        {
            case "vi": pckLanguage.SelectedIndex = 0; break;
            case "en": pckLanguage.SelectedIndex = 1; break;
            case "ko": pckLanguage.SelectedIndex = 2; break;
            case "zh": pckLanguage.SelectedIndex = 3; break;
            case "ja": pckLanguage.SelectedIndex = 4; break; // THÊM DÒNG NÀY
        }

        swAutoPlay.IsToggled = Preferences.Default.Get("AutoPlayGPS", true);
    }

    private void OnLanguageChanged(object sender, EventArgs e)
    {
        string selectedLang = "vi";
        switch (pckLanguage.SelectedIndex)
        {
            case 0: selectedLang = "vi"; break;
            case 1: selectedLang = "en"; break;
            case 2: selectedLang = "ko"; break;
            case 3: selectedLang = "zh"; break;
            case 4: selectedLang = "ja"; break; // THÊM DÒNG NÀY
        }
        Preferences.Default.Set("AppLanguage", selectedLang);
    }
    // Khi người dùng gạt công tắc Bật/Tắt GPS
    private void OnAutoPlayToggled(object sender, ToggledEventArgs e)
    {
        // Lưu trạng thái True/False vào bộ nhớ
        Preferences.Default.Set("AutoPlayGPS", e.Value);
    }
}