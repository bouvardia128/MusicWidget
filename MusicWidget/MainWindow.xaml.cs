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
    /// <remarks>ウィンドウは画面下部中央に初期配置され、タスクトレイアイコンから表示・非表示を切り替えることができます。
    /// ウィンドウのドラッグ移動時には、画面端へのスナップ機能が有効です。ウィンドウのライフサイクルに合わせて、関連サービスやリソースの管理も行います。
    /// </remarks>
    public partial class MainWindow : Window
    {
        private readonly MediaSessionService _service = new();
        private readonly MainViewModel _vm = new();
        private NotifyIcon? _trayIcon;

        // スナップする距離の閾値（px）
        private const double SnapThreshold = 30;

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
                Dispatcher.Invoke(() => _vm.Apply(info));

            await _service.InitializeAsync();
        }

        /// <summary>
        /// 画面下部中央にウィンドウを配置します。
        /// 画面の作業領域を取得し、ウィンドウの幅と高さを考慮して、適切なLeftとTopの値を計算して設定します。
        /// </summary>
        private void PositionWindow()
        {
            var screen = SystemParameters.WorkArea;
            Left = (screen.Width - Width) / 2;
            Top = screen.Height - Height;
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
        /// マウスボタンイベントを利用して、ウィンドウのドラッグ移動を実装します。
        /// </summary>
        /// <param name="sender">ドラッグ操作を発生させたオブジェクト。通常はウィンドウ自身です。</param>
        /// <param name="e">マウスボタンイベントのデータを含むオブジェクト。</param>
        private void OnDragMove(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
                DragMove();
        }

        /// <summary>
        /// ドラッグ操作の終了時にウィンドウ位置を調整し、画面端にスナップさせます。
        /// </summary>
        /// <remarks>ウィンドウの中心点が属するモニターの作業領域に基づき、ウィンドウが画面端に近い場合は自動的に端に揃えます。DPI スケーリングも考慮されます。</remarks>
        /// <param name="sender">ドラッグ操作を発生させたオブジェクト。通常はウィンドウ自身です。</param>
        /// <param name="e">マウスボタンイベントのデータを含むオブジェクト。</param>
        private void OnDragEnd(object sender, System.Windows.Input.MouseButtonEventArgs e)
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