# Adaptive Challenge–Skill Framework — Setup

โค้ดชุดนี้ทำให้เกมตรงตาม Proposal (Flow-Based Adaptive Challenge–Skill Balancing)
สรุปไฟล์และขั้นตอนการตั้งค่าใน Unity Editor

## ไฟล์หลัก
| ไฟล์ | หน้าที่ (อ้างอิงหัวข้อ Proposal) |
|---|---|
| `ChallengeFramework.cs` | สูตร/ค่าคงที่ทั้งหมด: Normalize, Challenge Score, Skill Score, CSBI, Adaptive Algorithm, Mapping, Reference Ranges (§7.3, §8) |
| `DDAManager.cs` | ตัวควบคุมกลาง: กำหนด Challenge ต่อห้อง, ประมาณ Skill, ปรับห้องถัดไป (§8–§9) |
| `GameplayTelemetry.cs` | เก็บ telemetry ระดับห้อง + event และ export CSV (§10, §13.5) |
| `PlayerConfig.cs` | ค่าตัวละครผู้เล่นคงที่ (§7.2) |
| `RoomController.cs` | บอกว่าห้องนี้คือ R1/R2/R3/Boss (§7.1) |
| `EnemyStats.cs` | ใส่ HP/ATK/Speed ให้ศัตรูจาก Challenge ของห้อง (§8.6) |
| `EnemySpawner.cs` | เกิดศัตรูตามอัตรา enemies/sec (§8.6, §10) |
| `Health / PlayerAttack / PlayerController` | HP คงที่ + เก็บ Accuracy/DamageTaken/Death (§7.2, §10) |
| `RoomExit / WarpDoor` | จบห้อง → คำนวณห้องถัดไป → โหลดซีน (§9.3) |

## ค่าที่ระบบปรับ (จาก Challenge C ระดับ 0–1) — §8.6
- Enemy Speed = 1.0 + C·(3.0−1.0)  → C=0.50 ได้ 2.00 units/sec
- Enemy Attack = 10 + C·(40−10)     → C=0.50 ได้ 25 dmg/hit
- Spawn Rate = 0.20 + C·(0.80−0.20) → C=0.50 ได้ 0.50 /sec
- Enemy HP = 50 + C·(200−50)        → C=0.50 ได้ 125 HP

## ขั้นตอนตั้งค่าในแต่ละซีน
1. **หมายเลขห้อง = อ่านจากชื่อซีนอัตโนมัติ** (Room1/Room2/Room3 → Normal, ชื่อที่มี
   "Boss" → Boss) — **ไม่ต้องเพิ่ม `RoomController`** ถ้าชื่อซีนตรงรูปแบบนี้
   - จะใช้ `RoomController` ก็ต่อเมื่อชื่อซีนไม่ตรง หรือมีศัตรูวางไว้ในซีนล่วงหน้า (ไม่ได้ spawn)
2. **Prefab ศัตรูทุกตัว** ต้องมี `EnemyStats` (พร้อม `Health`, และถ้ามีการเดิน/โจมตี ก็ `EnemyMovement`/`EnemyAttack`) และตั้ง Tag = `Enemy`
3. **ผู้เล่น** ตั้ง Tag = `Player` และมี `Health`, `PlayerController`, `PlayerAttack`
4. **ประตูออก** (`RoomExit` หรือ `WarpDoor`) ตั้ง `nextSceneName`:
   Room1→Room2, Room2→Room3, Room3→BossRoom, BossRoom→EndScene
5. `DDAManager` มีอยู่ในทุกซีนห้องแล้ว (singleton) — `GameplayTelemetry` จะถูกสร้างอัตโนมัติ

## ตั้งค่าการทดลอง (Adaptive / Non-Adaptive) — §9.1
เลือกวิธีใดวิธีหนึ่ง:
- เพิ่ม GameObject ใส่ `GameplayTelemetry` ในซีนแรก แล้วตั้ง `participantId`,
  `condition` (Adaptive/NonAdaptive), `sequence` ผ่าน Inspector ก่อนเริ่มเล่น
- หรือปล่อยให้สร้างอัตโนมัติ (default = Adaptive, P00)

การกำหนด Challenge ต่อห้อง:
- Room1 = 0.50 เสมอ (ทั้งสองเงื่อนไข)
- Non-Adaptive: Room2 = 0.60, Room3 = 0.70 (คงที่)
- Adaptive: Room2 คำนวณจาก S₁, Room3 จาก S₂ (§8.5)
- Boss = คงที่ (`bossFixedChallenge`, default 0.70)

