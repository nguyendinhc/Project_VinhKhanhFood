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
    private CameraBarcodeReaderView? _barcodeReader;
    private readonly BarcodeReaderOptions _barcodeOptions = new()
    {
        Formats = BarcodeFormat.QrCode,
        AutoRotate = true,
        TryHarder = false,
        TryInverted = false
    };

    public ScanQRPage()
    {
        InitializeComponent();
        _offlineSyncService = new OfflineSyncService(_apiService);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        var hasPermission = await EnsureCameraPermissionAsync();
        _isHandlingScan = false;

        ResetCameraView();

        if (hasPermission)
        {
            await Task.Delay(200);
            _barcodeReader = CreateCameraView();
            cameraHost.Children.Add(_barcodeReader);
            await Task.Delay(150);
            _barcodeReader.IsDetecting = true;
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

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (_barcodeReader != null)
        {
            _barcodeReader.IsDetecting = false;
        }
    }

    private void ResetCameraView()
    {
        if (_barcodeReader != null)
        {
            _barcodeReader.IsDetecting = false;
            _barcodeReader.IsEnabled = false;
            _barcodeReader.IsVisible = false;
            _barcodeReader.BarcodesDetected -= OnBarcodesDetected;
        }

        cameraHost.Children.Clear();
        _barcodeReader = null;
    }

    private CameraBarcodeReaderView CreateCameraView()
    {
        var view = new CameraBarcodeReaderView
        {
            CameraLocation = CameraLocation.Rear,
            Options = _barcodeOptions,
            IsDetecting = false
        };
        view.BarcodesDetected += OnBarcodesDetected;
        return view;
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
        if (_barcodeReader != null)
        {
            _barcodeReader.IsDetecting = false;
            _barcodeReader.IsEnabled = false;
            _barcodeReader.IsVisible = false;
        }
        var rawValue = result.Value?.Trim() ?? string.Empty;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
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

                var poiId = ExtractPoiId(rawValue);
                var targetPoi = poiId.HasValue
                    ? _pois.FirstOrDefault(p => p.Poiid == poiId.Value)
                    : null;

                if (targetPoi == null)
                {
                    await DisplayAlert("Thông báo", $"Không tìm thấy quán cho mã: {rawValue}", "OK");
                    if (_barcodeReader != null)
                    {
                        _barcodeReader.IsDetecting = true;
                        _barcodeReader.IsEnabled = true;
                        _barcodeReader.IsVisible = true;
                    }
                    _isHandlingScan = false;
                    return;
                }

                await Shell.Current.Navigation.PushAsync(new DetailPage(targetPoi, true));
            }
            catch (Exception ex)
            {
                await DisplayAlert("Lỗi", "Không thể xử lý mã QR: " + ex.Message, "OK");
                if (_barcodeReader != null)
                {
                    _barcodeReader.IsDetecting = true;
                    _barcodeReader.IsEnabled = true;
                    _barcodeReader.IsVisible = true;
                }
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


