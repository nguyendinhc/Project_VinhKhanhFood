using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Networking;

namespace VinhKhanhFood
{
    public partial class AppShell : Shell
    {
        private bool _hasShownWelcomeMessage;
        private bool _hasShownInitialConnectivity;
        private bool _isShowingConnectivityAlert;
        private NetworkAccess _lastNetworkAccess;

        public AppShell()
        {
            InitializeComponent();
            Navigated += OnShellNavigated;
            Appearing += OnShellAppearing;
            _lastNetworkAccess = Connectivity.Current.NetworkAccess;
            Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
        }

        private async void OnShellAppearing(object? sender, EventArgs e)
        {
            if (!_hasShownInitialConnectivity)
            {
                _hasShownInitialConnectivity = true;
                if (_lastNetworkAccess != NetworkAccess.Internet)
                {
                    await ShowConnectivityMessageAsync("Thông báo", "Ứng dụng đang sử dụng dữ liệu offline. Vui lòng kiểm tra kết nối.");
                }
            }

            if (_hasShownWelcomeMessage)
            {
                return;
            }

            var message = Preferences.Default.Get("PendingWelcomeMessage", string.Empty);
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            _hasShownWelcomeMessage = true;
            Preferences.Default.Remove("PendingWelcomeMessage");
            await ShowWelcomeDialogAsync(message);
        }

        private async void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
        {
            if (e.NetworkAccess == _lastNetworkAccess)
            {
                return;
            }

            _lastNetworkAccess = e.NetworkAccess;

            if (e.NetworkAccess == NetworkAccess.Internet)
            {
                await ShowConnectivityMessageAsync("Thành công", "Kết nối internet đã được khôi phục.");
            }
            else
            {
                await ShowConnectivityMessageAsync("Thông báo", "Ứng dụng đang sử dụng dữ liệu offline. Vui lòng kiểm tra kết nối.");
            }
        }

        private async Task ShowConnectivityMessageAsync(string title, string message)
        {
            if (_isShowingConnectivityAlert)
            {
                return;
            }

            _isShowingConnectivityAlert = true;
            try
            {
                await MainThread.InvokeOnMainThreadAsync(() => ShowConnectivityDialogAsync(title, message));
            }
            finally
            {
                _isShowingConnectivityAlert = false;
            }
        }

        private async Task ShowConnectivityDialogAsync(string title, string message)
        {
            var overlay = new ContentPage
            {
                BackgroundColor = Color.FromRgba(0, 0, 0, 0.35)
            };

            var dialogBackgroundColor = Color.FromArgb("#FFEDD5");
            var closeSignal = new TaskCompletionSource();

            var closeButton = new Button
            {
                Text = "Đã hiểu",
                BackgroundColor = Color.FromArgb("#FF5722"),
                TextColor = Colors.White,
                FontAttributes = FontAttributes.Bold,
                CornerRadius = 14,
                HeightRequest = 48
            };

            var navigation = await GetNavigationWhenReadyAsync();
            if (navigation == null)
            {
                return;
            }
            closeButton.Clicked += async (_, _) =>
            {
                try
                {
                    if (navigation != null && navigation.ModalStack.Contains(overlay))
                    {
                        await navigation.PopModalAsync();
                    }
                }
                finally
                {
                    closeSignal.TrySetResult();
                }
            };

            overlay.Content = new Grid
            {
                Children =
                {
                    new Border
                    {
                        BackgroundColor = dialogBackgroundColor,
                        StrokeThickness = 0,
                        StrokeShape = new RoundRectangle { CornerRadius = 20 },
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center,
                        WidthRequest = 320,
                        Padding = new Thickness(24),
                        Content = new VerticalStackLayout
                        {
                            Spacing = 16,
                            Children =
                            {
                                new Label
                                {
                                    Text = title,
                                    FontSize = 24,
                                    FontAttributes = FontAttributes.Bold,
                                    TextColor = Colors.Black,
                                    HorizontalTextAlignment = TextAlignment.Center
                                },
                                new Label
                                {
                                    Text = message,
                                    FontSize = 18,
                                    TextColor = Colors.Black,
                                    HorizontalTextAlignment = TextAlignment.Center
                                },
                                closeButton
                            }
                        }
                    }
                }
            };

            await navigation.PushModalAsync(overlay, false);
            await closeSignal.Task;
        }

        private async Task ShowWelcomeDialogAsync(string message)
        {
            var overlay = new ContentPage
            {
                BackgroundColor = Color.FromRgba(0, 0, 0, 0.35)
            };

            var dialogBackgroundColor = Color.FromArgb("#FFEDD5");

            var closeSignal = new TaskCompletionSource();

            var closeButton = new Button
            {
                Text = "Bắt đầu",
                BackgroundColor = Color.FromArgb("#FF5722"),
                TextColor = Colors.White,
                FontAttributes = FontAttributes.Bold,
                CornerRadius = 14,
                HeightRequest = 48
            };

            var navigation = await GetNavigationWhenReadyAsync();
            if (navigation == null)
            {
                return;
            }
            closeButton.Clicked += async (_, _) =>
            {
                try
                {
                    if (navigation != null && navigation.ModalStack.Contains(overlay))
                    {
                        await navigation.PopModalAsync();
                    }
                }
                finally
                {
                    closeSignal.TrySetResult();
                }
            };

            overlay.Content = new Grid
            {
                Children =
                {
                    new Border
                    {
                        BackgroundColor = dialogBackgroundColor,
                        StrokeThickness = 0,
                        StrokeShape = new RoundRectangle { CornerRadius = 20 },
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center,
                        WidthRequest = 320,
                        Padding = new Thickness(24),
                        Content = new VerticalStackLayout
                        {
                            Spacing = 16,
                            Children =
                            {
                                new Label
                                {
                                    Text = "🎉 Đăng nhập thành công",
                                    FontSize = 24,
                                    FontAttributes = FontAttributes.Bold,
                                    TextColor = Colors.Black,
                                    HorizontalTextAlignment = TextAlignment.Center
                                },
                                new Label
                                {
                                    Text = message,
                                    FontSize = 18,
                                    TextColor = Colors.Black,
                                    HorizontalTextAlignment = TextAlignment.Center
                                },
                                closeButton
                            }
                        }
                    }
                }
            };

            await navigation.PushModalAsync(overlay, false);
            await closeSignal.Task;
        }

        private static async Task<INavigation?> GetNavigationWhenReadyAsync()
        {
            var deadline = DateTime.UtcNow.AddSeconds(3);
            while (DateTime.UtcNow < deadline)
            {
                var page = Shell.Current?.CurrentPage ?? Application.Current?.MainPage;
                if (page is Page rootPage && rootPage.Handler != null && rootPage.IsLoaded)
                {
                    return Shell.Current?.Navigation ?? rootPage.Navigation;
                }

                await Task.Delay(100);
            }

            return null;
        }

        private async void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
        {
            if (e.Source != ShellNavigationSource.ShellItemChanged)
            {
                return;
            }

            var location = CurrentState?.Location?.OriginalString ?? string.Empty;
            if (location.StartsWith("//scanqr", StringComparison.OrdinalIgnoreCase))
            {
                if (Navigation.NavigationStack.Count > 1)
                {
                    await Navigation.PopToRootAsync();
                }
            }
        }
    }
}
