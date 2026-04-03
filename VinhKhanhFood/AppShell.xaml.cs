namespace VinhKhanhFood
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Navigated += OnShellNavigated;
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
