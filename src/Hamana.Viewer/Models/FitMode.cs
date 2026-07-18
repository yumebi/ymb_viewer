namespace Hamana.Viewer.Models;

public enum FitMode
{
    // 画像全体が見えるように収める(余白が出る場合あり)
    Contain,
    // 常に横幅いっぱいに合わせる(縦ははみ出る場合あり)
    FitWidth,
    // 常に縦幅いっぱいに合わせる(横ははみ出る場合あり)
    FitHeight,
    // 画面全体を覆う(はみ出た分はクロップ、ドラッグでパン)
    Cover
}