## ผลลัพธ์ (Dataset)
เมื่อจบ Boss ระบบ export CSV อัตโนมัติไปที่
`%USERPROFILE%\AppData\LocalLow\<Company>\<Product>\Telemetry\`
- `rooms_*.csv` — หนึ่งแถวต่อห้อง: C, S, CSBI, BalanceError, Accuracy, ClearTime,
  DamageTaken, DeathCount, Enemy params ฯลฯ (คอลัมน์ตาม §13.5)
- `events_*.csv` — event-level (Attack, Hit, EnemySpawn, EnemyKilled, Damage, Death,
  RoomStart, RoomClear) สำหรับวิเคราะห์ย้อนหลัง

คอลัมน์ `Flow` เว้นว่างไว้ให้เติมจากแบบสอบถาม FSS (§11) ภายหลัง

## อัปโหลดขึ้น Firebase (Realtime Database)
ใช้ `FirebaseUploader.cs` — ส่งผ่าน REST API **ไม่ต้อง import Firebase SDK**
- databaseUrl ตั้งค่าไว้แล้วในโค้ด:
  `https://projectrougelite-default-rtdb.asia-southeast1.firebasedatabase.app`
- `FirebaseUploader` ถูกสร้างอัตโนมัติ (ไม่ต้องเพิ่ม component เอง) — จะเพิ่มเองเพื่อ
  แก้ URL/ปิดการอัปโหลด/ใส่ authToken ก็ได้
- **เหลืออย่างเดียว: ตั้ง Rules** ในเว็บ Firebase (แท็บ Realtime Database → Rules):
  ```json
  { "rules": { "telemetry": { ".read": false, ".write": true } } }
  ```
  ระบบจริงควรใช้ `authToken` + `".write": "auth != null"` แทน
- เมื่อจบ Boss ระบบจะอัปโหลดอัตโนมัติไปที่
  `telemetry/{participantId}/{runId}` (พร้อม rooms[] และ events[])

> ถ้าอยากใช้ **Firestore** หรือ **Firebase SDK อย่างเป็นทางการ** แทน REST บอกได้
> (ต้อง import SDK + google-services.json เพิ่ม)

## ส่งเข้า Google Sheets (1 แถว/ห้อง)
ใช้ `GoogleSheetUploader.cs` + Google Apps Script Web App (ไม่ต้องใช้ API key/OAuth)
1. เปิด Google Sheet ใหม่ → Extensions → Apps Script
2. วางโค้ดจากไฟล์ **`GoogleSheet_AppsScript.gs.txt`** → Save
3. Deploy → New deployment → **Web app**
   - Execute as: **Me**
   - Who has access: **Anyone**  ← ต้องเลือกอันนี้
   - Authorize access
4. คัดลอก **Web app URL** (ลงท้าย `/exec`)
5. Unity: ใส่ URL ในช่อง `webAppUrl` ของ `GoogleSheetUploader`
   (หรือแก้ค่า default ใน `GoogleSheetUploader.cs`)

ระบบจะส่ง **1 แถวต่อ 1 ห้องตอนจบห้อง** (ข้อมูลครบ: C, S, CSBI, Accuracy,
ClearTime, DamageTaken ฯลฯ) เข้าชีตชื่อ "Telemetry" พร้อมหัวตารางอัตโนมัติ

> Firebase กับ Google Sheet ส่งพร้อมกันได้ (ปิดอันที่ไม่ใช้ด้วย `uploadEnabled`)
> ค่าเริ่มต้น `GoogleSheetUploader.uploadEnabled = false` (ใช้วิธีดึงจาก Firebase แทน — ด้านล่าง)

## ดึงจาก Firebase → Google Sheet (รันครั้งเดียว ดึงทั้งหมด) ⭐
ใช้ไฟล์ **`FirebaseToSheet_AppsScript.gs.txt`** (ไม่ต้องแก้เกม/ไม่ต้อง deploy web app)
1. เปิด Google Sheet ใหม่ → Extensions → Apps Script
2. วางโค้ดจากไฟล์นั้น → Save
3. กด Run เลือก `importFromFirebase` ครั้งแรก → Authorize access
4. กลับหน้า Sheet จะมีเมนู **"Firebase" → "นำเข้าข้อมูลตอนนี้"** กดได้ทุกเมื่อ
5. สคริปต์จะดึง `telemetry` ทั้งหมดจาก Firebase มากางเป็นตาราง (1 แถว/ห้อง ของทุกผู้เล่น)
   ล้างของเก่าแล้วเขียนใหม่ทุกครั้งที่กด (Firebase = แหล่งข้อมูลจริง)

## หมายเหตุ (ตาม §8.1, §8.2, §13.4)
- น้ำหนัก Challenge (0.20/0.30/0.20/0.30) และ Skill (0.25×4), reference ranges ของ
  ClearTime/Damage/Death และ balance band เป็น *initial design parameters*
  ปรับได้ผ่าน Inspector (`DDAManager.skillRefs`) และ `ChallengeFramework`
  เพื่อทำ Pilot / Sensitivity Analysis
