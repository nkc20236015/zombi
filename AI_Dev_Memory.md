# Zombi RTS Colony - 開発状況とAI引き継ぎメモ

このファイルは、複数のPC（自宅・学校）間で開発を進める際、AIアシスタントに現在の開発状況やこれまでの文脈を即座に理解させるための共有メモリです。
**※ 新しいPCで作業を再開する際、AIに「このファイルを読んで前回の続きから始めて」と指示してください。**

---

## 📅 現在の開発フェーズ
**現在: Phase 2 （資源システムと採取機能）実装中 → 動作確認待ち**

## ✅ 完了済みの実装 (Phase 1まで & UI/ビジュアル改修)
*   **NPC指揮システムの基盤:** RTSスタイルでNPCを動かす基盤が動作中。
    *   **操作体系:** 右クリックでNPC移動指示、中ボタンドラッグでカメラ回転、WASDでパン。
    *   **選択UIとマーカー:** NPC選択時は足元に緑色のサークル（LineRenderer）＋カスタムシェーダーによるアウトラインを表示。右クリック移動時には指定座標にマーカープレハブを表示。
*   **NPCアニメーション (`NPCAnimationController.cs`):**
    *   `ExplosiveLLC` (Crafting Mecanim Animation Pack) のIdle / Walk / Chop / Mine アニメーションを適用。
    *   NavMeshAgentの速度に合わせた自動再生。
    *   `PlayAction(int actionType)` メソッドで採取アニメーション呼び出し。0=Chop(伐採), 1=Mine(採掘)
*   **昼夜サイクルシステム (`TimeManager.cs`):** 
    *   朝(3分)、昼(5分)、夕方(2分)、夜(8分)のサイクルと空・光源の切り替え。
*   **HUD UI (`HUDManager.cs` & Layout):**
    *   Going Medieval風の四隅に配置するモジュラーレイアウトに刷新。

## 🔧 Phase 2 実装済み（動作確認待ち）
*   **資源管理システム:**
    *   `ResourceType.cs` - Wood, Stone, Food の列挙型
    *   `ResourceManager.cs` - 資源の増減・イベント通知を管理するシングルトン（GameManagerオブジェクトにアタッチ済み）
    *   `ResourceNode.cs` - 木や岩にアタッチする採取対象スクリプト。残量管理、枯渇時の縮小・非アクティブ化
*   **NPC採取機能:**
    *   `NPCController.cs` に `GatherResource()` メソッド追加。MovingToResource → Gathering のステートマシン
    *   `CommandManager.cs` に `resourceLayer` レイキャスト追加。右クリックでResourceNode検知時は採取指示
    *   Animator Controllerに Chop(Chop-Vertical) と Mine(Dig-Scoop) ステートを追加
*   **資源UI:**
    *   `HUDManager.cs` にWood/Stone/Foodの表示機能追加。TopLeftPanelに配置
*   **シーン配置:**
    *   Spruce_008（木）× 5本、Rock × 3個をResourceNodesコンテナの下に配置
    *   Resourceレイヤー(9)設定済み、CapsuleCollider付き
*   **使用アセット:**
    *   木: `Happy Little Trees/Prefabs/Trees/Spruce/Spruce_008.prefab`
    *   岩: `Happy Little Trees/Prefabs/Rocks/Rock.prefab`, `Rock_002.prefab`, `Rock_004.prefab`
    *   伐採アニメ: `Crafter@Chop-Vertical.FBX`
    *   採掘アニメ: `Crafter@Dig-Scoop.FBX`
    *   取り出しアニメ: `Crafter@Item-Take.FBX`、しまうアニメ: `Crafter@Item-Putdown.FBX`
    *   道具モデル: `Veresen/BasicTools/Prefabs/axe.prefab`(斧), `pickaxe.prefab`(ピッケル)
*   **NPCツールホルダー (`NPCToolHolder.cs`):**
    *   右手ボーン（Hand.R）にツールをインスタンス化して持たせる機能
    *   Animator: Idle → TakeItem → Chop/Mine(ループ) → PutItem → Idle のフロー

## 🚀 次のステップ
1.  **バグ修正:** 斧（ツール）がNPCの手から離れて表示される問題の修正（`NPCToolHolder`の `holdPositionOffset` / `holdRotationOffset` の再調整）
2.  **動作確認:** プレイモードでNPC選択→木/岩を右クリック→取り出し→採取→しまうの一連を検証
3.  **資源UIの改善:** アイコンやフォントの調整

## 🐛 既知のバグ・課題 (次回修正)
*   採取ツール（斧・ピッケル）が手から浮いている/離れている。スクショ確認後、オフセットを修正する。

## 📝 開発上の注意点
*   **Gitのルートフォルダ:** 実際のプロジェクトルートは `C:\Users\root\Documents\zombi` です。
*   **アニメーションイベント:** アセットのアニメーションに付与されている足音等のイベント（`FootR`, `FootL`）は、警告を防ぐために各Controller内に空メソッドとして定義済みです。
*   **レイヤー設定:** Resource = Layer 9

---
*Last Updated: 2026-04-30 (Phase 2 資源システム実装完了)*
