# 银行劫案 (Bank Heist)

> Godot 4.6.3 (.NET 8, C#) · ENet 多人 · 劫匪 vs 警察非对称对抗  
> 文档版本 0.5 — 本次更新：可驾驶载具（E 上车 / WASD 驾驶 / 第三人称跟车相机 / 网络同步）、画面美化（SSAO/Glow/雾/ACES/双光源）、HUD 重排（顶栏单行、战术卡移左下、新准星、车速显示）、警车与轿车程序化模型重做。
>
> 0.4：近距交互系统（免瞄准 + HUD 提示 + 可用性过滤）、平衡性调参表、Kenney CC0 模型接入、结算面板、程序化音效。

**默认端口**: UDP `24565` · **最低人数**: 1（Solo）

---

## 1. 一句话玩法

劫匪混入银行前广场人群 → 潜入银行触发锁门（10 分钟谈判倒计时）→ 处理人质 / 伪装 / 抢金库 → 从侧门或下水道撤离；警察在外线喝退可疑者、喇叭谈判，倒计时 30% 后可从前/侧/后门突入并堵撤离路线。

---

## 2. 场景流与界面

```
MainMenu.tscn（主菜单）
├── 主页：单人开始 / 多人联机 / 设置 / 退出
├── 联机页：玩家名字、地址、端口 → Host / Join
└── 设置页：语言、鼠标灵敏度、主音量、语音音量、语音模式、全屏
        ↓ AppState.PendingIntent
Main.tscn（对局）
├── 阵营选择面板（劫匪 / 警察）
├── HUD：顶栏（阶段/倒计时/阵营/语音状态）+ 战术卡 + 提示条 + Toast
└── ESC 战术菜单：继续 / 设置 / 重选阵营 / 返回主菜单 / 退出
```

- 设置即时生效并持久化到 `user://settings.cfg`。
- 断线 / 连接失败自动返回主菜单并显示原因。
- 命令行直入对局：`--host`、`--join=IP`、`--port=N`（冒烟测试用）。

## 3. 语言（i18n）

- `Scripts/Core/Loc.cs`：中 / 英全量字典（菜单、HUD、状态广播、地图标注、NPC、名牌）。
- 切换语言触发 `Loc.LanguageChanged`，主菜单 / HUD / 3D 标签全部即时重建。
- 网络状态广播只传 **key + 参数**，各端按本地语言渲染（`ApplyBankStatusRpc(key, arg)`）。

## 4. 语音交流

| 项 | 实现 |
|----|------|
| 采集 | `Record` 总线 + `AudioEffectCapture`（`project.godot` 已开 `audio/driver/enable_input`） |
| 编码 | `IVoiceCodec` 接口 + PCM16 fallback；下采样 16 kHz 单声道，30 ms/帧（960 B/帧，低于 ENet MTU） |
| 传输 | 客户端 → 服务端 `ServerVoiceRelay`（不可靠 RPC）→ 服务端广播 `ClientVoiceRpc` |
| 回放 | 远端玩家节点挂 `AudioStreamPlayer3D` + `AudioStreamGenerator` → **按 3D 距离衰减的近距离语音**（45m 上限） |
| 模式 | 设置页三档：关麦 / 按 V 说话 / 自由开麦（带 RMS 噪声门限） |
| 指示 | 头顶绿点 + HUD 顶栏「正在说话」 |
| 抑制 | 发送端高通 / 噪声门 / 远端回放衰减，降低啸叫与底噪 |

局限：尚未接原生 Opus/WebRTC AEC；公网大房间建议用 GDExtension 或第三方 VoIP 替换 `IVoiceCodec`。

## 5. 地图（真实尺度街区）

`Scripts/World/CityMapBuilder.cs` 运行时生成，约 **120 m × 74 m**，坐标原点在银行营业厅中心（+Z 朝南临街）。

| 区域 | 真实参照尺寸 |
|------|--------------|
| 银行主楼 | 36 × 26 m，墙高 5 m，墙厚 0.4 m |
| 营业大厅 | 进深 12 m；柜台长 20 m、高 1.1 m + 防弹玻璃 |
| 金库 | 内室约 7 × 9 m，围墙厚 0.8 m，重型库门 + 转轮 + 金条堆 |
| 后区 | 走廊宽 3 m；办公室、安防机房（机柜阵列）、人质等候区（沙发组） |
| 门洞 | 大门 4.2 m（锁门时卷帘落下）、侧门/后门 1.6 m |
| 南侧街道 | 双车道 8 m + 中线虚线 + 斑马线 + 路缘石 |
| 前广场 | 进深 16 m：人群、喷泉、花坛、警戒护栏（留缺口） |
| 停车场 | 西侧 32 × 14 m，2.6 m 车位线，7 辆轿车（4.5 × 1.8 m） |
| 小巷 | 东侧巷 8 m 宽（侧门撤离线）；后巷 6 m（下水道井盖撤离点） |
| 街景 | 路灯（带光源）、警车 ×2 + 警用面包车、消防栓、垃圾箱、四周临街楼立面封边 |

所有传送 / 判定坐标集中在 `Scripts/World/MapLocations.cs`，与几何同源，改图改一处。

### 开源模型（v0.4，全部 CC0）

| 用途 | 资产 | 来源 |
|------|------|------|
| 停车场车辆 | vehicle-truck-{red,green,yellow,purple} | Kenney Starter Kit Racing (GitHub) |
| 临街建筑（街区四周） | building-small-a/b/c/d、building-garage | Kenney Starter Kit City Builder (GitHub) |
| 喷泉 | pavement-fountain | 同上 |
| 树木 | grass-trees / grass-trees-tall | 同上 |
| 手枪（第一人称） | phase-sidearm | Kenney Blaster Kit |
| 门框装饰 | door-sliding-double | Kenney Prototype Kit |

接入策略：模型加载失败自动退回程序化几何（`AssetLibrary.AddModel` 返回 null 时走 fallback），碰撞一律用独立隐形碰撞体而非模型网格。缩放常量集中在 `CityMapBuilder` 顶部（`TruckScale` 等），视觉不匹配时只调一处。银行主体仍为程序化（需要精确门洞与房间逻辑）；玩家保留程序化人形（换装配色 + 走路摆动 + 说话指示是玩法功能，静态 glb 无法承载）。

## 6. 角色模型

`Scripts/World/PlayerModelBuilder.cs` 拼装 1.8 m 人形（头/躯干/四肢分件）：

- 四肢挂枢轴，按移动速度摆动（行走动画）；
- 配色方案：警察制服 / 劫匪三套伪装（随换装轮换）/ 伪人质黄 / 被捕灰；
- 头顶说话指示灯 + 双行名牌（名字 + 阵营/HP/持枪状态）；
- NPC（广场人群、人质）共用同一人形；人群使用轻量行为树式状态机（排队 / 漫游 / 封锁撤离 / 避让玩家），人质 idle、释放后逃离、死亡倒地。

玩家碰撞胶囊 1.8 m / 半径 0.3，视点高 1.62 m；步行 4.2 m/s，Shift 冲刺 6.4 m/s。

## 7. 操作与交互

| 动作 | 键位 |
|------|------|
| 移动 / 冲刺 / 跳 | WASD / Shift / Space |
| 射击 | 鼠标左键（仅持枪时） |
| 交互目标点 | E |
| 阵营情境动作 | F 或 Q |
| 说话（PTT 模式） | V |
| 战术菜单 | Esc |
| 上车 / 下车 | E（靠近可驾驶载具） |
| 驾驶 | W/S 油门刹车 · A/D 转向 |

情境动作：劫匪锁门前=换装，锁门后=伪装人质→录音；警察锁门前=喝退，锁门后=喇叭谈判。

### 载具（v0.5）

- 可驾驶车辆 3 台：`Drive_Patrol`（警戒线旁警车）、`Drive_SedanA/B`（停车场轿车）。
- 街机手感：最高 ≈47 km/h，倒车 18 km/h，转向随速度衰减，碰撞自动减速。
- 第三人称跟车相机；驾驶中显示「[E] 下车 · 车速」。
- 网络：占用权服务端裁决（一车一人），位姿驾驶端上报→服务端转播（不可靠通道），中途加入者同步在车玩家。
- 实现：`Scripts/World/DriveableCar.cs`（运动学模拟 + IInteractable）+ Game 载具 RPC 组。

### 交互系统（v0.4）

- **近距触发**：走进交互点半径内按 E 即可，无需准星对准地面圆盘。
- **HUD 提示**：屏幕中下方实时显示「[E] 当前可执行动作」。
- **可用性同源**：`BankActionRules` 同时驱动 HUD 提示与服务端裁决——对你不可用的交互点（错误阵营 / 错误阶段）光柱与文字自动变暗，且按下也不会误触发。
- **光柱标识**：每个交互点带竖直光柱，远处可辨。

## 7.5 平衡性（v0.4，集中在 `Scripts/Core/Balance.cs`）

| 项 | 数值 | 理由 |
|----|------|------|
| 武器 | 20 伤害 × 4 发/s（DPS 80，TTK 1.25s） | 原 132 DPS 互秒太快 |
| 谈判加分 | +3 / 次，**每队 10s 冷却** | 原可无限刷分 |
| 警察喝退 | **每人 8s 冷却** | 原可贴脸无限传送劫匪 |
| 击倒加分 | 警察击倒持枪劫匪 +30；劫匪击倒警察 +15 | 击杀原本无收益 |
| 逮捕 | +80 分；**仅携带金库目标的劫匪被捕才直接终结回合** | 多劫匪局一人失误不再立即全队失败 |
| 劫匪死亡 | 掉落金库目标 + 掉枪（需回金库点重新拾取） | 死亡原本无惩罚 |

## 8. 网络架构

- Listen Server（Host 即服务端），ENet UDP。
- 服务端权威：阵营、银行行为、伤害/逮捕、名字；状态经 `SyncHeistStateRpc` 全量同步（含倒计时锚点 Unix 时间）。
- 主门开闭额外经 `SyncDoorStateRpc` 直发到新加入者，立即落地门位置，并在 0.25s 后再补发一次，避免加入瞬间门状态错位。
- 玩家 Transform：本端上报 → 服务端转播（不可靠）。
- 语音：不可靠中继，不进可靠通道。
- 所有 RPC 入口校验 `sender == peerId or server`（见 `MultiplayerGuard`）。

## 9. 目录结构

```
Scripts/
├── Core/        Loc（双语）、AppState（autoload 设置/意图）、GameLayers
├── World/       CityMapBuilder、MapLocations、PlayerModelBuilder
├── UI/          MainMenu、HeistHud、SettingsPanelBuilder、Aui/（主题组件库）
├── Voice/       VoiceChatManager
├── Heist/       HeistGameMode（回合状态机）、HeistService、BankDoor/BankNpc/BankObjective、枚举
├── Networking/  MultiplayerGuard
├── Game.cs      对局入口：RPC 汇聚、阵营/名字/语音中继、银行行为裁决
├── PlayerController.cs / SampleWeapon.cs
Scenes/          MainMenu.tscn（主入口）、Main.tscn（对局）、Player.tscn
Assets/Kenney/   CC0 模型（当前主要由程序化几何替代，可选用）
```

旧「相位」原型代码已全部移除。

## 10. 运行 / 构建 / 冒烟

```powershell
.\OpenGodotSample.ps1     # 打开编辑器（自动定位 Tools 下的 Godot 4.6.3 mono）
.\BuildGodotSample.ps1    # dotnet build
```

无头冒烟（已验证通过：建房/加入/离开、无 RPC 错误）：

```powershell
godot --headless --path . --quit-after 1500 -- --host
godot --headless --path . --quit-after 700  -- --join=127.0.0.1
```

注意：不要用 Unity Hub 打开本目录。

## 11. 已知限制 / 留给 Codex

v0.4 新增已完成：

- [x] 近距交互系统：免瞄准、HUD「[E] 动作」提示、`BankActionRules` 提示与裁决同源、不可用交互点自动变暗
- [x] 平衡性调参表 `Balance.cs`：武器 TTK、谈判/喝退冷却、击倒与逮捕计分、劫匪死亡掉枪掉目标
- [x] Kenney CC0 模型接入：卡车×5、临街建筑环（四周）、喷泉、树木、手枪、门框（均带程序化 fallback）
- [x] 门状态补发 `SyncDoorStateRpc`（中途加入者落地主门开闭）
- [x] 音效（枪声/警笛/锁门/人质事件）与程序化粒子反馈
- [x] 结算面板（终局弹出，显示双方评分与人质统计）

v0.5 新增已完成：

- [x] 可驾驶载具系统（占用裁决 / 位姿同步 / 跟车相机 / 车速 HUD）
- [x] 画面：SSAO、Glow、距离雾、ACES tonemap、暖主光 + 冷补光、柔化阴影
- [x] HUD：顶栏单行化（修复中文逐字竖排）、战术卡移至左下、细线准星、Toast 自动换行
- [x] 程序化警车/轿车重做（分段车身、车灯、轮毂、警灯条），第一人称手枪缩小贴角

参考《Airport Security Sucks!》技术栈结论（仅看程序集构成，未复制任何内容）：其语音 = Concentus(Opus) + RNNoise 降噪——印证语音待办方向；联机 = Mirror + KCP；UI 动效 = DOTween + 全屏模糊。

仍待办：

- [~] 语音已有 `IVoiceCodec` 与发送端基础抑制；建议接 Concentus(Opus) + RNNoise（参考游戏同方案，均有 .NET 实现）
- [~] 突入检测仍为半径判定，无视野锥；主门已改为卷帘物理开合动画
- [ ] `FeedbackFx` 为程序化音调，可替换为真实采样（脚步/警笛循环）
- [ ] 车辆碰撞伤害：当前车辆碰撞只做物理阻挡/减速，撞到玩家或 NPC 不扣血、不击倒
- [ ] 引擎音效：当前可驾驶车辆无怠速、加速、刹车、碰撞音效
- [ ] Kenney 模型缩放常量（`CityMapBuilder` 顶部 `TruckScale`/`FacadeBuildingScale` 等）按编辑器实际观感微调；手枪朝向不对则调 `SampleWeapon._Ready` 的 rotation
- [ ] 无缝重开一局（当前结算后需回主菜单重建房间）
- [ ] UI 组件层（Aui）借鉴 Apricity UI 思路，未拷贝其源码

## 12. 许可

代码与文档 MIT；Kenney 资产 CC0（见 `Assets/Kenney/ATTRIBUTION.md`）。
