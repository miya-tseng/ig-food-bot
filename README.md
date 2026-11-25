# IG Food Bot

把 Instagram 美食貼文分享給 bot 帳號 `yummy.pin`，bot 會回覆一張店家卡片（評分、營業狀態、台幣價位、開啟地圖按鈕）和一則摘要。

## 運作方式

```
IG 分享貼文給 yummy.pin
  → Meta 依 webhook 訂閱，把訊息 POST 到 Cloud Run 的 /webhook（WebhookController）
  → 驗證 X-Hub-Signature-256
  → Gemini 讀貼文文字（Reels 會附上完整貼文內容）與圖片，產生店名、地區、國家、搜尋字串、摘要、推薦餐點
  → Google Places 依序用多組搜尋字串找店，只接受餐廳類結果
  → ExchangeRate-API 把價位換算成台幣
  → 透過 Instagram Messaging API 回覆店家卡片與摘要
```

Cloud Run 平常縮到 0 台，有 webhook 進來才啟動，第一次可能多 1～3 秒冷啟動時間。

## 回覆格式

第一則：店家卡片（Generic Template）

```
🍗 PARU（파루 닭한마리）
⭐ 4.8（105） · 🟢 營業中，21:30 打烊
💰 NT$490–730（₩20,000–30,000）
[ 📍 開啟地圖 ]
```

第二則：摘要

```
📝 景福宮巷弄內的韓方一隻雞名店⋯
👍 一隻雞火鍋、鐵板雞肉、綠豆煎餅
```

- 標題 emoji 依 Google 地點的主要類型決定
- 找不到店時，卡片改為「🤔 店名」與「🔍 在地圖搜尋」按鈕
- IG 不接受卡片格式時，自動改送純文字加連結

## 使用的服務

| 服務 | 位置 | 用途 | 費用 |
|---|---|---|---|
| Meta App（Instagram API, Unified login） | 協助者的 Meta 開發者帳號 | 收發 `yummy.pin` 私訊 | 免費 |
| Cloud Run、Secret Manager、Artifact Registry | GCP 專案 `ig-food-bot`（有帳單） | 執行程式、存放金鑰 | 免費額度內 |
| Places API (New) | GCP 專案 `ig-food-bot` | 找店、評分、營業時間、價位 | 免費額度內 |
| Gemini API | GCP 專案 `personal-gemini-free`（**不綁帳單**） | 讀貼文、產生摘要 | 免費方案 |
| ExchangeRate-API | open.er-api.com | 匯率，快取 12 小時 | 免費 |

> Gemini key 必須放在**沒有帳單**的專案。專案一旦連結帳單，Gemini 會變成預付制，未儲值會回 `402 prepayment credits are depleted`。

帳單帳戶已設定每月 NT$100 的預算警示（`monthly-alert`，10% / 50% / 90% / 100% 寄信，不扣除抵免額）。

## 監控與警示

| 警示 | 觸發條件 | 用途 |
|---|---|---|
| `IG Food Bot - Instagram API unavailable` | Uptime check 每 15 分鐘呼叫 `/health/instagram`，失敗即通知 | Meta 封鎖 App 或權杖失效時，webhook 不會再進來，只能靠主動檢查發現 |
| `IG Food Bot - Errors in logs` | Cloud Run 出現 `fail:` / `crit:` 開頭的 IgFoodBot 記錄，每小時最多通知一次 | Instagram、Gemini、Places 呼叫失敗 |

通知會寄到 Monitoring 的 Email 通知管道。`/health/instagram` 需要設定 `Instagram:AccountId`，結果快取 5 分鐘。

## 1. 準備金鑰

### Gemini
1. 建立一個**不綁帳單**的 GCP 專案（例如 `personal-gemini-free`）
2. 在該專案建立 API key，限制只能呼叫 Generative Language API

### Google Places API (New)
1. 在 Cloud Run 所在的專案啟用 **Places API (New)**
2. 建立 API key，限制只能呼叫 Places API (New)

