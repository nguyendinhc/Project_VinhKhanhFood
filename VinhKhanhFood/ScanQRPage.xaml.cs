using System.Text.Json;
using System.Text.RegularExpressions;
using VinhKhanhFood.Models;
using VinhKhanhFood.Services;
using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;

namespace VinhKhanhFood;

public partial class ScanQRPage : ContentPage
{
    private readonly ApiService _apiService = new ApiService();
    private readonly OfflineSyncService _offlineSyncService;
    private List<Poi> _pois = new();
    private bool _isHandlingScan;
    private readonly BarcodeReaderOptions _barcodeOptions = new()
    {
        Formats = BarcodeFormat.QrCode,
        AutoRotate = true,
        Multiple = false,
        TryHarder = true,
        TryInverted = true
    };
    private bool _isTorchOn;
    private CameraBarcodeReaderView? _barcodeReader;

    public ScanQRPage()
    {
        InitializeComponent();
        _offlineSyncService = new OfflineSyncService(_apiService);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        _isHandlingScan = false;
        _isTorchOn = false;
        btnTorch.Text = "Bật đèn";

        var hasPermission = await EnsureCameraPermissionAsync();

        if (hasPermission)
        {
            await ForceRestartCameraAsync();
        }
        else
        {
            if (_barcodeReader != null)
            {
                _barcodeReader.IsDetecting = false;
                _barcodeReader.IsEnabled = false;
            }
            lblScanStatus.Text = "Chưa có quyền camera. Vui lòng cấp quyền trong cài đặt";
        }

        if (_pois.Count == 0)
        {
            try
            {
                _pois = await _apiService.GetPoisAsync() ?? new List<Poi>();
            }
            catch
            {
                _pois = await _offlineSyncService.LoadPoisAsync();
            }
        }
    }

    public async Task ForceRestartCameraAsync()
    {
        DestroyCameraView();
        await Task.Delay(120);

        EnsureCameraViewCreated();
        if (_barcodeReader == null)
        {
            lblScanStatus.Text = "Không khởi tạo được camera (cameraHost null)";
            return;
        }

        _barcodeReader.Options = _barcodeOptions;
        _barcodeReader.IsVisible = true;
        _barcodeReader.IsEnabled = true;
        _barcodeReader.IsDetecting = false;

        await Task.Delay(250);
        _barcodeReader.IsDetecting = true;
        lblScanStatus.Text = "Đang sẵn sàng quét mã QR";
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (_barcodeReader != null)
        {
            _barcodeReader.IsDetecting = false;
            _barcodeReader.IsTorchOn = false;
            _barcodeReader.IsEnabled = false;
            _barcodeReader.IsVisible = false;
        }

        DestroyCameraView();
    }

    private void RestartScanner(string statusText)
    {
        EnsureCameraViewCreated();
        if (_barcodeReader == null)
        {
            lblScanStatus.Text = "Không thể khởi tạo camera";
            _isHandlingScan = false;
            return;
        }

        _barcodeReader.IsEnabled = true;
        _barcodeReader.IsVisible = true;
        _barcodeReader.IsDetecting = true;
        lblScanStatus.Text = statusText;
        _isHandlingScan = false;
    }

    private void EnsureCameraViewCreated()
    {
        if (_barcodeReader != null)
        {
            return;
        }

        var host = this.FindByName<Grid>("cameraHost");
        if (host == null)
        {
            lblScanStatus.Text = "Không tìm thấy cameraHost";
            return;
        }

        var reader = new CameraBarcodeReaderView
        {
            CameraLocation = CameraLocation.Rear,
            IsDetecting = false,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };

        reader.BarcodesDetected += OnBarcodesDetected;
        _barcodeReader = reader;
        host.Children.Clear();
        host.Children.Add(reader);
    }

    private void DestroyCameraView()
    {
        if (_barcodeReader == null)
        {
            return;
        }

        _barcodeReader.BarcodesDetected -= OnBarcodesDetected;
        var host = this.FindByName<Grid>("cameraHost");
        host?.Children.Clear();
        _barcodeReader = null;
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
            var openSettings = await DisplayAlert("Quyền camera", "Bạn cần cấp quyền camera để quét QR.", "Mở cài đặt", "Để sau");
            if (openSettings)
            {
                AppInfo.ShowSettingsUI();
            }
            return false;
        }

