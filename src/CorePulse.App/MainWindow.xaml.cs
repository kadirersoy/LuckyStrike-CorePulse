using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using CorePulse.Core.Services;
using Microsoft.Web.WebView2.Core;

namespace CorePulse.App;

public partial class MainWindow : Window
{
    // ==================== WIN32 WINDOW DRAG P/INVOKE ====================
    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HTCAPTION = 0x2;

    // ==================== FIELDS ====================
    public static void Log(string msg)
    {
        System.Diagnostics.Debug.WriteLine(msg);
    }

    private readonly LiveHardwareMonitor _liveMonitor;
    private NativeTrayIcon? _trayIcon;
    private CoreWebView2Environment? _webViewEnv;
    private bool _isWebViewReady;
    private bool _isExplicitExit;
    private HwndSource? _hwndSource;

    public MainWindow()
    {
        InitializeComponent();

        Log("Application constructor initialized.");
        _liveMonitor = new LiveHardwareMonitor { LogAction = Log };

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        StateChanged += MainWindow_StateChanged;
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            RootBorder.Padding = new Thickness(7);
            RootBorder.BorderThickness = new Thickness(0);
        }
        else
        {
            RootBorder.Padding = new Thickness(0);
            RootBorder.BorderThickness = new Thickness(1);
        }
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Log("MainWindow_Loaded called.");
        // Setup Win32 message hook for Tray Icon
        var helper = new WindowInteropHelper(this);
        _hwndSource = HwndSource.FromHwnd(helper.Handle);
        _hwndSource?.AddHook(WndProc);

        InitializeTrayIcon(helper.Handle);

        await InitializeWebViewAsync();

