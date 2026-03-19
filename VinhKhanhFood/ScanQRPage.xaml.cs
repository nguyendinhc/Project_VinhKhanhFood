using ZXing.Net.Maui;

using System.Text.RegularExpressions;
using VinhKhanhFood.Models;
using VinhKhanhFood.Services;
using ZXing.Net.Maui;

namespace VinhKhanhFood;

public partial class ScanQRPage : ContentPage
{
    private readonly ApiService _apiService = new ApiService();
    private List<Poi> _pois = new();
    private bool _isHandlingScan;

    public ScanQRPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        var hasPermission = await EnsureCameraPermissionAsync();
        barcodeReader.IsDetecting = hasPermission;
        _isHandlingScan = false;

        if (_pois.Count == 0)
        {
            _pois = await _apiService.GetPoisAsync() ?? new List<Poi>();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        barcodeReader.IsDetecting = false;
    }

    private async Task<bool> EnsureCameraPermissionAsync()
    {
        var cameraPermission = await Permissions.CheckStatusAsync<Permissions.Camera>();
        if (cameraPermission != PermissionStatus.Granted)
        {
            cameraPermission = await Permissions.RequestAsync<Permissions.Camera>();
        }

        if (cameraPermission != PermissionStatus.Granted)
        {
            await DisplayAlert("Quyền camera", "Bạn cần cấp quyền camera để quét QR.", "OK");
            return false;
        }

        return true;
    }

    private void OnBarcodesDetected(object sender, BarcodeDetectionEventArgs e)
    {
        if (_isHandlingScan)
        {
            return;
        }

        var result = e.Results.FirstOrDefault();
        if (result == null)
        {
            return;
        }

        _isHandlingScan = true;
        barcodeReader.IsDetecting = false;
        var rawValue = result.Value?.Trim() ?? string.Empty;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                if (_pois.Count == 0)
                {
                    _pois = await _apiService.GetPoisAsync() ?? new List<Poi>();
                }

                var poiId = ExtractPoiId(rawValue);
                var targetPoi = poiId.HasValue
                    ? _pois.FirstOrDefault(p => p.Poiid == poiId.Value)
                    : null;

                if (targetPoi == null)
                {
                    await DisplayAlert("Thông báo", $"Không tìm thấy quán cho mã: {rawValue}", "OK");
                    barcodeReader.IsDetecting = true;
                    _isHandlingScan = false;
                    return;
                }

                await Shell.Current.Navigation.PushAsync(new DetailPage(targetPoi));
            }
            catch (Exception ex)
            {
                await DisplayAlert("Lỗi", "Không thể xử lý mã QR: " + ex.Message, "OK");
                barcodeReader.IsDetecting = true;
                _isHandlingScan = false;
            }
        });
    }

    private int? ExtractPoiId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (int.TryParse(value, out var id))
        {
            return id;
        }

        var match = Regex.Match(value, @"(?:id|poiid)=(\d+)", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out id))
        {
            return id;
        }

        match = Regex.Match(value, @"\d+");
        if (match.Success && int.TryParse(match.Value, out id))
        {
            return id;
        }

        return null;
    }
}
