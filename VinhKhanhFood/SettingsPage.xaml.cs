using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using VinhKhanhFood.Services;

namespace VinhKhanhFood;

public partial class SettingsPage : ContentPage
{
    private readonly OfflineSyncService _offlineSyncService;
    private Button? _btnOfflineSync;
    private Label? _lblOfflineSyncStatus;

    public SettingsPage()
    {
        InitializeComponent();
        _offlineSyncService = new OfflineSyncService(new ApiService());
        _btnOfflineSync = this.FindByName<Button>("btnOfflineSync");
        _lblOfflineSyncStatus = this.FindByName<Label>("lblOfflineSyncStatus");
    }

    // Hàm này tự chạy khi mở trang Cài đặt lên
    protected override void OnAppearing()
    {
        base.OnAppearing();

        string lang = Preferences.Default.Get("AppLanguage", "vi");
        ApplyLanguageSelection(lang);

        swAutoPlay.IsToggled = Preferences.Default.Get("AutoPlayGPS", true);
        UpdateOfflineSyncStatus();
    }

    private void OnLanguageChanged(object sender, EventArgs e)
    {
        // giữ sự kiện để tương thích XAML; lưu được thực hiện khi nhấn nút Lưu Cài Đặt
    }
    // Khi người dùng gạt công tắc Bật/Tắt GPS
    private void OnAutoPlayToggled(object sender, ToggledEventArgs e)
    {
        // Lưu trạng thái True/False vào bộ nhớ
        Preferences.Default.Set("AutoPlayGPS", e.Value);
    }

    private async void OnSaveSettingsClicked(object sender, EventArgs e)
    {
        string selectedLang = "vi";
        switch (pckLanguage.SelectedIndex)
        {
            case 0: selectedLang = "vi"; break;
            case 1: selectedLang = "en"; break;
            case 2: selectedLang = "ko"; break;
            case 3: selectedLang = "zh"; break;
            case 4: selectedLang = "ja"; break;
        }

        Preferences.Default.Set("AppLanguage", selectedLang);
        Preferences.Default.Set("AutoPlayGPS", swAutoPlay.IsToggled);

        var token = Preferences.Default.Get("AuthToken", string.Empty);
        if (!string.IsNullOrWhiteSpace(token))
        {
            await _offlineSyncService.EnqueueLanguageActionAsync(selectedLang);
            await _offlineSyncService.ProcessPendingActionsAsync();
        }

        var options = new SnackbarOptions
        {
            BackgroundColor = Color.FromArgb("#FF5722"),
            TextColor = Colors.White,
            CornerRadius = new CornerRadius(12)
        };

        var snackbar = Snackbar.Make("Đã lưu cài đặt", visualOptions: options);
        await snackbar.Show();
    }

    private void ApplyLanguageSelection(string languageCode)
    {
        switch (languageCode)
        {
            case "vi": pckLanguage.SelectedIndex = 0; break;
            case "en": pckLanguage.SelectedIndex = 1; break;
            case "ko": pckLanguage.SelectedIndex = 2; break;
            case "zh": pckLanguage.SelectedIndex = 3; break;
            case "ja": pckLanguage.SelectedIndex = 4; break;
        }
    }

    private async void OnOfflineSyncClicked(object sender, EventArgs e)
    {
        if (_btnOfflineSync == null)
        {
            return;
        }

        if (_btnOfflineSync.IsEnabled == false)
        {
            return;
        }

        _btnOfflineSync.IsEnabled = false;
        try
        {
            var count = await _offlineSyncService.SyncPoisAsync();
            UpdateOfflineSyncStatus();

            var options = new SnackbarOptions
            {
                BackgroundColor = Color.FromArgb("#1E90FF"),
                TextColor = Colors.White,
                CornerRadius = new CornerRadius(12)
            };

            var snackbar = Snackbar.Make($"Đã đồng bộ {count} địa điểm", visualOptions: options);
            await snackbar.Show();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Lỗi", "Không thể đồng bộ dữ liệu: " + ex.Message, "OK");
        }
        finally
        {
            _btnOfflineSync.IsEnabled = true;
        }
    }

    private void UpdateOfflineSyncStatus()
    {
        var lastSyncText = OfflineSyncService.GetLastSyncDisplayText();
        if (_lblOfflineSyncStatus == null)
        {
            return;
        }

        _lblOfflineSyncStatus.Text = string.IsNullOrWhiteSpace(lastSyncText)
            ? "Chưa đồng bộ"
            : $"Lần đồng bộ gần nhất: {lastSyncText}";
    }

    private async void OnOwnerRegisterClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new OwnerRegisterPage());
    }

}