### Instagram（Unified login）
1. IG bot 帳號切換成**專業帳號**，並開啟 設定 → 訊息和限時動態回覆 → 訊息控制 → 連線工具 →「允許存取訊息」
2. 由 Facebook 帳號狀態正常的人在 [Meta for Developers](https://developers.facebook.com/) 建立 App，使用案例選「管理 Instagram 上的訊息和內容」，並建立商家資產管理組合
3. 「權限和功能」加入 `instagram_business_basic`、`instagram_business_manage_messages`
4. 「應用程式角色」把 IG bot 帳號加為 **Instagram 測試人員**，並在 IG 網頁版 `accounts/manage_access` 接受邀請
5. 「API setup with Unified login」：
   - 第 1 步從範本建立登入設定
   - 第 2 步產生權杖，取得不會過期的系統用戶權杖（`EAA` 開頭）作為 `AccessToken`
6. 「應用程式設定 → 基本資料」取得**應用程式密鑰**作為 `AppSecret`
7. 自行產生一組 **Verify Token**，例如 `openssl rand -hex 16`

`EAA` 開頭的權杖搭配 `https://graph.facebook.com`，訊息從 `/{IG 帳號 ID}/messages` 發送。若改用 `IG` 開頭的 Instagram 使用者權杖，將 `Instagram:GraphApiBaseUrl` 改為 `https://graph.instagram.com`。

## 2. 本機開發

```bash
cd src/IgFoodBot.Api
dotnet user-secrets set "Instagram:VerifyToken" "<verify-token>"
dotnet user-secrets set "Instagram:AppSecret" "<app-secret>"
dotnet user-secrets set "Instagram:AccessToken" "<access-token>"
dotnet user-secrets set "Gemini:ApiKey" "<gemini-key>"
dotnet user-secrets set "GooglePlaces:ApiKey" "<places-key>"
ASPNETCORE_ENVIRONMENT=Development dotnet run
```

`dotnet user-secrets` 只在 `Development` 環境載入。

執行單元測試：

```bash
dotnet test
```

## 3. 部署到 Google Cloud Run

首次設定：

```bash
gcloud auth login
gcloud config set project <PROJECT_ID>
gcloud services enable run.googleapis.com cloudbuild.googleapis.com artifactregistry.googleapis.com secretmanager.googleapis.com places.googleapis.com

printf '%s' '<verify-token>' | gcloud secrets create ig-verify-token --data-file=-
printf '%s' '<app-secret>'   | gcloud secrets create ig-app-secret --data-file=-
printf '%s' '<access-token>' | gcloud secrets create ig-access-token --data-file=-
printf '%s' '<gemini-key>'   | gcloud secrets create gemini-api-key --data-file=-
printf '%s' '<places-key>'   | gcloud secrets create places-api-key --data-file=-

PROJECT_NUMBER=$(gcloud projects describe <PROJECT_ID> --format='value(projectNumber)')
gcloud projects add-iam-policy-binding <PROJECT_ID> \
  --member="serviceAccount:${PROJECT_NUMBER}-compute@developer.gserviceaccount.com" \
  --role="roles/secretmanager.secretAccessor"

gcloud run deploy ig-food-bot \
  --source . \
  --region asia-east1 \
  --allow-unauthenticated \
  --timeout 60 \
  --max-instances 2 \
  --set-secrets "Instagram__VerifyToken=ig-verify-token:latest,Instagram__AppSecret=ig-app-secret:latest,Instagram__AccessToken=ig-access-token:latest,Gemini__ApiKey=gemini-api-key:latest,GooglePlaces__ApiKey=places-api-key:latest"
```

之後更新程式只要：

```bash
gcloud run deploy ig-food-bot --source . --region asia-east1
```

更換金鑰：新增 secret 版本後，部署新的 revision 讓它讀到 `latest`：

```bash
printf '%s' '<new-key>' | gcloud secrets versions add gemini-api-key --data-file=-
gcloud run services update ig-food-bot --region asia-east1 --update-secrets "Gemini__ApiKey=gemini-api-key:latest"
```

### 設定 Meta Webhook

在 Meta App 的「API setup with Unified login → 設定 Webhooks」：
- 回呼網址：`https://<cloud-run-url>/webhook`
- 驗證權杖：`<verify-token>`
- 訂閱欄位：`messages`

並讓 IG 帳號訂閱 App：

```bash
curl -X POST "https://graph.facebook.com/v27.0/<IG_ACCOUNT_ID>/subscribed_apps" \
  -H "Authorization: Bearer <access-token>" \
  --data-urlencode "subscribed_fields=messages"
```

## 誰可以使用

Meta App 在**開發模式**時，只有在「應用程式角色」中有角色的帳號（例如 Instagram 測試人員）傳的訊息會送到 webhook。

- 新增使用者：「應用程式角色 → 角色 → 新增用戶 → Instagram 測試人員」，對方到 `https://www.instagram.com/accounts/manage_access/` 的「測試人員邀請」接受
- 開放給所有人：需要隱私權政策網址、通過 `instagram_business_manage_messages` 的 App Review，並將 App 切換為上線模式

## 設定項目

| 設定 | 說明 | 預設 |
|---|---|---|
| `Instagram:VerifyToken` | Webhook 驗證用 | |
| `Instagram:AppSecret` | 驗證 `X-Hub-Signature-256` | |
| `Instagram:AccessToken` | 發送訊息用的權杖 | |
| `Instagram:GraphApiBaseUrl` | Graph API 網址 | `https://graph.facebook.com` |
| `Instagram:GraphApiVersion` | Graph API 版本 | `v27.0` |
| `Instagram:AllowedSenderIds` | 允許使用 bot 的 IG 使用者 ID，空陣列代表不限制 | `[]` |
| `Instagram:AccountId` | bot 的 IG 帳號 ID，`/health/instagram` 用來檢查 API 是否可用 | |
| `Gemini:ApiKey` | Gemini API key | |
| `Gemini:Model` | 模型名稱 | `gemini-3.5-flash-lite` |
| `Gemini:FallbackModels` | 主模型重試後仍失敗時依序改用的模型 | `["gemini-3.1-flash-lite"]` |
| `Gemini:ThinkingLevel` | 思考程度，`minimal` 最快；留空則使用模型預設 | `minimal` |
| `GooglePlaces:ApiKey` | Places API key | |
| `GooglePlaces:LanguageCode` | 地點名稱語言 | `zh-TW` |
| `GooglePlaces:RegionCode` | Gemini 沒判斷出國家時使用的預設地區 | `TW` |

## 查看紀錄

Cloud Run → `ig-food-bot` →「記錄」，或：

```bash
gcloud logging read 'resource.labels.service_name="ig-food-bot" AND textPayload:"Resolved"' \
  --freshness=1d --format='value(timestamp,textPayload)'
```

| 關鍵字 | 內容 |
|---|---|
| `Handling message` | 收到的訊息與附件類型 |
| `Resolved` | 貼文店名、搜尋字串、找到的店 |
| `Gemini ... returned 503, retrying` | Gemini 忙碌，自動重試 |
| `Gemini request failed with <model>` | 該模型失敗，改用 `FallbackModels` 中的下一個 |

Log 預設保留 30 天。需要看完整 webhook 內容與回覆文字時，暫時開啟 Debug：

```bash
gcloud run services update ig-food-bot --region asia-east1 --update-env-vars "Logging__LogLevel__IgFoodBot=Debug"
gcloud run services update ig-food-bot --region asia-east1 --remove-env-vars Logging__LogLevel__IgFoodBot
```

## 常見問題

| 現象 | 原因 |
|---|---|
| 啟動失敗並列出缺少的欄位 | 必要金鑰沒設定 |
| 回「這篇我認不出是哪一家店」 | Gemini 失敗（看 log 的 `Gemini request failed`）或貼文沒有店名 |
| Gemini `402` | Gemini key 所在的專案連結了帳單 |
| Gemini `503` | 免費方案忙碌，已自動重試 2 次，仍失敗則改用備用模型 |
| 找到地址而不是店家 | 搜尋字串只有地址；目前會跳過非餐廳類結果 |
| 別人傳訊息 bot 沒反應 | App 在開發模式，對方不是測試人員 |
| 所有人都沒反應、`/health/instagram` 回 503 | Meta 封鎖 App（`API access blocked`）或權杖失效，到 Meta App 主控板查看 |
