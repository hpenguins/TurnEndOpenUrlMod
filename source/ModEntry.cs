using System;
using CesiumLoader.SDK.Configuration;
using CesiumLoader.SDK.Gameplay;
using CesiumLoader.SDK.Logging;
using CesiumLoader.SDK.Mods;
using CesiumLoader.SDK.Runtime;
using Core.Net;
using GameLogic;
using party.protocol;
using Tools;
using UnityEngine;

namespace TurnEndOpenUrlMod
{
    public static class ModEntry
    {
        public static void Main()
        {
            ModBase.Run(new TurnEndOpenUrl());
        }
    }

    public sealed class TurnEndOpenUrl : ModBase
    {
        private const int MaxHookInstallsPerBattle = 4;
        private const long HookCheckIntervalTicks = TimeSpan.TicksPerSecond;

        private RoundStartS2CRPC _roundStartRpc;
        private RoundStartS2CRPC.OnRoundStartS2CServerDelegate _roundStartOriginal;
        private RoundStartS2CRPC.OnRoundStartS2CServerDelegate _roundStartWrapped;

        private ActionStartNotifyS2CRPC _actionStartRpc;
        private ActionStartNotifyS2CRPC.OnActionStartNotifyS2CServerDelegate _actionStartOriginal;
        private ActionStartNotifyS2CRPC.OnActionStartNotifyS2CServerDelegate _actionStartWrapped;

        private BattleLogic _battle;
        private int _installs;

        private bool _enabled = true;
        private string _targetUrl = "https://bilibili.com";
        private bool _triggerOnTurnEnd = false;
        private bool _onlyMyTurn = true;
        private bool _triggerOnDeath = true;
        private bool _onlyMyDeath = true;
        private int _cooldownSeconds = TurnEndPolicy.DefaultCooldownSeconds;

        private long _lastActivePlayerId = 0;
        private bool _isMyDead = false;
        private long _lastTriggerTicks = 0;
        private long _nextHookCheckTicks = 0;

        public override string Name => "回合结束打开网页";
        public override string Version => "1.0.0";
        public override string Author => "hpenguins";
        public override string Description => "当回合结束或当前角色死亡时，自动通过系统默认浏览器打开指定网页（默认 https://bilibili.com）。";

        public override void OnInitialize()
        {
            ApplyConfigDocs();
            LoadConfig();

            // 注册 SDK 属性变更事件（用于监测角色死亡）并启动自动挂钩
            GameEvents.StartAutoHook();
            GameEvents.HeroAttrUpdated += OnHeroAttrUpdated;

            Log.Info($"[TurnEndOpenUrl] 初始化完成: Enabled={_enabled}, TargetUrl={_targetUrl}, TriggerOnTurnEnd={_triggerOnTurnEnd}, OnlyMyTurn={_onlyMyTurn}, TriggerOnDeath={_triggerOnDeath}, OnlyMyDeath={_onlyMyDeath}, Cooldown={_cooldownSeconds}s");
        }

        public override void OnConfigChanged()
        {
            LoadConfig();
            Log.Info($"[TurnEndOpenUrl] 配置已热更新: Enabled={_enabled}, TargetUrl={_targetUrl}, TriggerOnTurnEnd={_triggerOnTurnEnd}, OnlyMyTurn={_onlyMyTurn}, TriggerOnDeath={_triggerOnDeath}, OnlyMyDeath={_onlyMyDeath}, Cooldown={_cooldownSeconds}s");
        }

        private void ApplyConfigDocs()
        {
            try
            {
                var cfg = Config;
                if (cfg == null) return;
                if (TurnEndOpenUrlConfigDocs.Apply(cfg))
                {
                    cfg.Save();
                }
            }
            catch (Exception ex)
            {
                Log.Warn("应用配置多语言文档失败：" + ex.Message);
            }
        }

        private void LoadConfig()
        {
            var cfg = Config;
            if (cfg == null) return;
            _enabled = cfg.GetBool("Enabled", _enabled);
            _targetUrl = cfg.GetString("TargetUrl", _targetUrl);
            _triggerOnTurnEnd = cfg.GetBool("TriggerOnTurnEnd", _triggerOnTurnEnd);
            _onlyMyTurn = cfg.GetBool("OnlyMyTurn", _onlyMyTurn);
            _triggerOnDeath = cfg.GetBool("TriggerOnDeath", _triggerOnDeath);
            _onlyMyDeath = cfg.GetBool("OnlyMyDeath", _onlyMyDeath);
            _cooldownSeconds = TurnEndPolicy.ClampCooldownSeconds(cfg.GetInt("CooldownSeconds", _cooldownSeconds));
        }

