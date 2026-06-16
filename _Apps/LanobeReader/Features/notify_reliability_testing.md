# 更新通知の信頼性テスト手順（Doze / バックグラウンド）

`feature/novelviewer-notify-reliability` の動作確認用メモ。
対象: exact アラーム → 前面サービス（`UpdateCheckForegroundService`）→ 新着チェック → 通知の経路。

> ⚠️ エミュレータは「ロジック・権限・FGS 起動」の検証用。OEM 独自の省電力キル
> （Xiaomi / Samsung 等）は再現できないため、最終確認は実機（できれば対象 OEM 機）で行う。

---

## 0. 前提

- **API 34 / Google APIs** のエミュレータ or 実機（`targetSdk = 34`）。
- **Debug ビルド**で検証（短縮間隔とログが有効）。
- アプリ ID: `com.tbird.lanobereader`

> 💡 **Windows のフィルタ**: 本書は `grep -iE "a|b"` 表記だが、Windows cmd には `grep` が無い。
> - cmd: `... | findstr /I "a b c"`（スペース区切り＝OR）
> - PowerShell: `... | Select-String -Pattern "a|b|c"`
> - OS 非依存: `adb shell "logcat | grep -iE 'a|b'"`（grep をデバイス側で実行）

---

## 1. 間隔を短縮する（待ち時間の短縮）

[DebugSchedulingConfig.cs](../Platforms/Android/DebugSchedulingConfig.cs) の定数を一時的に変更してリビルド:

```csharp
public const int AlarmOverrideMinutes = 15;   // 0 = 無効。テスト後は必ず 0 に戻す
```

- アラーム（Doze 貫通経路）の発火が「時間」→「分」になる。
- **9 分未満を指定しても、Doze 中はシステムのレート制限で実発火は ~9〜10 分間隔になる**点に注意。
- 定期 WorkManager 側は最短 15 分（WorkManager の下限）。本経路のテストはアラーム側で行う。
- ⚠️ コミット前に必ず `0` へ戻す。

---

## 2. Doze を強制してテスト

```bash
# 充電を外す（Doze はバッテリ駆動が条件）
adb shell dumpsys battery unplug

# deviceidle を有効化し、深い Doze へ一気に遷移
adb shell dumpsys deviceidle enable
adb shell dumpsys deviceidle force-idle        # → "Stepped to deep: IDLE"

# 状態確認
adb shell dumpsys deviceidle get deep          # IDLE になっていること

# （任意）ネットを擬似的に絞って制約耐性を見る
adb shell svc data disable
adb shell svc wifi disable
```

### アラーム発火を誘発（間隔を待たずに進める）

```bash
adb shell dumpsys deviceidle step              # メンテ窓へ進めて保留アラームを発火
# 必要に応じて複数回 step
```

### 後始末（必ず実行）

```bash
adb shell svc data enable
adb shell svc wifi enable
adb shell dumpsys deviceidle unforce
adb shell dumpsys battery reset
```

---

## 3. FGS＋チェック処理を発火させる

> ❌ `adb shell am start-foreground-service -n .../UpdateCheckForegroundService` は
> **使えない**。サービスは `Exported=false`（本番的に正しい）のため、adb シェル（uid 2000）
> からの起動は `Permission Denial` で拒否される。FGS は必ず「アプリ自身が」アラーム経由で
> 起動する必要がある。

正しい手順（実アラームを発火させ、アプリにFGSを起動させる）:

```bash
# 1) DebugSchedulingConfig.AlarmOverrideMinutes = 2 などにしてリビルド・インストール
# 2) アプリを一度起動してアラームを武装（ログに "Update alarm (exact) scheduled in 2... " 相当）
# 3) すぐ画面OFF/背面化し、指定分だけ待つ
#    → 時刻が来ると UpdateAlarmReceiver → UpdateCheckForegroundService（アプリ起動なので許可）
```

Doze 下で試す場合は、武装後すぐ `force-idle` して指定分待つ（exact アラームは Doze 中も時刻通り発火）:

```bash
adb shell dumpsys battery unplug
adb shell dumpsys deviceidle force-idle
# 指定分待つ → アラーム発火 → FGS 起動
```

→ 「更新を確認中…」の常駐通知が出て、ログに `CheckAll` → 新着通知が流れれば成功。

---

## 4. ログ確認

```bash
adb logcat -c    # クリア
adb logcat | grep -iE "Update alarm|foreground|CheckAll|FGS|notif"
```

### ✅ 成功の目安

| ログ | 意味 |
|------|------|
| `Update alarm (exact) scheduled in ...` | exact で武装できている（不正確に落ちていない） |
| `[DEBUG] Alarm override: firing in 15 min` | デバッグ短縮が効いている |
| 「更新を確認中」通知 → 数秒後に消える | FGS が起動し処理完遂 |
| `ShowUpdateNotification` 相当の新着通知 | 通知経路 OK |

### ❌ 異常の目安

| ログ | 意味・対処 |
|------|-----------|
| `Update alarm (inexact) scheduled ...` | exact 権限を取得できていない。`USE_EXACT_ALARM` 付与状況を確認（下記） |
| `startForeground denied; fallback to WorkManager` | 背面からの FGS 起動が拒否された。exact アラーム経由でないと起きやすい |
| `IPlatformApplication.Current is null, retry later` | プロセス未初期化。FGS は ~3 秒待つが、それでも null なら起動直後すぎる |
| `Failed to resolve services` | DI 解決失敗（登録漏れ） |

### 権限・状態の確認

```bash
# exact アラーム可否（true なら exact 経路が使える）
adb shell dumpsys alarm | grep -i com.tbird.lanobereader

# 電池最適化の除外状態
adb shell dumpsys deviceidle whitelist | grep -i lanobereader

# 登録済みサービス/レシーバの確認
adb shell dumpsys package com.tbird.lanobereader | grep -iE "Service|Receiver"
```

---

## 5. 再起動後の再武装（BootReceiver）

```bash
adb reboot
# 起動後
adb logcat | grep -iE "Boot re-arm|Update alarm"
```

→ `Update alarm (exact) scheduled ...` が出れば、再起動後の再武装 OK。

---

## 6. 実機での最終確認（推奨）

- `AlarmOverrideMinutes = 15` のまま実機に入れ、**画面 OFF＋放置で 1〜2 サイクル**通知が来るか。
- 対象 OEM 機（Xiaomi/Samsung 等）では、初回起動時の電池最適化除外ダイアログで「許可」した場合としない場合の差を確認。
- 確認できたら `AlarmOverrideMinutes = 0` に戻してコミット。