        return true;
    }

    private void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
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
        var rawValue = result.Value?.Trim() ?? string.Empty;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                if (_barcodeReader != null)
                {
                    _barcodeReader.IsDetecting = false;
                    _barcodeReader.IsEnabled = false;
                    _barcodeReader.IsVisible = false;
                    _barcodeReader.IsTorchOn = false;
                }

                _isTorchOn = false;
                btnTorch.Text = "Bật đèn";
                lblScanStatus.Text = $"Đã đọc mã: {rawValue}";

                if (_pois.Count == 0)
                {
                    try
                    {
                        _pois = await _apiService.GetPoisAsync() ?? new List<Poi>();
                    }
                    catch
                    {
                        _pois = await _offlineSyncService.LoadPoisAsync();
                    }
                }

                if (IsGlobalQr(rawValue))
                {
                    lblScanStatus.Text = "Đã quét QR tổng. Đang mở danh sách quán...";
                    Preferences.Default.Set("HasUnlockedPoiList", true);
                    _ = _apiService.LogAppEventAsync("qr_scan", "global");
                    await Shell.Current.GoToAsync("//main");
                    RestartScanner("Đã mở danh sách quán (QR tổng)");
                    return;
                }

                var poiId = ExtractPoiId(rawValue);
                Poi? targetPoi = null;
                Exception? poiApiLookupException = null;

                if (poiId.HasValue)
                {
                    try
                    {
                        targetPoi = await _apiService.GetPoiByIdAsync(poiId.Value);
                    }
                    catch (Exception ex)
                    {
                        poiApiLookupException = ex;
                    }

                    targetPoi ??= _pois.FirstOrDefault(p => p.Poiid == poiId.Value);
                }

                if (targetPoi == null)
                {
                    if (!poiId.HasValue)
                    {
                        await DisplayAlert("Thông báo", $"Đã đọc được mã nhưng không tách được ID quán từ nội dung: {rawValue}", "OK");
                        RestartScanner("Đã đọc mã nhưng không tách được ID quán");
                        return;
                    }

                    if (poiApiLookupException != null && _pois.Count == 0)
                    {
                        await DisplayAlert("Thông báo",
                            $"Đã đọc được ID {poiId.Value} nhưng không tải được dữ liệu quán. Kiểm tra kết nối API/server rồi thử lại.\nChi tiết: {poiApiLookupException.Message}",
                            "OK");
                        RestartScanner("Đã đọc ID nhưng chưa lấy được dữ liệu quán");
                        return;
                    }

                    await DisplayAlert("Thông báo", $"Không tìm thấy quán cho ID: {poiId.Value}", "OK");
                    RestartScanner("Không tìm thấy quán ứng với ID đã quét");
                    return;
                }

                lblScanStatus.Text = $"Đã quét: {targetPoi.Name}";
                await Navigation.PushAsync(new DetailPage(targetPoi, true));
            }
            catch (Exception ex)
            {
                await DisplayAlert("Lỗi", "Không thể xử lý mã QR: " + ex.Message, "OK");
                RestartScanner("Có lỗi khi xử lý mã QR");
            }
        });
    }

    private void OnToggleTorchClicked(object sender, EventArgs e)
    {
        if (_barcodeReader == null)
        {
            return;
        }

        _isTorchOn = !_isTorchOn;
        _barcodeReader.IsTorchOn = _isTorchOn;
        btnTorch.Text = _isTorchOn ? "Tắt đèn" : "Bật đèn";
    }

    private async void OnManualInputClicked(object sender, EventArgs e)
    {
        var input = await DisplayPromptAsync("Nhập mã QR", "Nhập nội dung mã hoặc ID quán", "Tìm", "Hủy", keyboard: Keyboard.Text);
        if (string.IsNullOrWhiteSpace(input))
        {
            return;
        }

        if (IsGlobalQr(input))
        {
            lblScanStatus.Text = "Đã nhập QR tổng. Đang mở danh sách quán...";
            Preferences.Default.Set("HasUnlockedPoiList", true);
            _ = _apiService.LogAppEventAsync("qr_scan", "global");
            await Shell.Current.GoToAsync("//main");
            return;
        }

        var poiId = ExtractPoiId(input);
        if (!poiId.HasValue)
        {
            lblScanStatus.Text = "Mã nhập vào không hợp lệ";
            await DisplayAlert("Thông báo", "Không đọc được ID quán từ mã bạn nhập.", "OK");
            return;
        }

        Poi? targetPoi = null;
        try
        {
            targetPoi = await _apiService.GetPoiByIdAsync(poiId.Value);
        }
        catch
        {
        }

        targetPoi ??= _pois.FirstOrDefault(p => p.Poiid == poiId.Value);
        if (targetPoi == null)
        {
            lblScanStatus.Text = "Không tìm thấy quán theo mã nhập";
            await DisplayAlert("Thông báo", "Không tìm thấy quán tương ứng.", "OK");
            return;
        }

        lblScanStatus.Text = $"Đã chọn: {targetPoi.Name}";
        await Navigation.PushAsync(new DetailPage(targetPoi, true));
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

        try
        {
            using var json = JsonDocument.Parse(value);
            if (json.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (json.RootElement.TryGetProperty("poiId", out var poiIdElement) && poiIdElement.TryGetInt32(out id))
                {
                    return id;
                }

                if (json.RootElement.TryGetProperty("id", out var idElement) && idElement.TryGetInt32(out id))
                {
                    return id;
                }
            }
        }
        catch
        {
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            var idFromQuery = TryExtractIdFromText(uri.Query);
            if (idFromQuery.HasValue)
            {
                return idFromQuery.Value;
            }

            var segments = uri.Segments;
            if (segments.Length > 0)
            {
                var lastSegment = segments[^1].Trim('/');
                if (int.TryParse(lastSegment, out id))
                {
                    return id;
                }
            }
        }

        var idFromText = TryExtractIdFromText(value);
        if (idFromText.HasValue)
        {
            return idFromText.Value;
        }

        return null;
    }

    private static int? TryExtractIdFromText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        int id;

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

    private static bool IsGlobalQr(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        if (string.Equals(rawValue.Trim(), "global", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (rawValue.Contains("qr=global", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (Uri.TryCreate(rawValue.Trim(), UriKind.Absolute, out var uri))
        {
            if (uri.Query.Contains("qr=global", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}


