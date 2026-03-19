using Microsoft.Maui.Controls.Maps; // Dùng cho Pin và Map
using Microsoft.Maui.Maps;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using VinhKhanhFood.Models;
using VinhKhanhFood.Services;
using ZXing.Net.Maui;

namespace VinhKhanhFood;

public partial class MainPage : ContentPage
{
    private List<Poi> _allPois;
    ApiService _apiService = new ApiService();
    bool _isNavigating;
    // Lưu danh sách các quán đã được thuyết minh để không bị nói lặp lại liên tục khi đang đứng yên một chỗ
    private HashSet<int> _spokenPoiIds = new HashSet<int>();
    private IDispatcherTimer? _gpsTimer;
    private bool _isCheckingProximity;
    private bool _isSpeaking;
    private Microsoft.Maui.Controls.Maps.Circle? _userLocationCircle;
    private bool _hasCenteredUserLocation;
    private const double TriggerDistanceMeters = 50;
    private const double ResetDistanceMeters = 80;
    public MainPage()
    {
        InitializeComponent();
        LoadData();
    }

    private async void OnPoiSelected(object sender, SelectionChangedEventArgs e)
    {
        var selectedPoi = e.CurrentSelection.FirstOrDefault() as Poi;
        if (selectedPoi != null)
        {
            ((CollectionView)sender).SelectedItem = null;
            await NavigateToDetailAsync(selectedPoi);
        }
    }

    private async Task NavigateToDetailAsync(Poi poi)
    {
        if (_isNavigating)
        {
            return;
        }

        _isNavigating = true;
        try
        {
            await Navigation.PushAsync(new DetailPage(poi));
        }
        finally
        {
            _isNavigating = false;
        }
    }

