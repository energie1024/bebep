using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace CFSNetworkLauncher;

public partial class MainWindow : Window
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(4) };
    private LauncherConfig _config = new();
    private bool _online;

    public MainWindow()
    {
        InitializeComponent();
        LoadConfig();
        Loaded += async (_, _) => await CheckServerAsync();
    }

    private void LoadConfig()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "config.json");

            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                _config = JsonSerializer.Deserialize<LauncherConfig>(json)
                          ?? new LauncherConfig();
            }

            AddressText.Text = _config.ServerAddress;
        }
        catch
        {
            AddressText.Text = _config.ServerAddress;
        }
    }

    private async Task CheckServerAsync()
    {
        try
        {
            var json = await _http.GetStringAsync(_config.ServerInfoUrl);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var clients = root.TryGetProperty("clients", out var c)
                ? c.GetInt32()
                : 0;

            var max = root.TryGetProperty("sv_maxclients", out var m)
                ? m.GetInt32()
                : 48;

            _online = true;

            StatusDot.Fill = new SolidColorBrush(
                Color.FromRgb(40, 220, 110));

            StatusText.Text = "ONLINE";
            PlayersText.Text = $"{clients} / {max} játékos";
        }
        catch
        {
            _online = false;

            StatusDot.Fill = new SolidColorBrush(
                Color.FromRgb(230, 65, 75));

            StatusText.Text = "OFFLINE / NEM ELÉRHETŐ";
            PlayersText.Text = "";
        }
    }

    private async void Connect_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ConnectButton.IsEnabled = false;

            StatusText.Text = "Launcher hitelesítése...";

            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(5)
            };

            var response = await client.GetAsync(_config.LauncherAuthUrl);

            if (!response.IsSuccessStatusCode)
            {
                MessageBox.Show(
                    "A szerver nem engedélyezte a launcher csatlakozását.\n\n" +
                    "HTTP hiba: " + (int)response.StatusCode,
                    "CFS Network",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                StatusText.Text = "Hitelesítés sikertelen";
                return;
            }

            var result = await response.Content.ReadAsStringAsync();

            if (!result.Contains("\"success\":true"))
            {
                MessageBox.Show(
                    "A launcher hitelesítése sikertelen.",
                    "CFS Network",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                StatusText.Text = "Hitelesítés sikertelen";
                return;
            }

            StatusText.Text = "Csatlakozás a CFS Networkhöz...";

            await Task.Delay(300);

            var uri = $"fivem://connect/{_config.ServerAddress}";

            var psi = new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{uri}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process.Start(psi);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Nem sikerült csatlakozni a CFS Networkhöz.\n\n" +
                ex.Message,
                "CFS Network",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            await Task.Delay(3000);
            ConnectButton.IsEnabled = true;
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Discord_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _config.DiscordUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Nem sikerült megnyitni a Discordot.\n\n" + ex.Message,
                "CFS Network",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ConnectButton.IsEnabled = false;

            StatusText.Text = "Frissítés ellenőrzése...";

            var updateResult = await LauncherUpdater.CheckAndUpdateAsync(
                _config.GitHubLatestReleaseUrl,
                LauncherUpdater.CurrentVersion);

            if (updateResult.Status == UpdateStatus.UpdateAvailable)
            {
                var answer = MessageBox.Show(
                    $"Új launcher verzió érhető el: {updateResult.LatestVersion}\n\n" +
                    "Szeretnéd most frissíteni?",
                    "CFS Network Launcher",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (answer == MessageBoxResult.Yes)
                {
                    StatusText.Text = "Frissítés letöltése...";

                    await LauncherUpdater.DownloadAndInstallAsync(
                        updateResult.DownloadUrl!);

                    return;
                }

                StatusText.Text = "Frissítés kihagyva";
            }
            else if (updateResult.Status == UpdateStatus.UpToDate)
            {
                MessageBox.Show(
                    $"A launcher naprakész.\n\nJelenlegi verzió: {LauncherUpdater.CurrentVersion}",
                    "CFS Network",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                StatusText.Text = "Naprakész";
            }
            else
            {
                MessageBox.Show(
                    "Nem sikerült ellenőrizni a launcher frissítéseit.\n\n" +
                    updateResult.ErrorMessage,
                    "CFS Network",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                StatusText.Text = "Frissítés ellenőrzése sikertelen";
            }

            await CheckServerAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Hiba történt a frissítés ellenőrzése közben.\n\n" + ex.Message,
                "CFS Network",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            StatusText.Text = "Frissítési hiba";
        }
        finally
        {
            ConnectButton.IsEnabled = true;
        }
    }
}

public class LauncherConfig
{
    public string ServerName { get; set; } = "CFS Network";

    public string ServerAddress { get; set; } =
        "87.229.6.49:30120";

    public string DiscordUrl { get; set; } =
        "https://discord.gg/MV379UnmeR";

    public string ServerInfoUrl { get; set; } =
        "http://87.229.6.49:30120/info.json";

    public string LauncherAuthUrl { get; set; } =
        "http://87.229.6.49:30120/cfs-launcher-auth-7f4b9c2e8a61";

    public string GitHubLatestReleaseUrl { get; set; } =
        "https://api.github.com/repos/energie1024/bebep/releases/latest";
}