        _liveMonitor.TelemetryUpdated += OnTelemetryUpdated;
        _liveMonitor.Start();
        Log("LiveHardwareMonitor started.");
    }

    private void InitializeTrayIcon(IntPtr hwnd)
    {
        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        if (!File.Exists(iconPath))
        {
            iconPath = Path.Combine(AppContext.BaseDirectory, "app.ico");
        }

        _trayIcon = new NativeTrayIcon(hwnd, iconPath, "CorePulse - Donanım İzleme Merkezi");
        _trayIcon.LeftClicked += ShowAndRestoreWindow;
        _trayIcon.RightClicked += ShowTrayContextMenu;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (_trayIcon != null && msg == NativeTrayIcon.WM_TRAYICON)
        {
            _trayIcon.HandleMessage(msg, lParam);
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void ShowAndRestoreWindow()
    {
        Dispatcher.Invoke(() =>
        {
            Show();
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }
            Activate();
        });
    }

    private void ShowTrayContextMenu()
    {
        Dispatcher.Invoke(() =>
        {
            var menu = new ContextMenu();

            var itemShow = new MenuItem { Header = "📊 Göster / Aç" };
            itemShow.Click += (s, e) => ShowAndRestoreWindow();

            var itemRam = new MenuItem { Header = "⚡ Standby Bellek Temizle" };
            itemRam.Click += (s, e) =>
            {
                var res = MemoryOptimizer.PurgeStandbyMemory();
                SendToastToWeb(res.Message);
            };

            var itemExit = new MenuItem { Header = "✕ Çıkış" };
            itemExit.Click += (s, e) => RealExitApplication();

            menu.Items.Add(itemShow);
            menu.Items.Add(itemRam);
            menu.Items.Add(new Separator());
            menu.Items.Add(itemExit);

            menu.IsOpen = true;
        });
    }

    private void RealExitApplication()
    {
        _isExplicitExit = true;
        _trayIcon?.Dispose();
        _liveMonitor.Stop();
        _liveMonitor.Dispose();
        System.Windows.Application.Current.Shutdown();
    }

    private async Task InitializeWebViewAsync()
    {
        try
        {
            Log("InitializeWebViewAsync started.");
            var dataFolder = Path.Combine(Path.GetTempPath(), "CorePulse_WV2");
            _webViewEnv = await CoreWebView2Environment.CreateAsync(null, dataFolder);
            await MainWebView.EnsureCoreWebView2Async(_webViewEnv);
            Log("EnsureCoreWebView2Async finished.");

            MainWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            MainWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            MainWebView.CoreWebView2.Settings.AreDevToolsEnabled = true;

            MainWebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            MainWebView.CoreWebView2.NewWindowRequested += OnNewWindowRequested;

            MainWebView.CoreWebView2.NavigationCompleted += (s, e) =>
            {
                Log($"NavigationCompleted: IsSuccess={e.IsSuccess}, WebErrorStatus={e.WebErrorStatus}");
                _isWebViewReady = true;
                _liveMonitor.CollectAndBroadcast();
            };

            string htmlPath = Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html");
            if (!File.Exists(htmlPath))
            {
                string altPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\tests\corepulse_modular_ui.html"));
                if (File.Exists(altPath)) htmlPath = altPath;
            }

            Log($"Navigating to: {htmlPath} (Exists: {File.Exists(htmlPath)})");
            if (File.Exists(htmlPath))
            {
                MainWebView.CoreWebView2.Navigate(new Uri(htmlPath).AbsoluteUri);
            }
        }
        catch (Exception ex)
        {
            Log($"WebView2 exception: {ex}");
            MessageBox.Show($"WebView2 başlatma hatası: {ex.Message}", "CorePulse Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        var deferral = e.GetDeferral();
        try
        {
            double winW = e.WindowFeatures.HasSize && e.WindowFeatures.Width > 150 ? e.WindowFeatures.Width : 480;
            double winH = e.WindowFeatures.HasSize && e.WindowFeatures.Height > 120 ? e.WindowFeatures.Height + 38 : 360;

            var bgDark = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(8, 12, 20));
            var borderSlate = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 41, 59));
            var btnBg = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(15, 23, 42));
            var btnHover = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 41, 59));
            var btnCloseHover = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(159, 18, 57));
            var textLight = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(226, 232, 240));
            var textMuted = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(148, 163, 184));
            var cyanAccent = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(34, 211, 238));

            var popWebView = new Microsoft.Web.WebView2.Wpf.WebView2
            {
                DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 8, 12, 20),
                Margin = new Thickness(1)
            };

            var popWin = new Window
            {
                Title = "CorePulse - Canlı HUD",
                Width = winW,
                Height = winH,
                MinWidth = 240,
                MinHeight = 160,
                Background = bgDark,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = false,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            System.Windows.Shell.WindowChrome.SetWindowChrome(popWin, new System.Windows.Shell.WindowChrome
            {
                GlassFrameThickness = new Thickness(0),
                CornerRadius = new CornerRadius(0),
                CaptionHeight = 0,
                ResizeBorderThickness = new Thickness(6),
                UseAeroCaptionButtons = false
            });

            // Custom Dark Theme Title Bar matching CorePulse Main Header
            var titleText = new TextBlock
            {
                Text = "CorePulse - Canlı HUD",
                Foreground = textLight,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            var badgeDot = new System.Windows.Shapes.Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = cyanAccent,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };

            var leftTitleStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 8, 0)
            };
            leftTitleStack.Children.Add(badgeDot);
            leftTitleStack.Children.Add(titleText);

            Border CreateCaptionButton(string symbol, string tooltip, System.Windows.Media.Brush hoverBrush, Action onClick)
            {
                var lbl = new TextBlock
                {
                    Text = symbol,
                    Foreground = textMuted,
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                var btnBorder = new Border
                {
                    Width = 26,
                    Height = 24,
                    CornerRadius = new CornerRadius(6),
                    Background = btnBg,
                    Margin = new Thickness(2, 0, 2, 0),
                    ToolTip = tooltip,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Child = lbl
                };
                btnBorder.MouseEnter += (_, _) =>
                {
                    btnBorder.Background = hoverBrush;
                    lbl.Foreground = System.Windows.Media.Brushes.White;
                };
                btnBorder.MouseLeave += (_, _) =>
                {
                    btnBorder.Background = btnBg;
                    lbl.Foreground = textMuted;
                };
                btnBorder.MouseLeftButtonDown += (_, args) =>
                {
                    args.Handled = true;
                    onClick();
                };
                return btnBorder;
            }

            var rightButtonsStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(4, 0, 8, 0)
            };
            rightButtonsStack.Children.Add(CreateCaptionButton("—", "Simge Durumuna Küçült", btnHover, () => popWin.WindowState = WindowState.Minimized));
            rightButtonsStack.Children.Add(CreateCaptionButton("▢", "Ekranı Kapla / Geri Yükle", btnHover, () =>
            {
                popWin.WindowState = popWin.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            }));
            rightButtonsStack.Children.Add(CreateCaptionButton("✕", "Kapat", btnCloseHover, () => popWin.Close()));

            var headerGrid = new Grid { Background = bgDark, Height = 36 };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(leftTitleStack, 0);
            Grid.SetColumn(rightButtonsStack, 1);
            headerGrid.Children.Add(leftTitleStack);
            headerGrid.Children.Add(rightButtonsStack);

            var headerBorder = new Border
            {
                Background = bgDark,
                BorderBrush = borderSlate,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = headerGrid
            };

            headerBorder.MouseLeftButtonDown += (_, args) =>
            {
                if (args.ClickCount == 2)
                {
                    popWin.WindowState = popWin.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                }
                else if (args.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
                {
                    try { popWin.DragMove(); } catch { }
                }
            };

            var rootGrid = new Grid { Background = bgDark };
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(headerBorder, 0);
            Grid.SetRow(popWebView, 1);
            rootGrid.Children.Add(headerBorder);
            rootGrid.Children.Add(popWebView);

            popWin.Content = new Border
            {
                Background = bgDark,
                BorderBrush = borderSlate,
                BorderThickness = new Thickness(1),
                Child = rootGrid
            };

            if (Icon != null)
            {
                popWin.Icon = Icon;
            }

            popWin.Show();

            await popWebView.EnsureCoreWebView2Async(_webViewEnv);
            popWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            popWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            popWebView.CoreWebView2.DocumentTitleChanged += (s2, e2) =>
            {
                if (!string.IsNullOrWhiteSpace(popWebView.CoreWebView2.DocumentTitle))
                {
                    popWin.Title = popWebView.CoreWebView2.DocumentTitle;
                    titleText.Text = popWebView.CoreWebView2.DocumentTitle;
                }
            };

            e.NewWindow = popWebView.CoreWebView2;
            e.Handled = true;
        }
        catch (Exception ex)
        {
            Log($"OnNewWindowRequested error: {ex.Message}");
            e.Handled = false;
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var doc = JsonDocument.Parse(e.WebMessageAsJson);
            var root = doc.RootElement;
            if (root.TryGetProperty("action", out var actionProp))
            {
                var action = actionProp.GetString();
                Log($"OnWebMessageReceived action: {action}");
                switch (action)
                {
                    case "minimize":
                        Dispatcher.Invoke(() => { WindowState = WindowState.Minimized; });
                        break;
                    case "maximize":
                        Dispatcher.Invoke(() => { WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; });
                        break;
                    case "close":
                        // Requirement 5: X kapatınca sadece açık pencereyi kapatır, saatin yanında icon kalır
                        Dispatcher.Invoke(() => { Hide(); });
                        break;
                    case "drag":
                        Dispatcher.Invoke(() =>
                        {
                            if (WindowState != WindowState.Maximized)
                            {
                                var helper = new WindowInteropHelper(this);
                                ReleaseCapture();
                                SendMessage(helper.Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
                            }
                        });
                        break;
                    case "clearStandbyRam":
                        var res = MemoryOptimizer.PurgeStandbyMemory();
                        SendToastToWeb(res.Message);
                        break;
                    case "ready":
                        _isWebViewReady = true;
                        _liveMonitor.CollectAndBroadcast();
                        break;
                    case "log":
                        if (root.TryGetProperty("text", out var textProp))
                            Log($"[JS] {textProp.GetString()}");
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Log($"OnWebMessageReceived error: {ex.Message}");
        }
    }

    private void SendToastToWeb(string message)
    {
        Dispatcher.Invoke(() =>
        {
            if (_isWebViewReady && MainWebView.CoreWebView2 != null)
            {
                var script = $"showToast({JsonSerializer.Serialize(message)});";
                MainWebView.CoreWebView2.ExecuteScriptAsync(script);
            }
        });
    }

    private void OnTelemetryUpdated(Dictionary<string, SensorWebValue> telemetry)
    {
        try
        {
            var json = JsonSerializer.Serialize(telemetry);

            Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    if (!_isWebViewReady || MainWebView?.CoreWebView2 == null)
                        return;

                    MainWebView.CoreWebView2.PostWebMessageAsJson(json);
                }
                catch (Exception ex)
                {
                    Log($"Dispatch to WebView2 error: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            Log($"OnTelemetryUpdated serialization error: {ex.Message}");
        }
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_isExplicitExit)
        {
            e.Cancel = true;
            Hide(); // Requirement 5: Sadece pencereyi gizle, saatin yanındaki tray ikonu açık kalır
        }
        else
        {
            _trayIcon?.Dispose();
            _liveMonitor.Stop();
            _liveMonitor.Dispose();
        }
    }
}