    async void LoadData()
    {
        try
        {
            bool hasPermission = await CheckAndRequestLocationPermission();
            if (!hasPermission) return;

            myMap.IsShowingUser = true;

            var data = await _apiService.GetPoisAsync();
            if (data != null && data.Any())
            {
                foreach (var item in data)
                {
                    item.Introduction = item.Poilocalizations?.FirstOrDefault()?.Description
                                        ?? "Chào mừng bạn đến với " + item.Name;
                    item.Description = "Địa điểm tham quan hấp dẫn tại Vĩnh Khánh";
                }

                lstPois.ItemsSource = data;
                _allPois = data.ToList();
                myMap.Pins.Clear();
                foreach (var poi in data)
                {
                    var pin = new Microsoft.Maui.Controls.Maps.Pin
                    {
                        Label = poi.Name,
                        Location = new Location(poi.Latitude, poi.Longitude),
                        Address = "Đường Vĩnh Khánh",
                        Type = PinType.Place
                    };

                    pin.MarkerClicked += async (s, args) =>
                    {
                        args.HideInfoWindow = true;
                        await NavigateToDetailAsync(poi);
                    };

                    myMap.Pins.Add(pin);
                }

                var request = new GeolocationRequest(GeolocationAccuracy.High, TimeSpan.FromSeconds(8));
                var userLocation = await Geolocation.Default.GetLocationAsync(request);
                if (userLocation != null)
                {
                    UpdateUserLocationOnMap(userLocation);
                }
                else
                {
                    var centerLocation = new Location(10.7600, 106.7050);
                    myMap.MoveToRegion(MapSpan.FromCenterAndRadius(centerLocation, Distance.FromKilometers(0.5)));
                }

                StartGpsTimer();
            }
            else
            {
                await DisplayAlert("Thông báo", "API trả về rỗng, chưa có địa điểm để hiển thị.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Lỗi", "Không thể tải dữ liệu: " + ex.Message, "OK");
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (lstPois.ItemsSource is IEnumerable<Poi> pois && pois.Any())
        {
            StartGpsTimer();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopGpsTimer();
    }

    private void StartGpsTimer()
    {
        if (_gpsTimer == null)
        {
            _gpsTimer = Dispatcher.CreateTimer();
            _gpsTimer.Interval = TimeSpan.FromSeconds(5);
            _gpsTimer.Tick += async (s, e) => await CheckProximityAndSpeak();
        }

        if (!_gpsTimer.IsRunning)
        {
            _gpsTimer.Start();
        }

        _ = CheckProximityAndSpeak();
    }

    private void StopGpsTimer()
    {
        if (_gpsTimer?.IsRunning == true)
        {
            _gpsTimer.Stop();
        }
    }

    // hàm qr code click 
    
    // hàm kiểm tra và yêu câù quyền định vị của người dùng 
    private async Task<bool> CheckAndRequestLocationPermission()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();

        if (status != PermissionStatus.Granted)
        {
            status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        }

        if (status != PermissionStatus.Granted)
        {
            await DisplayAlert("Thông báo", "App cần quyền định vị để tự động thuyết minh khi bạn đến gần quán ăn.", "OK");
            return false;
        }
        return true;
    }
    // check kiểm khoảnh cách và tự động nói khi đến gần quán 
    private async Task CheckProximityAndSpeak()
    {
        if (_isCheckingProximity)
        {
            return;
        }

        _isCheckingProximity = true;
        try
        {
            // 1. Lấy vị trí hiện tại của người dùng (Độ chính xác trung bình để tiết kiệm pin)
            var request = new GeolocationRequest(GeolocationAccuracy.High, TimeSpan.FromSeconds(8));
            var location = await Geolocation.Default.GetLocationAsync(request);

            if (location == null) return;

            UpdateUserLocationOnMap(location);

            // 2. Lấy danh sách quán từ CollectionView
            var pois = (lstPois.ItemsSource as IEnumerable<Poi>)?.ToList();
            if (pois == null) return;

            Poi? nearestPoi = null;
            double nearestDistanceInMeters = double.MaxValue;

            foreach (var poi in pois)
            {
                // 3. Tính khoảng cách giữa Người dùng và Quán (đơn vị: Kilometers)
                double distanceInKm = Location.CalculateDistance(location,
                                       new Location(poi.Latitude, poi.Longitude),
                                       DistanceUnits.Kilometers);

                double distanceInMeters = distanceInKm * 1000; // Đổi sang mét

                if (distanceInMeters < nearestDistanceInMeters)
                {
                    nearestDistanceInMeters = distanceInMeters;
                    nearestPoi = poi;
                }

                if (distanceInMeters > ResetDistanceMeters)
                {
                    _spokenPoiIds.Remove(poi.Poiid);
                }
            }

            // 4. Chỉ thuyết minh quán gần nhất trong bán kính trigger
            if (nearestPoi != null
                && nearestDistanceInMeters <= TriggerDistanceMeters
                && !_spokenPoiIds.Contains(nearestPoi.Poiid)
                && !_isSpeaking)
            {
                _spokenPoiIds.Add(nearestPoi.Poiid);
                _isSpeaking = true;
                try
                {
                    string speechText = $"Bạn đang ở gần {nearestPoi.Name}. {nearestPoi.Introduction}";
                    await TextToSpeech.Default.SpeakAsync(speechText);
                }
                finally
                {
                    _isSpeaking = false;
                }
            }
        }
        catch (Exception ex)
        {
            // Xử lý nếu người dùng tắt GPS hoặc lỗi quyền truy cập
            System.Diagnostics.Debug.WriteLine($"Lỗi GPS: {ex.Message}");
        }
        finally
        {
            _isCheckingProximity = false;
        }
    }

    private void UpdateUserLocationOnMap(Location location)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            lblCurrentLocation.Text = $"Vị trí hiện tại: {location.Latitude:F6}, {location.Longitude:F6}";

            if (_userLocationCircle == null)
            {
                _userLocationCircle = new Microsoft.Maui.Controls.Maps.Circle
                {
                    Center = location,
                    Radius = new Distance(12),
                    FillColor = Color.FromRgba(30, 144, 255, 90),
                    StrokeColor = Colors.DodgerBlue,
                    StrokeWidth = 2
                };
                myMap.MapElements.Add(_userLocationCircle);
            }
            else
            {
                _userLocationCircle.Center = location;
            }

            if (!_hasCenteredUserLocation)
            {
                myMap.MoveToRegion(MapSpan.FromCenterAndRadius(location, Distance.FromKilometers(0.2)));
                _hasCenteredUserLocation = true;
            }
        });
    }
    // Hàm xử lý chạm vào quán (bấm 1 lần là ăn)
    private async void OnPoiTapped(object sender, TappedEventArgs e)
    {
        var selectedPoi = e.Parameter as Poi;
        if (selectedPoi != null)
        {
            await Navigation.PushAsync(new DetailPage(selectedPoi));
        }
    }

    // Hàm xử lý thanh tìm kiếm
    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_allPois == null) return;

        var keyword = e.NewTextValue?.ToLower() ?? "";
        if (string.IsNullOrWhiteSpace(keyword))
        {
            lstPois.ItemsSource = _allPois;
        }
        else
        {
            lstPois.ItemsSource = _allPois.Where(p => p.Name.ToLower().Contains(keyword)).ToList();
        }
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