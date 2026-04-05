using System.IO;
using System.Text.Json;
using System.Windows;
using MusicWidget.Services;
using MusicWidget.ViewModels;

namespace MusicWidget
{
    /// <summary>
    /// Apple Music ウィジェットのメインウィンドウを表します。
    /// ウィンドウの表示、トレイアイコンの管理、ドラッグ移動やスナップ動作など、アプリケーションの主要な UI 機能を提供します。
    /// </summary>
    ///
    /// <remarks>ウィンドウは最後に配置した位置を記憶し、次回起動時にはその位置から復元されます。
    /// ウィンドウのドラッグ移動時には、画面端へのスナップ機能が有効です。ウィンドウのライフサイクルに合わせて、関連サービスやリソースの管理も行います。
    /// </remarks>
    public partial class MainWindow : Window
    {
        private readonly MediaSessionService _service = new();
        private readonly MainViewModel _vm = new();
        private NotifyIcon? _trayIcon;

        // スナップする距離の閾値（px）
        private const double SnapThreshold = 30;

        private static readonly string PositionFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MusicWidget", "windowpos.json");

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            DataContext = _vm;
            SetupTrayIcon();
            PositionWindow();
            Loaded += OnLoaded;
        }

        /// <summary>
        /// 画面がロードされた際に、MediaSessionService の初期化を行い、トラック情報の変更イベントを購読します。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            _service.TrackChanged += info =>
                Dispatcher.Invoke(() =>
                {
                    _vm.Apply(info);
                    Visualizer.IsPlaying = info.IsPlaying;
                });


            await _service.InitializeAsync();
        }

        /// <summary>
        /// ウィンドウを配置します。前回保存した位置があればそこに復元し、なければ画面下部中央に配置します。
        /// </summary>
        private void PositionWindow()
        {
            if (!TryLoadPosition(out var left, out var top))
            {
                var screen = SystemParameters.WorkArea;
                left = (screen.Width - Width) / 2;
                top = screen.Height - Height;
            }

            Left = left;
            Top = top;
            SavePosition();
        }

        /// <summary>
        /// 現在のウィンドウ位置をファイルに保存します。
        /// </summary>
        private void SavePosition()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PositionFilePath)!);
                var json = JsonSerializer.Serialize(new { Left, Top });
                File.WriteAllText(PositionFilePath, json);
            }
            catch { /* 保存失敗は無視 */ }
        }

        /// <summary>
        /// 保存されたウィンドウ位置を読み込みます。
        /// </summary>
        private static bool TryLoadPosition(out double left, out double top)
        {
            left = 0;
            top = 0;
            try
            {
                if (!File.Exists(PositionFilePath)) return false;
                using var doc = JsonDocument.Parse(File.ReadAllText(PositionFilePath));
                left = doc.RootElement.GetProperty("Left").GetDouble();
                top = doc.RootElement.GetProperty("Top").GetDouble();
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        /// トレイアイコンのセットアップを行います。
        /// アイコンの表示、コンテキストメニューの追加、ダブルクリックイベントの設定などを行い、ユーザーがタスクトレイからウィンドウを操作できるようにします。
        /// </summary>
        private void SetupTrayIcon()
        {
            _trayIcon = new NotifyIcon
            {
                Icon = new System.Drawing.Icon("Resources/tray.ico"),
                Visible = true,
                Text = "Apple Music Widget",
            };

            var menu = new ContextMenuStrip();
            menu.Items.Add("終了", null, (_, _) =>
            {
                _trayIcon.Visible = false;
                System.Windows.Application.Current.Shutdown();
            });
            _trayIcon.ContextMenuStrip = menu;

            // トレイアイコンダブルクリックで表示/非表示トグル
            _trayIcon.DoubleClick += (_, _) =>
            {
                Visibility = Visibility == Visibility.Visible
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            };
        }

        /// <summary>
        /// ウィジェットを閉じる際に、トレイアイコンやサービスなどのリソースを適切に解放します。
        /// </summary>
        /// <param name="e"></param>
        protected override void OnClosed(EventArgs e)
        {
            _trayIcon?.Dispose();
            _service.Dispose();
            base.OnClosed(e);
        }

        /// <summary>
        /// ウィンドウをドラッグ移動し、ドロップ後に画面端へのスナップと位置保存を行います。
        /// DragMove() はドロップまでブロックする同期呼び出しのため、その直後にスナップ処理を実行します。
        /// </summary>
        private void OnDragMove(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState != System.Windows.Input.MouseButtonState.Pressed) return;

            DragMove();

            // DragMove() はドロップ完了まで戻らないため、ここはドロップ後に実行される
            SnapToEdge();
            SavePosition();
        }

        /// <summary>
        /// ウィンドウが画面端に近い場合、自動的に端に揃えます。DPI スケーリングも考慮されます。
        /// </summary>
        private void SnapToEdge()
        {
            // ウィンドウ中心点がどのモニター上にあるかを特定
            var centerX = (int)(Left + Width / 2);
            var centerY = (int)(Top + Height / 2);
            var screen = System.Windows.Forms.Screen.FromPoint(
                              new System.Drawing.Point(centerX, centerY));

            // DPIスケールを考慮してWPF座標に変換
            var source = PresentationSource.FromVisual(this);
            var dpiScaleX = source?.CompositionTarget?.TransformFromDevice.M11 ?? 1.0;
            var dpiScaleY = source?.CompositionTarget?.TransformFromDevice.M22 ?? 1.0;

            var workArea = screen.WorkingArea;
            var areaLeft = workArea.Left * dpiScaleX;
            var areaTop = workArea.Top * dpiScaleY;
            var areaRight = workArea.Right * dpiScaleX;
            var areaBottom = workArea.Bottom * dpiScaleY;

            var newLeft = Left;
            var newTop = Top;

            // 左端・右端へのスナップ
            if (Left < areaLeft + SnapThreshold)
                newLeft = areaLeft;
            else if (Left + Width > areaRight - SnapThreshold)
                newLeft = areaRight - Width;

            // 上端・下端へのスナップ
            if (Top < areaTop + SnapThreshold)
                newTop = areaTop;
            else if (Top + Height > areaBottom - SnapThreshold)
                newTop = areaBottom - Height;

            Left = newLeft;
            Top = newTop;
        }
    }
}