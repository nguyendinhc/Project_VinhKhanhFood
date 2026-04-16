using Microsoft.Maui.Controls.Maps; // Dùng cho Pin và Map
using Microsoft.Maui.Maps;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using VinhKhanhFood.Models;
using VinhKhanhFood.Services;
using ZXing.Net.Maui;

namespace VinhKhanhFood;

public partial class MainPage : ContentPage
{
    private const string GlobalQrUnlockedKey = "HasUnlockedPoiList";
    private static readonly HttpClient _httpClient = new();
    private List<Poi> _allPois = new();
    ApiService _apiService = new ApiService();
    private readonly OfflineSyncService _offlineSyncService;
    bool _isNavigating;
    // Lưu danh sách các quán đã được thuyết minh để không bị nói lặp lại liên tục khi đang đứng yên một chỗ
    private HashSet<int> _spokenPoiIds = new HashSet<int>();
    private IDispatcherTimer? _gpsTimer;
    private bool _isCheckingProximity;
    private bool _isSpeaking;
    private Microsoft.Maui.Controls.Maps.Circle? _userLocationCircle;
    private Microsoft.Maui.Controls.Maps.Circle? _nearestPoiCircle;
    private Polyline? _routePolyline;
    private bool _hasCenteredUserLocation;
    private bool _hasRequestedInitialLocation;
    private Location? _lastKnownLocation;
    private const double TriggerDistanceMeters = 50;
    private const double ResetDistanceMeters = 80;
    public MainPage()
    {
        InitializeComponent();
        _offlineSyncService = new OfflineSyncService(_apiService);
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
            Poi? detailPoi = null;

            if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            {
                try
                {
                    detailPoi = await _apiService.GetPoiByIdAsync(poi.Poiid);
                }
                catch
                {
                    detailPoi = null;
                }
            }

            if (detailPoi != null)
            {
                detailPoi.Introduction = detailPoi.Poilocalizations?.FirstOrDefault()?.Description
                                         ?? poi.Introduction
                                         ?? "Chào mừng bạn đến với " + detailPoi.Name;
                detailPoi.Description = poi.Description ?? "Địa điểm tham quan hấp dẫn tại Vĩnh Khánh";
            }

            await Navigation.PushAsync(new DetailPage(detailPoi ?? poi));
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

            List<Poi> data = new();
            bool apiFailed = false;
            if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            {
                try
                {
                    await _offlineSyncService.SyncPoisAsync();
                }
                catch
                {
                    apiFailed = true;
                }
            }

            data = await _offlineSyncService.LoadPoisAsync();

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
                if (apiFailed)
                {
                    await DisplayAlert("Thông báo", "Không kết nối được máy chủ và chưa có dữ liệu offline.", "OK");
                }
                else
                {
                    await DisplayAlert("Thông báo", "API trả về rỗng, chưa có địa điểm để hiển thị.", "OK");
                }
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
        ApplyPoiListVisibility();
        _ = _apiService.EnsureFirstOpenLoggedAsync();
        _ = _offlineSyncService.ProcessPendingActionsAsync();
        if (lstPois.ItemsSource is IEnumerable<Poi> pois && pois.Any())
        {
            StartGpsTimer();
            _ = DrawPendingRouteAsync();
        }

        if (!_hasRequestedInitialLocation)
        {
            _hasRequestedInitialLocation = true;
            _ = EnsureUserLocationAsync();
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

    private async Task DrawPendingRouteAsync()
    {
        int pendingPoiId = Preferences.Default.Get("PendingRoutePoiId", -1);
        if (pendingPoiId <= 0 || _allPois == null || !_allPois.Any())
        {
            return;
        }

        var targetPoi = _allPois.FirstOrDefault(p => p.Poiid == pendingPoiId);
        if (targetPoi == null)
        {
            return;
        }

        Preferences.Default.Remove("PendingRoutePoiId");
        await DrawRouteToPoiAsync(targetPoi);
    }

    private async Task DrawRouteToPoiAsync(Poi targetPoi)
    {
        bool hasPermission = await CheckAndRequestLocationPermission();
        if (!hasPermission)
        {
            return;
        }

        var request = new GeolocationRequest(GeolocationAccuracy.High, TimeSpan.FromSeconds(8));
        var userLocation = await Geolocation.Default.GetLocationAsync(request);
        if (userLocation == null)
        {
            await DisplayAlert("Thông báo", "Không lấy được vị trí hiện tại để vẽ đường đi.", "OK");
            return;
        }

        UpdateUserLocationOnMap(userLocation);

        var destination = new Location(targetPoi.Latitude, targetPoi.Longitude);
        List<Location> routePoints;

        if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
        {
            routePoints = await GetRoadRoutePointsAsync(userLocation, destination);
            if (routePoints.Count < 2)
            {
                routePoints = new List<Location> { userLocation, destination };
            }
        }
        else
        {
            routePoints = new List<Location> { userLocation, destination };
            await DisplayAlert("Thông báo", "Đang offline, hiển thị đường thẳng đến quán gần nhất.", "OK");
        }

        if (routePoints.Count < 2)
        {
            await DisplayAlert("Thông báo", "Không lấy được lộ trình theo đường đi. Vui lòng thử lại.", "OK");
            return;
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_routePolyline != null)
            {
                myMap.MapElements.Remove(_routePolyline);
            }

            _routePolyline = new Polyline
            {
                StrokeColor = Colors.DodgerBlue,
                StrokeWidth = 7
            };

            foreach (var point in routePoints)
            {
                _routePolyline.Geopath.Add(point);
            }

            myMap.MapElements.Add(_routePolyline);

            var minLat = routePoints.Min(p => p.Latitude);
            var maxLat = routePoints.Max(p => p.Latitude);
            var minLon = routePoints.Min(p => p.Longitude);
            var maxLon = routePoints.Max(p => p.Longitude);

            var center = new Location((minLat + maxLat) / 2, (minLon + maxLon) / 2);

            var corner1 = new Location(minLat, minLon);
            var corner2 = new Location(maxLat, maxLon);
            var diagonalKm = Location.CalculateDistance(corner1, corner2, DistanceUnits.Kilometers);
            var radiusKm = Math.Max(0.2, (diagonalKm / 2) + 0.1);


            myMap.MoveToRegion(MapSpan.FromCenterAndRadius(center, Distance.FromKilometers(radiusKm)));
        });
    }

