# Zombi RTS Colony - 開発状況とAI引き継ぎメモ

このファイルは、複数のPC（自宅・学校）間で開発を進める際、AIアシスタントに現在の開発状況やこれまでの文脈を即座に理解させるための共有メモリです。
**※ 新しいPCで作業を再開する際、AIに「このファイルを読んで前回の続きから始めて」と指示してください。**

---

## 📅 現在の開発フェーズ
**現在: Phase 2 （資源システムと採取機能）→ UI・カメラ・操作性の改善中**

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

## 🔧 Phase 2 実装済み
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
    *   テキストのみ表示（数字のみ）。アイコンはCainos Pixel Art Icon Pack からUI Imageで配置（ユーザーが手動設定）
    *   アニメーション: 資源変化時にテキストが黄色くふわっと光るDOColor演出
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
    *   UIアイコン: `Cainos/Pixel Art Icon Pack - RPG/Texture/`
*   **NPCツールホルダー (`NPCToolHolder.cs`):**
    *   右手ボーン（Hand.R）にツールをインスタンス化して持たせる機能
    *   Animator: Idle → TakeItem → Chop/Mine(ループ) → PutItem → Idle のフロー

## 🔧 5/1 実装済み（カメラ・操作性改善）
*   **カメラ追従 (`TopDownCamera.cs`):**
    *   Fキーで選択中のNPCを追従開始（複数選択時は中心点を追従）
    *   中ホイール回転は追従を維持したまま使用可能
    *   WASD / スクロール / Q/E / Escで追従解除
*   **採取中断の改善 (`NPCController.cs` + `NPCState.cs`):**
    *   採取中に移動指示を出すと、ツールをしまう（PuttingAwayステート、2秒間）→ しまい終わったら移動開始
    *   しまう途中に新しい指示が来た場合は目的地を上書き
*   **カメラ遮蔽物の半透明化 (`CameraOccluder.cs`):** ← NEW（要テスト）
    *   メインカメラにアタッチして使用
    *   選択中NPCとカメラの間にある障害物（木など）を自動的に半透明化
    *   カメラ自体が木に近づいた場合も周囲の木を半透明化（OverlapSphere方式、BoxCollider不要）
    *   URP Litシェーダーの_Surface / _Blend / RenderQueueを動的に切り替え
    *   カメラが離れると滑らかに元の不透明に戻る

## 🚀 残りのタスク
1.  **カメラ遮蔽物の半透明化テスト:** CameraOccluderをカメラにアタッチしてテスト
2.  **採取UIパネル:** 採取対象を左クリック選択 → 「ここで採取する / キャンセル」のUIパネル表示
3.  **NPC一覧UI:** 画面左にNPC一覧パネル（参考写真待ち）
4.  **Phase 3:** 溜まった資源を用いた建築・消費システムの構築

## 🐛 既知のバグ・課題
*   （ツールの手からのズレは修正済み）
*   CameraOccluder は未テスト。木のColliderが正しく設定されていないと透過しない可能性あり。

## 📝 開発上の注意点
*   **Gitのルートフォルダ:** 実際のプロジェクトルートは `C:\Users\root\Documents\zombi` です。
*   **アニメーションイベント:** アセットのアニメーションに付与されている足音等のイベント（`FootR`, `FootL`）は、警告を防ぐために各Controller内に空メソッドとして定義済みです。
*   **レイヤー設定:** Resource = Layer 9
*   **木のマテリアル:** URP Lit シェーダー (Surface=Opaque, Blend=0)。CameraOccluder で半透明化対応済み。

---
*Last Updated: 2026-05-01 (カメラ追従・採取中断改善・遮蔽物半透明化)*
