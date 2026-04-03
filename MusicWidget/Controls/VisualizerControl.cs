using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace MusicWidget.Controls;

/// <summary>
/// ウィンドウ背景に音楽のビジュアライザーを表示するカスタムコントロールです。
/// </summary>
public class VisualizerControl : Canvas
{
    // ── 調整用パラメーター ──────────────────────────────
    private const double VisualizerOpacity = 0.4;
    private const int BarCount = 28;
    private const int SegmentCount = 14;
    private const double GapRatio = 0.25;
    private const double LerpSpeed = 0.5;
    private const double TargetChangePct = 0.5;
    private const double PeakFallSpeed = 0.012;
    private const int PeakHoldFrames = 30;
    private const double ReflectAlpha = 0.35;
    private const double StopFallSpeed = 0.02;
    // ──────────────────────────────────────────

    private readonly double[] _heights = new double[BarCount];
    private readonly double[] _targets = new double[BarCount];
    private readonly double[] _peaks = new double[BarCount];
    private readonly int[] _peakHold = new int[BarCount];
    private readonly Random _rng = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(8) };

    private WriteableBitmap? _bitmap;
    private byte[]? _pixelBuffer;
    private System.Windows.Controls.Image? _image;
    private bool _isPlaying;

    /// <summary>
    /// 再生中かどうかを示すフラグ。true の場合、ビジュアライザーは音楽に合わせて動きます。false の場合、バーは徐々に下がっていきます。
    /// </summary>
    public bool IsPlaying
    {
        get => _isPlaying;
        set => _isPlaying = value;
    }

    /// <summary>
    /// コンストラクタ
    /// </summary>
    public VisualizerControl()
    {
        Background = System.Windows.Media.Brushes.Transparent;

        for (int i = 0; i < BarCount; i++)
        {
            _heights[i] = _rng.NextDouble() * 0.5 + 0.2;
            _targets[i] = _rng.NextDouble() * 0.8 + 0.1;
            _peaks[i] = _heights[i];
        }

        // ImageコントロールをCanvasの子として追加
        _image = new System.Windows.Controls.Image
        {
            Stretch = Stretch.Fill,
            Opacity = VisualizerOpacity,
            IsHitTestVisible = false,
        };
        Children.Add(_image);

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>
    /// ロードイベントハンドラー。コントロールが画面に表示されたときに呼び出され、ビジュアライザーの更新を開始します。
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _timer.Tick += OnTick;
        _timer.Start();
    }

    /// <summary>
    /// アンロードイベントハンドラー。コントロールが画面から削除されたときに呼び出され、ビジュアライザーの更新を停止してリソースを解放します。
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
    }

    /// <summary>
    /// タイマーのTickイベントハンドラー。定期的に呼び出され、ビジュアライザーの状態を更新し、描画を行います。
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnTick(object? sender, EventArgs e)
    {
        var w = (int)ActualWidth;
        var h = (int)ActualHeight;

        System.Diagnostics.Debug.WriteLine($"Visualizer size: {w}x{h}, IsPlaying: {_isPlaying}");

        if (w <= 0 || h <= 0) return;

        // Imageのサイズを明示的に合わせる
        _image!.Width = w;
        _image!.Height = h;
        SetLeft(_image, 0);
        SetTop(_image, 0);

        if (_bitmap is null || _bitmap.PixelWidth != w || _bitmap.PixelHeight != h)
        {
            _bitmap = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
            _pixelBuffer = new byte[w * h * 4];
            _image!.Source = _bitmap;
        }

        UpdateBars();
        Render(w, h);
    }

    /// <summary>
    /// 各バーの高さ・ターゲット値・ピーク値を毎フレーム更新するメソッド。
    /// 再生中と停止中で挙動が異なる。
    /// </summary>
    private void UpdateBars()
    {
        for (int i = 0; i < BarCount; i++)
        {
            if (_isPlaying)
            {
                // TargetChangePct の確率でターゲット高さをランダムに更新する。
                // 確率を上げると頻繁に上下し、下げると緩やかな動きになる。
                if (_rng.NextDouble() < TargetChangePct)
                    _targets[i] = _rng.NextDouble() * 0.85 + 0.05;

                // 線形補間（Lerp）で現在の高さをターゲットに近づける。
                // LerpSpeed が大きいほど素早くターゲットに追従する。
                // 計算式: 現在値 += (目標値 - 現在値) × 速度
                _heights[i] += (_targets[i] - _heights[i]) * LerpSpeed;
            }
            else
            {
                // 停止中は全バーを StopFallSpeed ずつ下降させ、最小値0で止める。
                _heights[i] = Math.Max(0, _heights[i] - StopFallSpeed);
            }

            // ピークインジケーターの更新。
            // ピークとは各バーの最高到達点を示す短いセグメントのこと。
            _peakHold[i]++;

            if (_heights[i] >= _peaks[i])
            {
                // 現在の高さがピークを超えた場合はピークを更新し、保持カウントをリセット。
                _peaks[i] = _heights[i];
                _peakHold[i] = 0;
            }
            else if (_peakHold[i] > PeakHoldFrames)
            {
                // PeakHoldFrames フレーム以上ピークが更新されなかった場合、
                // PeakFallSpeed ずつ落下させる。最小値は0。
                _peaks[i] = Math.Max(0, _peaks[i] - PeakFallSpeed);
            }
        }
    }

    /// <summary>
    /// ピクセルバッファにビジュアライザーの全要素を描画し、
    /// WriteableBitmap に転送するメソッド。
    /// 描画する要素はメインバー・ピークセグメント・反射（下半分）の3種類。
    /// </summary>
    /// <param name="w">描画領域の幅（ピクセル）</param>
    /// <param name="h">描画領域の高さ（ピクセル）</param>
    private void Render(int w, int h)
    {
        // ピクセルバッファを全クリア（透明で初期化）。
        Array.Clear(_pixelBuffer!, 0, _pixelBuffer!.Length);

        // 描画領域の上半分をバー表示エリア、下半分を反射エリアとして使用する。
        // 0.52 にしているのは反射との境界線がちょうど中央より少し下になるよう調整するため。
        var halfH = h * 0.52;

        // バー1本あたりの横幅を算出。全バーが描画領域に均等に収まるよう分割する。
        var barW = (double)w / BarCount;

        // GapRatio 分だけ横幅を削ったバーの実際の描画幅。
        // バー間の隙間はこの差分で表現される。
        var barInner = barW * (1 - GapRatio);

        // セグメント1個あたりの高さ。バー表示エリア全体をセグメント数で均等分割。
        var segH = halfH / SegmentCount;

        // セグメント間の隙間の高さ。segH の 28% を隙間とする。
        var segGap = segH * 0.28;

        // セグメントの実際の描画高さ（隙間を除いた部分）。
        var segFill = segH - segGap;

        for (int i = 0; i < BarCount; i++)
        {
            // バーの左端X座標。barW の中央に barInner が来るよう余白を計算する。
            var x = (int)(i * barW + (barW - barInner) / 2);
            var bw = (int)barInner;

            // 現在の高さ（0.0〜1.0）をセグメント数に換算して描画するセグメント数を算出。
            var barSegs = (int)Math.Round(_heights[i] * SegmentCount);

            // ── メインバーの描画 ──────────────────────────────
            // s=0 が最下段、s=barSegs-1 が最上段となるよう下から積み上げる。
            // Y座標は halfH（基準線）から上方向に計算する。
            for (int s = 0; s < barSegs; s++)
            {
                var y = (int)(halfH - (s + 1) * segH + segGap / 2);
                FillRect(x, y, bw, (int)segFill, 255, w, h);
            }

            // ── ピークセグメントの描画 ────────────────────────
            // ピーク高さが有効な値（0.02以上）のときのみ描画する。
            // ピークの位置はメインバーと同じ計算式で Y 座標を算出する。
            if (_peaks[i] > 0.02)
            {
                var py = (int)(halfH - Math.Round(_peaks[i] * SegmentCount) * segH + segGap / 2);
                FillRect(x, py, bw, (int)segFill, 255, w, h);
            }

            // ── 反射（下半分）の描画 ──────────────────────────
            // メインバーを上下反転させた位置に、下に向かって徐々に透明になる形で描画する。
            // s=0 が反射の最上段（最も不透明）、s が増えるほど透明になる。
            // alpha = ReflectAlpha × (1 - 現在セグメント / 全セグメント数) で線形フェード。
            for (int s = 0; s < barSegs; s++)
            {
                var alpha = (byte)(255 * ReflectAlpha * (1.0 - (double)s / SegmentCount));
                var y = (int)(halfH + s * segH + segGap / 2);
                FillRect(x, y, bw, (int)segFill, alpha, w, h);
            }
        }

        // 描画済みのピクセルバッファを WriteableBitmap に一括転送する。
        // Lock/Unlock で囲むことで転送中の描画の競合を防ぐ。
        // AddDirtyRect で変更領域全体を通知し、WPF に再描画を促す。
        _bitmap!.Lock();
        try
        {
            _bitmap.WritePixels(
                new Int32Rect(0, 0, w, h),
                _pixelBuffer!,
                w * 4,  // ストライド（1行あたりのバイト数 = 幅 × 4byte/pixel）
                0);
            _bitmap.AddDirtyRect(new Int32Rect(0, 0, w, h));
        }
        finally
        {
            _bitmap.Unlock();
        }
    }

    /// <summary>
    /// ピクセルバッファの指定領域を単色で塗りつぶすメソッド。
    /// BGRA32 形式で白色（R=255, G=255, B=255）を alpha 値付きで書き込む。
    /// 描画領域外の座標は自動的にクリッピングする。
    /// </summary>
    /// <param name="x">塗りつぶし開始X座標</param>
    /// <param name="y">塗りつぶし開始Y座標</param>
    /// <param name="w">塗りつぶす幅（ピクセル）</param>
    /// <param name="h">塗りつぶす高さ（ピクセル）</param>
    /// <param name="alpha">アルファ値（0=透明, 255=不透明）</param>
    /// <param name="bmpW">ビットマップ全体の幅（クリッピング境界）</param>
    /// <param name="bmpH">ビットマップ全体の高さ（クリッピング境界）</param>
    private void FillRect(int x, int y, int w, int h, byte alpha, int bmpW, int bmpH)
    {
        // 描画範囲をビットマップ境界内にクリッピングする。
        var maxX = Math.Min(x + w, bmpW);
        var maxY = Math.Min(y + h, bmpH);
        var startX = Math.Max(x, 0);
        var startY = Math.Max(y, 0);

        for (int row = startY; row < maxY; row++)
        {
            for (int col = startX; col < maxX; col++)
            {
                // BGRA32 形式のピクセルオフセットを算出する。
                // 1ピクセルあたり4バイト（B, G, R, A）で構成される。
                // オフセット = (行 × 幅 + 列) × 4
                var idx = (row * bmpW + col) * 4;

                _pixelBuffer![idx + 0] = 255;   // B（青）
                _pixelBuffer![idx + 1] = 255;   // G（緑）
                _pixelBuffer![idx + 2] = 255;   // R（赤） → RGB全255で白色
                _pixelBuffer![idx + 3] = alpha; // A（透明度）
            }
        }
    }
}