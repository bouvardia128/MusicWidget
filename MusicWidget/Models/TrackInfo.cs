using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MusicWidget.Models
{
    /// <summary>
    /// 音楽トラックのメタデータおよび再生状態情報を表します。
    /// </summary>
    /// <remarks>
    /// 現在再生中のトラックに関する詳細（タイトル、アーティスト、アルバム、アートワーク、再生状態など）を伝えるために使用されます。
    /// ここにあるすべてのプロパティは不変であり、初期化時に設定されます。
    /// </remarks>
    public class TrackInfo
    {
        /// <summary>
        /// 楽曲のタイトル
        /// </summary>
        public string Title { get; init; } = string.Empty;
        /// <summary>
        /// 楽曲のアーティスト
        /// </summary>
        public string Artist { get; init; } = string.Empty;
        /// <summary>
        /// 楽曲のアルバム名
        /// </summary>
        public string Album { get; init; } = string.Empty;
        /// <summary>
        /// 楽曲のアートワーク（アルバムカバーなど）を表すバイト配列
        /// </summary>
        /// <remarks>
        /// アートワークは、JPEGやPNGファイルなどの生画像データを含むバイト配列として表現されます。
        /// アートワークが存在しない場合、この値はnullになることがあります。
        /// </remarks>
        public byte[]? Artwork { get; init; }
        /// <summary>
        /// 楽曲が現在再生中かどうかを示すフラグ
        /// </summary>
        public bool IsPlaying { get; init; }

        /// <summary>
        /// TrackInfoクラスの空のインスタンスを取得します。
        /// </summary>
        /// <remarks>
        /// このプロパティを使用して、TrackInfoのデフォルト値または初期化されていない値を表します。
        /// </remarks>
        public static TrackInfo Empty => new();
    }
}