    private async Task<List<Location>> GetRoadRoutePointsAsync(Location origin, Location destination)
    {
        var points = new List<Location>();

        try
        {
            string originLon = origin.Longitude.ToString(CultureInfo.InvariantCulture);
            string originLat = origin.Latitude.ToString(CultureInfo.InvariantCulture);
            string destinationLon = destination.Longitude.ToString(CultureInfo.InvariantCulture);
            string destinationLat = destination.Latitude.ToString(CultureInfo.InvariantCulture);

            string url = $"https://router.project-osrm.org/route/v1/driving/{originLon},{originLat};{destinationLon},{destinationLat}?overview=full&geometries=geojson";

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return points;
            }

            var json = await response.Content.ReadAsStringAsync();
            var root = JObject.Parse(json);
            var coordinates = root["routes"]?.FirstOrDefault()?["geometry"]?["coordinates"] as JArray;

            if (coordinates == null)
            {
                return points;
            }

            foreach (var item in coordinates)
            {
                if (item is JArray pair && pair.Count >= 2)
                {
                    double lon = pair[0]?.Value<double>() ?? 0;
                    double lat = pair[1]?.Value<double>() ?? 0;
                    points.Add(new Location(lat, lon));
                }
            }
        }
        catch
        {
        }

        return points;
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

            MarkNearestPoiCircle(nearestPoi);

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
                    string currentLangCode = Preferences.Default.Get("AppLanguage", "vi");

                    string? localizedDesc = nearestPoi.Poilocalizations?
                        .FirstOrDefault(x => x.LanguageCode.ToLower() == currentLangCode)?
                        .Description;

                    string speechText = localizedDesc ?? $"Bạn đang ở gần {nearestPoi.Name}. {nearestPoi.Introduction}";

                    var locales = await TextToSpeech.Default.GetLocalesAsync();
                    var selectedLocale = locales.FirstOrDefault(l => l.Language.ToLower().Contains(currentLangCode));

                    await TextToSpeech.Default.SpeakAsync(speechText, new SpeechOptions
                    {
                        Locale = selectedLocale
                    });
                }
                finally
                {
                    _isSpeaking = false;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Lỗi GPS: {ex.Message}");
        }
        finally
        {
            _isCheckingProximity = false;
        }
    }

    private void MarkNearestPoiCircle(Poi? nearestPoi)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (nearestPoi == null)
            {
                if (_nearestPoiCircle != null)
                {
                    myMap.MapElements.Remove(_nearestPoiCircle);
                    _nearestPoiCircle = null;
                }
                return;
            }

            var nearestLocation = new Location(nearestPoi.Latitude, nearestPoi.Longitude);

            if (_nearestPoiCircle == null)
            {
                _nearestPoiCircle = new Microsoft.Maui.Controls.Maps.Circle
                {
                    Center = nearestLocation,
                    Radius = Distance.FromKilometers(0.03),
                    FillColor = Color.FromRgba(255, 0, 0, 80),
                    StrokeColor = Colors.Red,
                    StrokeWidth = 3
                };
                myMap.MapElements.Add(_nearestPoiCircle);
            }
            else
            {
                _nearestPoiCircle.Center = nearestLocation;
            }
        });
    }


    private void UpdateUserLocationOnMap(Location location)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _lastKnownLocation = location;

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

    private async Task EnsureUserLocationAsync()
    {
        bool hasPermission = await CheckAndRequestLocationPermission();
        if (!hasPermission)
        {
            return;
        }

        var lastKnown = await Geolocation.Default.GetLastKnownLocationAsync();
        if (lastKnown != null)
        {
            _hasCenteredUserLocation = false;
            UpdateUserLocationOnMap(lastKnown);
        }

        var request = new GeolocationRequest(GeolocationAccuracy.High, TimeSpan.FromSeconds(8));
        var currentLocation = await Geolocation.Default.GetLocationAsync(request);
        if (currentLocation != null)
        {
            _hasCenteredUserLocation = false;
            UpdateUserLocationOnMap(currentLocation);
        }
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

        var keyword = NormalizeSearchText(e.NewTextValue);
        if (string.IsNullOrWhiteSpace(keyword))
        {
            lstPois.ItemsSource = _allPois;
        }
        else
        {
            lstPois.ItemsSource = _allPois
                .Where(p => NormalizeSearchText(p.Name).Contains(keyword))
                .ToList();
        }
    }

    private static string NormalizeSearchText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new System.Text.StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(ch);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
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

    private void ApplyPoiListVisibility()
    {
        var isUnlocked = Preferences.Default.Get(GlobalQrUnlockedKey, false);
        poiListPanel.IsVisible = isUnlocked;
        globalQrOverlay.IsVisible = !isUnlocked;
    }

    private async void OnScanGlobalQrClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//scanqr");
    }
}