        public override void OnUpdate()
        {
            long now = DateTime.UtcNow.Ticks;
            if (now >= _nextHookCheckTicks)
            {
                _nextHookCheckTicks = now + HookCheckIntervalTicks;
                EnsureHooks();
            }
        }

        private void EnsureHooks()
        {
            try
            {
                var gm = SimpleSingletonProvider<GameLogicManager>.inst;
                if (!IsLiveBattle(gm)) return;

                // 每一局重置挂接计数与状态
                if (!ReferenceEquals(_battle, gm.battle))
                {
                    _battle = gm.battle;
                    _installs = 0;
                    _roundStartRpc = null;
                    _roundStartOriginal = null;
                    _roundStartWrapped = null;
                    _actionStartRpc = null;
                    _actionStartOriginal = null;
                    _actionStartWrapped = null;
                    _lastActivePlayerId = 0;
                    _isMyDead = false;
                }

                if (_installs >= MaxHookInstallsPerBattle) return;

                var net = MonoSingletonProvider<NetManager>.inst?.RPC;
                if (net == null) return;

                HookRoundStart(net);
                HookActionStart(net);
            }
            catch (Exception e)
            {
                Log.Warn("检查/安装 RPC 挂钩异常：" + e.Message);
            }
        }

        private void HookRoundStart(Core.Net.RPCMsgManager net)
        {
            var rpc = net.RoundStartS2C;
            if (rpc == null) return;

            var current = rpc.OnRoundStartS2CServerCallBackAsync;
            if (current == null || ReferenceEquals(current, _roundStartWrapped)) return;

            var original = current;
            RoundStartS2CRPC.OnRoundStartS2CServerDelegate wrapped =
                (model, errId, isDispatch) =>
                {
                    try
                    {
                        if (errId == 0 && model != null)
                        {
                            HandlePlayerTurnShift(model.PlayerId, "RoundStart");
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Warn("处理 RoundStart 回调异常: " + e.Message);
                    }
                    return original(model, errId, isDispatch);
                };

            _installs++;
            _roundStartRpc = rpc;
            _roundStartOriginal = original;
            _roundStartWrapped = wrapped;
            rpc.OnRoundStartS2CServerCallBackAsync = wrapped;
        }

        private void HookActionStart(Core.Net.RPCMsgManager net)
        {
            var rpc = net.ActionStartNotifyS2C;
            if (rpc == null) return;

            var current = rpc.OnActionStartNotifyS2CServerCallBackAsync;
            if (current == null || ReferenceEquals(current, _actionStartWrapped)) return;

            var original = current;
            ActionStartNotifyS2CRPC.OnActionStartNotifyS2CServerDelegate wrapped =
                (model, errId, isDispatch) =>
                {
                    try
                    {
                        if (errId == 0 && model != null)
                        {
                            HandlePlayerTurnShift(model.PlayerId, "ActionStart");
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Warn("处理 ActionStart 回调异常: " + e.Message);
                    }
                    return original(model, errId, isDispatch);
                };

            _installs++;
            _actionStartRpc = rpc;
            _actionStartOriginal = original;
            _actionStartWrapped = wrapped;
            rpc.OnActionStartNotifyS2CServerCallBackAsync = wrapped;
        }

        private void HandlePlayerTurnShift(long newPlayerId, string source)
        {
            if (!_enabled) return;
            if (newPlayerId <= 0) return;

            var gm = SimpleSingletonProvider<GameLogicManager>.inst;
            if (!IsLiveBattle(gm)) return;

            long selfId = gm.account?.GetPlayerID() ?? 0;
            long now = DateTime.UtcNow.Ticks;

            // 轮到自己行动时重置死亡标记
            if (newPlayerId == selfId)
            {
                _isMyDead = false;
            }

            if (TurnEndPolicy.ShouldTrigger(
                _enabled,
                _triggerOnTurnEnd,
                _lastActivePlayerId,
                newPlayerId,
                selfId,
                _onlyMyTurn,
                now,
                _lastTriggerTicks,
                _cooldownSeconds,
                out string reason))
            {
                _lastTriggerTicks = now;
                TriggerOpenUrl(reason);
            }

            _lastActivePlayerId = newPlayerId;
        }

        private void OnHeroAttrUpdated(UpdateHeroAttrS2C model)
        {
            if (!_enabled || !_triggerOnDeath || model == null || model.EffectDatas == null) return;

            var gm = SimpleSingletonProvider<GameLogicManager>.inst;
            if (!IsLiveBattle(gm)) return;

            long selfId = gm.account?.GetPlayerID() ?? 0;
            long now = DateTime.UtcNow.Ticks;

            foreach (var effect in model.EffectDatas)
            {
                if (effect == null || effect.Hp == null) continue;

                var hp = effect.Hp;
                long targetPlayerId = effect.PlayerId != 0 ? effect.PlayerId : hp.PlayerId;

                // 若当前玩家血量回升，解除死亡状态
                if (targetPlayerId == selfId && (hp.RealHp > 0 || hp.CurrHp > 0))
                {
                    _isMyDead = false;
                }

                bool wasDead = (targetPlayerId == selfId) ? _isMyDead : false;

                // 综合考虑 RealHp 与 CurrHp 判定是否归零
                int effectiveNewHp = hp.RealHp;
                if (effectiveNewHp > 0 && hp.CurrHp <= 0)
                {
                    effectiveNewHp = hp.CurrHp;
                }

                if (TurnEndPolicy.ShouldTriggerOnDeath(
                    _enabled,
                    _triggerOnDeath,
                    targetPlayerId,
                    selfId,
                    _onlyMyDeath,
                    hp.OriHp,
                    effectiveNewHp,
                    wasDead,
                    now,
                    _lastTriggerTicks,
                    _cooldownSeconds,
                    out string reason))
                {
                    if (targetPlayerId == selfId)
                    {
                        _isMyDead = true;
                    }

                    _lastTriggerTicks = now;
                    TriggerOpenUrl(reason);
                    break;
                }
            }
        }

        private void TriggerOpenUrl(string reason)
        {
            string url = _targetUrl;
            if (!TurnEndPolicy.IsValidUrl(url))
            {
                Log.Warn($"[TurnEndOpenUrl] 目标网址格式不合法，跳过打开：{url}");
                return;
            }

            Log.Info($"[TurnEndOpenUrl] 【{reason}】触发在默认浏览器中打开网页：{url}");

            MainThread.Post(() =>
            {
                OpenBrowser(url);
            });
        }

        private void OpenBrowser(string url)
        {
            try
            {
                Application.OpenURL(url);
                Log.Info($"[TurnEndOpenUrl] 已通过 Application.OpenURL 打开网页: {url}");
            }
            catch (Exception ex)
            {
                Log.Error("[TurnEndOpenUrl] Application.OpenURL 唤起异常: " + ex);
            }
        }

        private static bool IsLiveBattle(GameLogicManager gm)
        {
            if (gm?.room?.curRoomInfo == null || gm.account == null || gm.battle == null) return false;
            if (!gm.room.IsInRoom || gm.room.roomController == null) return false;
            if (gm.room.roomController.roomStateType != RoomStateType.RUNNING) return false;
            if (!gm.battle.clientFinishReady) return false;
            if (gm.battle.PlayerDatas == null || gm.battle.PlayerDatas.Count == 0) return false;
            if (IsReplayOrWatcher(gm)) return false;

            return true;
        }

        private static bool IsReplayOrWatcher(GameLogicManager gm)
        {
            try { if (gm.replay != null && gm.replay.Session != null && gm.replay.Session.IsReplay) return true; }
            catch { }
            try { if (gm.watch != null && gm.watch.PlayerIsWatcher()) return true; }
            catch { }
            return false;
        }

        public override void OnUnload()
        {
            GameEvents.HeroAttrUpdated -= OnHeroAttrUpdated;

            if (_roundStartRpc != null && ReferenceEquals(_roundStartRpc.OnRoundStartS2CServerCallBackAsync, _roundStartWrapped))
            {
                _roundStartRpc.OnRoundStartS2CServerCallBackAsync = _roundStartOriginal;
            }
            if (_actionStartRpc != null && ReferenceEquals(_actionStartRpc.OnActionStartNotifyS2CServerCallBackAsync, _actionStartWrapped))
            {
                _actionStartRpc.OnActionStartNotifyS2CServerCallBackAsync = _actionStartOriginal;
            }
        }
    }
}
