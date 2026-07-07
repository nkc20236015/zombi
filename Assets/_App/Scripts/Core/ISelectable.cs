using System.Collections.Generic;

/// <summary>
/// プレイヤーがクリックして選択可能なオブジェクトが実装するインターフェース。
/// 画面右下に表示される詳細情報パネルへデータを提供します。
/// </summary>
public interface ISelectable
{
    /// <summary>オブジェクトの表示名</summary>
    string GetSelectionName();

    /// <summary>オブジェクトの説明</summary>
    string GetSelectionDescription();

    /// <summary>UIにグリッドやリスト形式で表示されるステータスのキーと値のペア</summary>
    Dictionary<string, string> GetSelectionStats();
}
