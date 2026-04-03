using MusicWidget.Models;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace MusicWidget.ViewModels
{
    /// <summary>
    /// メイン画面の表示内容を管理するビューモデルクラスです。
    /// </summary>
    /// <remarks>
    /// このクラスはメディアプレーヤーアプリケーションにおいて、メインUIのビューモデルを表し データバインディング用にトラック情報と再生状態を提供します。
    /// UIの更新のためにプロパティ変更通知をサポートするため、INotifyPropertyChanged インターフェースを実装しています。
    /// すべてのプロパティのセッターは private です。更新は Apply メソッドを介して行う必要があり、このメソッドは UI スレッドから呼び出す必要があります。
    /// このクラスは、MVVM ベースの WPF アプリケーションにおけるバインディングソースとして使用することを目的としています。
    /// </remarks>
    public class MainViewModel : INotifyPropertyChanged
    {
        private string _title = "—";
        private string _artist = "—";
        private string _album = "—";
        private BitmapImage? _artwork;
        private bool _isPlaying;

        /// <summary>
        /// 楽曲のタイトルを取得します。
        /// </summary>
        public string Title { get => _title; private set => Set(ref _title, value); }
        /// <summary>
        /// 楽曲のアーティスト名を取得します。
        /// </summary>
        public string Artist { get => _artist; private set => Set(ref _artist, value); }
        /// <summary>
        /// 楽曲のアルバム名を取得します。
        /// </summary>
        public string Album { get => _album; private set => Set(ref _album, value); }
        /// <summary>
        /// 楽曲のアートワークを取得します。アートワークが存在しない場合は null になります。
        /// </summary>
        public BitmapImage? Artwork { get => _artwork; private set => Set(ref _artwork, value); }
        /// <summary>
        /// 楽曲の再生状態を示すフラグを取得します。true の場合、楽曲は現在再生中であることを示します。
        /// </summary>
        public bool IsPlaying { get => _isPlaying; private set => Set(ref _isPlaying, value); }

        /// <summary>
        /// 現在のトラックの表示を、指定されたトラックからの情報で更新します。
        /// </summary>
        /// <remarks>このメソッドは必ずUI スレッドから呼び出す必要があります。
        /// 渡されたトラック情報に含まれる値がnullまたは空のフィールドである場合、プレースホルダーに置き換えられます。</remarks>
        /// <param name="info">表示するトラック情報を格納するオブジェクト。nullにすることはできません。</param>
        public void Apply(TrackInfo info)
        {
            Title = string.IsNullOrEmpty(info.Title) ? "—" : info.Title;
            Artist = string.IsNullOrEmpty(info.Artist) ? "—" : info.Artist;
            Album = string.IsNullOrEmpty(info.Album) ? "—" : info.Album;
            IsPlaying = info.IsPlaying;
            Artwork = ToImage(info.Artwork);
        }

        private static BitmapImage? ToImage(byte[]? data)
        {
            if (data is null or { Length: 0 }) return null;
            var img = new BitmapImage();
            img.BeginInit();
            img.StreamSource = new MemoryStream(data);
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.EndInit();
            img.Freeze();
            return img;
        }

        /// <summary>
        /// プロパティが変更されたときに発生するイベントです。
        /// </summary>
        /// <remarks>
        /// INotifyPropertyChanged インターフェースを実装し、このイベントを発生させることで
        /// プロパティの値が変更されたことをクライアント（通常はバインディングされているクライアント）に通知します。
        /// </remarks>
        public event PropertyChangedEventHandler? PropertyChanged;

        private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}