using System.IO;
using Windows.Media.Control;
using MusicWidget.Models;

namespace MusicWidget.Services
{
    /// <summary>
    /// Apple Musicの現在再生中のトラック情報を取得し、変更を通知するサービスクラスです。
    /// </summary>
    /// <remarks>
    /// Global System Media Transport Controls (GSMTC) API を使用して、Apple Music セッションを監視および操作する機能を提供します。
    /// トラック情報が変更された際にイベントを発生させ、セッションのライフサイクルを初期化および管理するためのメソッドを提供します。
    ///
    /// このサービスは、Windows上のApple Musicで現在再生中のトラックに関する更新情報を受け取る必要があるアプリケーションでの使用を目的としています。
    /// 利用可能な場合、自動的にApple Musicセッションに接続し、トラック情報や再生状態が変更された際にサブスクライバーに通知します。
    /// 使用前にInitializeAsyncを呼び出してサービスを初期化する必要があります。
    /// 不要になった際にセッションのイベントハンドラやリソースを解放するために、IDisposableを実装しています。
    /// </remarks>
    public class MediaSessionService : IDisposable
    {
        private const string AmpModelId = "AppleInc.AppleMusicWin";

        private GlobalSystemMediaTransportControlsSessionManager? _manager;
        private GlobalSystemMediaTransportControlsSession? _ampSession;

        /// <summary>
        /// 曲情報が変化したときに通知するイベント
        /// </summary>
        public event Action<TrackInfo>? TrackChanged;

        /// <summary>
        /// 非同期的にサービスを初期化し、Apple Musicセッションへの接続を確立します。
        /// </summary>
        /// <returns>
        /// 戻り値はありませんが、Apple Musicセッションへの接続が確立され、トラック情報の変更を受け取る準備が整うまで完了しません。
        /// </returns>
        public async Task InitializeAsync()
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _manager.SessionsChanged += OnSessionsChanged;
            AttachAmpSession();
        }

        private void OnSessionsChanged(
            GlobalSystemMediaTransportControlsSessionManager sender,
            SessionsChangedEventArgs args)
        {
            AttachAmpSession();
        }

        private void AttachAmpSession()
        {
            // 既存セッションのイベントを解除
            if (_ampSession is not null)
            {
                _ampSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                _ampSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
                _ampSession = null;
            }

            // Apple Musicセッションを探す
            _ampSession = _manager?
                .GetSessions()
                .FirstOrDefault(s => s.SourceAppUserModelId.Contains(AmpModelId));

            if (_ampSession is not null)
            {
                _ampSession.MediaPropertiesChanged += OnMediaPropertiesChanged;
                _ampSession.PlaybackInfoChanged += OnPlaybackInfoChanged;
                // 初回即時取得
                _ = FetchAndNotifyAsync();
            }
            else
            {
                // Apple Musicが起動していない
                TrackChanged?.Invoke(TrackInfo.Empty);
            }
        }

        private void OnMediaPropertiesChanged(
            GlobalSystemMediaTransportControlsSession sender,
            MediaPropertiesChangedEventArgs args)
            => _ = FetchAndNotifyAsync();

        private void OnPlaybackInfoChanged(
            GlobalSystemMediaTransportControlsSession sender,
            PlaybackInfoChangedEventArgs args)
            => _ = FetchAndNotifyAsync();

        private async Task FetchAndNotifyAsync()
        {
            if (_ampSession is null) return;

            var props = await _ampSession.TryGetMediaPropertiesAsync();
            if (props is null || (string.IsNullOrEmpty(props.Title) &&
                                  string.IsNullOrEmpty(props.AlbumArtist)))
            {
                TrackChanged?.Invoke(TrackInfo.Empty);
                return;
            }

            // AlbumArtistフィールドに "アーティスト — アルバム名" が入っている
            var parts = props.AlbumArtist.Split('—');
            var artist = parts.First().Trim();
            var album = parts.Last().Trim();

            // アートワークをbyte[]に変換
            byte[]? artwork = null;
            if (props.Thumbnail is not null)
            {
                try
                {
                    using var streamRef = await props.Thumbnail.OpenReadAsync();
                    using var ms = new MemoryStream();
                    using var stream = streamRef.AsStreamForRead();
                    await stream.CopyToAsync(ms);
                    artwork = ms.ToArray();
                }
                catch { /* アートワーク取得失敗は無視 */ }
            }

            // 再生状態
            var playback = _ampSession.GetPlaybackInfo();
            var isPlaying = playback.PlaybackStatus ==
                            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

            TrackChanged?.Invoke(new TrackInfo
            {
                Title = props.Title,
                Artist = artist,
                Album = album,
                Artwork = artwork,
                IsPlaying = isPlaying,
            });
        }

        /// <summary>
        /// 全リソースを解放し、イベントハンドラを解除します。サービスが不要になった際に呼び出してください。
        /// </summary>
        public void Dispose()
        {
            if (_ampSession is not null)
            {
                _ampSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                _ampSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            }
        }
    }
}
