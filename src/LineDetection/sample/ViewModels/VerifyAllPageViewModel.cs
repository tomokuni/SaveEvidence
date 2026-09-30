using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace EsUtil.SaveEvidence.LineDetection.Sample.ViewModels;

/// <summary>「データ全検証」ページの ViewModel。 dataset の全画像を一括検証し、正解データ（GroundTruth）と照合する。</summary>
/// <remarks>
/// 「データ検証」ページ（単一検証）とは分離し、全画像の検証結果を <see cref="VerifyResults"/> に反映する。<br/>
/// ページ表示時に自動で全画像を検証する（ボタンは設けない）。<br/>
/// 検証ロジックは共有の <see cref="DatasetVerifier"/> に委譲する。
/// </remarks>
public sealed partial class VerifyAllPageViewModel : ObservableObject
{
    /// <summary>検証結果の一覧（画像名 + 一致/不一致 + 4 辺の詳細）。</summary>
    public ObservableCollection<VerifyResultItem> VerifyResults { get; } = [];

    /// <summary>一致した画像の件数。</summary>
    public int MatchedCount => VerifyResults.Count(r => r.IsCorrect);

    /// <summary>検証対象の画像総数。</summary>
    public int TotalCount => VerifyResults.Count;

    /// <summary>検証結果のサマリ文字列（例: "8/8 一致"）。</summary>
    public string SummaryText => $"{MatchedCount}/{TotalCount} 一致";

    /// <summary>dataset の画像一覧（検証対象）。</summary>
    public IReadOnlyList<DatasetEntry> DatasetItems { get; private set; } = [];

    /// <summary>全検証で使用する検出器。</summary>
    private readonly LineDetectorOption _detector;

    /// <summary>ステータスバーの表示テキスト。</summary>
    [ObservableProperty]
    private string _statusText = "";

    /// <summary>VerifyAllPageViewModel を初期化する。</summary>
    public VerifyAllPageViewModel()
    {
        _detector = new LineDetectorOption(new LogicLineDetector());
        DatasetItems = DatasetVerifier.LoadDatasetItems();
    }

    /// <summary>全画像を検証して結果を反映する。</summary>
    /// <remarks>ページ表示時や再読み込み時に呼び出す。</remarks>
    public void VerifyAll()
    {
        VerifyResults.Clear();

        if (DatasetItems.Count == 0)
        {
            StatusText = "dataset 検証対象がありません";
            NotifySummaryChanged();
            return;
        }

        foreach (var entry in DatasetItems)
        {
            VerifyResults.Add(DatasetVerifier.VerifyImage(entry, _detector.Detector));
        }

        StatusText = $"dataset 検証: {MatchedCount}/{TotalCount} 一致（許容誤差 0px）";
        NotifySummaryChanged();
    }

    /// <summary>サマリ関連プロパティの変更を一括通知する。</summary>
    private void NotifySummaryChanged()
    {
        OnPropertyChanged(nameof(MatchedCount));
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(SummaryText));
    }

    /// <summary>検証ページの dataset 一覧を再読み込みして全検証する。</summary>
    /// <remarks>データ作成ページで保存された後に呼び出し、一覧と結果を更新する。</remarks>
    public void ReloadDatasetItems()
    {
        DatasetItems = DatasetVerifier.LoadDatasetItems();
        VerifyAll();
    }
}
