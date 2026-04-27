# Zombi RTS Colony - 開発状況とAI引き継ぎメモ

このファイルは、複数のPC（自宅・学校）間で開発を進める際、AIアシスタントに現在の開発状況やこれまでの文脈を即座に理解させるための共有メモリです。
**※ 新しいPCで作業を再開する際、AIに「このファイルを読んで前回の続きから始めて」と指示してください。**

---

## 📅 現在の開発フェーズ
**現在: Phase 2 （資源システムと採取機能）に進む直前**

## ✅ 完了済みの実装 (Phase 1まで & UI改修)
*   **NPC指揮システムの基盤:** RTSスタイルでNPCを動かす基盤が動作中。
    *   **操作体系:** 右クリックでNPC移動指示、中ボタンドラッグでカメラ回転、WASDでパン（Going Medievalスタイル）。
    *   **選択UI:** NPC選択時は足元に緑色のサークル（LineRenderer）を表示。
*   **昼夜サイクルシステム (`TimeManager.cs`):** 
    *   朝(3分)、昼(5分)、夕方(2分)、夜(8分)のサイクル。
    *   RenderSettings.skyboxとDirectional Lightの滑らかな切り替え。
*   **HUD UI (`HUDManager.cs` & Layout):**
    *   Going Medieval風の四隅に配置するモジュラーレイアウトに刷新（TopRightPanelに時間、その他はプレースホルダ）。
    *   テキスト変更時にDOTweenでアニメーション。

## 🚀 次のステップ (Phase 2)
1.  **資源の概念の追加:** 木材、石、食料などのリソースシステムを作成（`ResourceManager`）。
2.  **UIのアップデート:** 画面右端に用意したプレースホルダ領域(`RightPanel`)に各資源の所持量を表示する。
3.  **NPCの採取アクション:** NPCに木を伐採させるなどの指示（タスク）を出せるようにする。

## 📝 開発上の注意点
*   **Gitのルートフォルダ:** 実際のプロジェクトルートは `C:\Users\root\Documents\zombi` です（`zombi/zombi` ではありません）。
*   **PlayerControllerについて:** 以前のアクションゲーム用の `PlayerController.cs` は非推奨となり、RTS用に `NPCController.cs` への移行を進めています（関連するコンパイルエラーは修正済み）。
*   **UIについて:** HUDのアニメーションには `DG.Tweening` (DOTween)、テキストには `TextMeshPro` を使用しています。

---
*Last Updated: Phase 1完了時*
