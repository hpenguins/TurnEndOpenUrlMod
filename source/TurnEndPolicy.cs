using System;

namespace TurnEndOpenUrlMod
{
    /// <summary>
    /// 回合结束触发与冷却判定的纯逻辑类（不碰游戏单例，可离线单测）。
    /// </summary>
    public static class TurnEndPolicy
    {
        public const int DefaultCooldownSeconds = 3;
        public const int MaxCooldownSeconds = 300;

        /// <summary>
        /// 限制冷却秒数在有效范围内 [0, 300]。
        /// </summary>
        public static int ClampCooldownSeconds(int seconds)
        {
            if (seconds < 0) return 0;
            return seconds > MaxCooldownSeconds ? MaxCooldownSeconds : seconds;
        }

        /// <summary>
        /// 校验 URL 是否合法（必须非空且以 http:// 或 https:// 开头）。
        /// </summary>
        public static bool IsValidUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 判定当收到新的行动玩家 ID 时，上一个玩家的回合是否应当触发打开网页。
        /// </summary>
        /// <param name="enabled">模组总开关</param>
        /// <param name="triggerOnTurnEnd">回合结束触发开关</param>
        /// <param name="lastActivePlayerId">上一个行动的玩家 ID（初始为 0）</param>
        /// <param name="newActivePlayerId">新开始行动的玩家 ID</param>
        /// <param name="selfPlayerId">当前玩家自身的 ID</param>
        /// <param name="onlyMyTurn">是否仅在自己回合结束时触发</param>
        /// <param name="nowTicks">当前 Utc 纳秒刻度</param>
        /// <param name="lastTriggerTicks">上次触发的 Utc 刻度</param>
        /// <param name="cooldownSeconds">冷却秒数</param>
        /// <param name="reason">触发原因说明</param>
        /// <returns>是否应触发打开网页</returns>
        public static bool ShouldTrigger(
            bool enabled,
            bool triggerOnTurnEnd,
            long lastActivePlayerId,
            long newActivePlayerId,
            long selfPlayerId,
            bool onlyMyTurn,
            long nowTicks,
            long lastTriggerTicks,
            int cooldownSeconds,
            out string reason)
        {
            reason = null;
            if (!enabled || !triggerOnTurnEnd) return false;
            if (newActivePlayerId <= 0) return false;

            // 如果与当前记录的行动玩家完全相同，属于同一个回合内的多条广播或心跳通知，不属于回合结束
            if (newActivePlayerId == lastActivePlayerId)
            {
                return false;
            }

            // 如果是刚进局的第一个玩家开始行动（lastActivePlayerId == 0），前面没有任何玩家的回合结束，不触发
            if (lastActivePlayerId <= 0)
            {
                return false;
            }

            // 判断上一个结束的玩家是否是自己
            bool wasMyTurn = (selfPlayerId > 0 && lastActivePlayerId == selfPlayerId);

            if (onlyMyTurn && !wasMyTurn)
            {
                return false;
            }

            // 冷却判定（仅在曾经触发过时判定冷却间隔）
            if (lastTriggerTicks > 0)
            {
                int clampedCooldown = ClampCooldownSeconds(cooldownSeconds);
                long cooldownTicks = (long)clampedCooldown * TimeSpan.TicksPerSecond;
                if (nowTicks - lastTriggerTicks < cooldownTicks)
                {
                    return false;
                }
            }

            reason = wasMyTurn ? "我的回合结束" : $"玩家 {lastActivePlayerId} 回合结束";
            return true;
        }

        /// <summary>
        /// 判定是否由于角色死亡而触发打开网页。
        /// </summary>
        /// <param name="enabled">模组总开关</param>
        /// <param name="triggerOnDeath">死亡触发开关</param>
        /// <param name="deadPlayerId">受到伤害/发生血量变化的玩家 ID</param>
        /// <param name="selfPlayerId">当前玩家自身的 ID</param>
        /// <param name="onlyMyDeath">是否仅在自己的角色死亡时触发</param>
        /// <param name="oriHp">变化前的生命值</param>
        /// <param name="newHp">变化后的生命值</param>
        /// <param name="wasAlreadyDead">此前是否已经处于死亡状态（避免单次死亡重复触发）</param>
        /// <param name="nowTicks">当前 Utc 纳秒刻度</param>
        /// <param name="lastTriggerTicks">上次触发的 Utc 刻度</param>
        /// <param name="cooldownSeconds">冷却秒数</param>
        /// <param name="reason">触发原因说明</param>
        /// <returns>是否应触发打开网页</returns>
        public static bool ShouldTriggerOnDeath(
            bool enabled,
            bool triggerOnDeath,
            long deadPlayerId,
            long selfPlayerId,
            bool onlyMyDeath,
            int oriHp,
            int newHp,
            bool wasAlreadyDead,
            long nowTicks,
            long lastTriggerTicks,
            int cooldownSeconds,
            out string reason)
        {
            reason = null;
            if (!enabled || !triggerOnDeath) return false;
            if (deadPlayerId <= 0) return false;

            // 必须是从存活状态（oriHp > 0 且 !wasAlreadyDead）变为死亡状态（newHp <= 0）
            if (wasAlreadyDead || oriHp <= 0 || newHp > 0)
            {
                return false;
            }

            bool isMyDeath = (selfPlayerId > 0 && deadPlayerId == selfPlayerId);
            if (onlyMyDeath && !isMyDeath)
            {
                return false;
            }

            // 冷却判定（仅在曾经触发过时判定冷却间隔）
            if (lastTriggerTicks > 0)
            {
                int clampedCooldown = ClampCooldownSeconds(cooldownSeconds);
                long cooldownTicks = (long)clampedCooldown * TimeSpan.TicksPerSecond;
                if (nowTicks - lastTriggerTicks < cooldownTicks)
                {
                    return false;
                }
            }

            reason = isMyDeath ? "当前角色死亡" : $"玩家 {deadPlayerId} 死亡";
            return true;
        }
    }
}
