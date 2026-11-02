# TurnEndOpenUrlMod · 回合结束打开网页

用于《吉星派对》（Astral Party）的 CesiumLoader 模组。在对局中，当行动回合结束（例如完成掷骰、移动、出牌或事件，切换到其他玩家/怪物行动）或当前角色死亡时，自动通过系统默认浏览器打开指定网页（默认 `https://bilibili.com`），方便在等待他人回合或死亡休整时切换摸鱼；结合内置模组「回合置前」（TurnFocusMod），可在轮到自己时自动切回游戏。

## 安装

1. 下载模组发布包 `TurnEndOpenUrlMod.zip`。
2. 解压到游戏可执行文件同级目录下的 `AstralParty_ModLoader\mods\`，得到：

   ```text
   AstralParty_ModLoader\
     mods\
       TurnEndOpenUrlMod\
         TurnEndOpenUrlMod.dll
         TurnEndOpenUrlMod.json
         TurnEndOpenUrlMod.pdb   (可选)
   ```

3. 启动游戏。已有版本时覆盖更新即可，保留原 `config.json` 可保留个人偏好设置。

卸载时退出游戏，删除 `mods\TurnEndOpenUrlMod\` 目录即可。

## 功能

- **回合结束自动打开网页**：精准监测对局行动轮换，在玩家本轮操作完成、行动权交出时，自动唤起本机系统默认浏览器打开指定网址。
- **角色死亡自动打开网页**：实时监测血量变化，在角色生命值归零阵亡的瞬间自动唤起默认浏览器。
- **自定义目标网页**：支持在配置中自由指定任何以 `http://` 或 `https://` 开头的目标链接。
- **防重复触发保护**：内置防抖与冷却时间机制（默认 3 秒），避免网络抖动或连续伤害导致多开标签页。
- **排除非实时战斗**：回放与观战模式下自动静默，不抢占焦点或频繁唤起浏览器。

## 运行环境与配置

目标加载器：CesiumLoader 2.9.1+。目标框架：.NET Standard 2.0。

编辑模组目录下的 `config.json`，或在 AstralParty.Toys 的模组配置页进行可视化修改：

| 配置项 | 默认值 | 作用 |
| --- | --- | --- |
| Enabled | true | 总开关，是否启用模组功能 |
| TargetUrl | https://bilibili.com | 触发时在默认浏览器中打开的网页地址 |
| TriggerOnTurnEnd | false | 回合结束时是否打开网页（默认关闭） |
| OnlyMyTurn | true | 仅在自己的回合结束时打开网页（设为 false 则任何人的回合结束均触发） |
| TriggerOnDeath | true | 角色死亡时是否自动打开网页（默认开启） |
| OnlyMyDeath | true | 仅在自己的角色死亡时打开网页（默认开启，设为 false 则任何玩家角色死亡均触发） |
| CooldownSeconds | 3 | 触发冷却时间（秒），防止短时间内重复打开多个网页标签页 |

## 从源码构建

准备引用程序集：

```powershell
pwsh -NoProfile -File tools\fetch-refs.ps1 -Source C:\path\to\CesiumLoader
```

编译：

```powershell
dotnet build ./source/TurnEndOpenUrlMod.csproj -c Release
```
