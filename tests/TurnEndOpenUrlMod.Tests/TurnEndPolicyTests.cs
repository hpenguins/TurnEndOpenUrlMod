using System;
using Xunit;

namespace TurnEndOpenUrlMod.Tests
{
    public sealed class TurnEndPolicyTests
    {
        [Fact]
        public void ShouldTrigger_WhenDisabled_ReturnsFalse()
        {
            long now = 10000000;
            bool ok = TurnEndPolicy.ShouldTrigger(
                enabled: false,
                triggerOnTurnEnd: true,
                lastActivePlayerId: 1001,
                newActivePlayerId: 1002,
                selfPlayerId: 1001,
                onlyMyTurn: true,
                nowTicks: now,
                lastTriggerTicks: 0,
                cooldownSeconds: 3,
                out string reason);

            Assert.False(ok);
            Assert.Null(reason);
        }

        [Fact]
        public void ShouldTrigger_WhenTriggerOnTurnEndIsFalse_ReturnsFalse()
        {
            long now = 10000000;
            // 回合结束触发被禁用时，即便轮换也不触发
            bool ok = TurnEndPolicy.ShouldTrigger(
                enabled: true,
                triggerOnTurnEnd: false,
                lastActivePlayerId: 1001,
                newActivePlayerId: 1002,
                selfPlayerId: 1001,
                onlyMyTurn: true,
                nowTicks: now,
                lastTriggerTicks: 0,
                cooldownSeconds: 3,
                out string reason);

            Assert.False(ok);
            Assert.Null(reason);
        }

        [Fact]
        public void ShouldTrigger_WhenFirstPlayerStarts_ReturnsFalse()
        {
            long now = 10000000;
            // 刚开局，没有前一个行动玩家
            bool ok = TurnEndPolicy.ShouldTrigger(
                enabled: true,
                triggerOnTurnEnd: true,
                lastActivePlayerId: 0,
                newActivePlayerId: 1001,
                selfPlayerId: 1001,
                onlyMyTurn: true,
                nowTicks: now,
                lastTriggerTicks: 0,
                cooldownSeconds: 3,
                out string reason);

            Assert.False(ok);
            Assert.Null(reason);
        }

        [Fact]
        public void ShouldTrigger_WhenSamePlayerRepeats_ReturnsFalse()
        {
            long now = 10000000;
            // 同一个玩家连续多条行动广播（例如 RoundStart + ActionStart）
            bool ok = TurnEndPolicy.ShouldTrigger(
                enabled: true,
                triggerOnTurnEnd: true,
                lastActivePlayerId: 1001,
                newActivePlayerId: 1001,
                selfPlayerId: 1001,
                onlyMyTurn: true,
                nowTicks: now,
                lastTriggerTicks: 0,
                cooldownSeconds: 3,
                out string reason);

            Assert.False(ok);
            Assert.Null(reason);
        }

        [Fact]
        public void ShouldTrigger_WhenMyTurnEnds_ReturnsTrue()
        {
            long now = 10000000;
            // 上一个行动的是自己(1001)，现在轮到 1002
            bool ok = TurnEndPolicy.ShouldTrigger(
                enabled: true,
                triggerOnTurnEnd: true,
                lastActivePlayerId: 1001,
                newActivePlayerId: 1002,
                selfPlayerId: 1001,
                onlyMyTurn: true,
                nowTicks: now,
                lastTriggerTicks: 0,
                cooldownSeconds: 3,
                out string reason);

            Assert.True(ok);
            Assert.Equal("我的回合结束", reason);
        }

        [Fact]
        public void ShouldTrigger_WhenOtherTurnEnds_AndOnlyMyTurnIsTrue_ReturnsFalse()
        {
            long now = 10000000;
            // 上一个是 1002，现在是 1003，而我是 1001
            bool ok = TurnEndPolicy.ShouldTrigger(
                enabled: true,
                triggerOnTurnEnd: true,
                lastActivePlayerId: 1002,
                newActivePlayerId: 1003,
                selfPlayerId: 1001,
                onlyMyTurn: true,
                nowTicks: now,
                lastTriggerTicks: 0,
                cooldownSeconds: 3,
                out string reason);

            Assert.False(ok);
            Assert.Null(reason);
        }

        [Fact]
        public void ShouldTrigger_WhenOtherTurnEnds_AndOnlyMyTurnIsFalse_ReturnsTrue()
        {
            long now = 10000000;
            // 允许所有回合触发，上一个是 1002，现在是 1003
            bool ok = TurnEndPolicy.ShouldTrigger(
                enabled: true,
                triggerOnTurnEnd: true,
                lastActivePlayerId: 1002,
                newActivePlayerId: 1003,
                selfPlayerId: 1001,
                onlyMyTurn: false,
                nowTicks: now,
                lastTriggerTicks: 0,
                cooldownSeconds: 3,
                out string reason);

            Assert.True(ok);
            Assert.Equal("玩家 1002 回合结束", reason);
        }

        [Fact]
        public void ShouldTrigger_CooldownThrottling_WorksCorrectly()
        {
            long lastTrigger = 10 * TimeSpan.TicksPerSecond;
            long withinCooldown = lastTrigger + (2 * TimeSpan.TicksPerSecond); // 仅过了2秒，冷却为3秒
            long afterCooldown = lastTrigger + (4 * TimeSpan.TicksPerSecond);  // 过了4秒

            bool throttled = TurnEndPolicy.ShouldTrigger(
                enabled: true,
                triggerOnTurnEnd: true,
                lastActivePlayerId: 1001,
                newActivePlayerId: 1002,
                selfPlayerId: 1001,
                onlyMyTurn: true,
                nowTicks: withinCooldown,
                lastTriggerTicks: lastTrigger,
                cooldownSeconds: 3,
                out _);

            Assert.False(throttled);

            bool allowed = TurnEndPolicy.ShouldTrigger(
                enabled: true,
                triggerOnTurnEnd: true,
                lastActivePlayerId: 1001,
                newActivePlayerId: 1002,
                selfPlayerId: 1001,
                onlyMyTurn: true,
                nowTicks: afterCooldown,
                lastTriggerTicks: lastTrigger,
                cooldownSeconds: 3,
                out string reason);

            Assert.True(allowed);
            Assert.Equal("我的回合结束", reason);
        }

        [Fact]
        public void ShouldTriggerOnDeath_WhenMyCharacterDies_ReturnsTrue()
        {
            long now = 10000000;
            bool ok = TurnEndPolicy.ShouldTriggerOnDeath(
                enabled: true,
                triggerOnDeath: true,
                deadPlayerId: 1001,
                selfPlayerId: 1001,
                onlyMyDeath: true,
                oriHp: 5,
                newHp: 0,
                wasAlreadyDead: false,
                nowTicks: now,
                lastTriggerTicks: 0,
                cooldownSeconds: 3,
                out string reason);

            Assert.True(ok);
            Assert.Equal("当前角色死亡", reason);
        }

        [Fact]
        public void ShouldTriggerOnDeath_WhenAlreadyDead_ReturnsFalse()
        {
            long now = 10000000;
            // 已经是死亡状态，收到后续扣血或死亡广播时不重复触发
            bool ok = TurnEndPolicy.ShouldTriggerOnDeath(
                enabled: true,
                triggerOnDeath: true,
                deadPlayerId: 1001,
                selfPlayerId: 1001,
                onlyMyDeath: true,
                oriHp: 0,
                newHp: -2,
                wasAlreadyDead: true,
                nowTicks: now,
                lastTriggerTicks: 0,
                cooldownSeconds: 3,
                out string reason);

            Assert.False(ok);
            Assert.Null(reason);
        }

        [Fact]
        public void ShouldTriggerOnDeath_WhenOtherDies_AndOnlyMyDeathIsTrue_ReturnsFalse()
        {
            long now = 10000000;
            bool ok = TurnEndPolicy.ShouldTriggerOnDeath(
                enabled: true,
                triggerOnDeath: true,
                deadPlayerId: 1002,
                selfPlayerId: 1001,
                onlyMyDeath: true,
                oriHp: 3,
                newHp: 0,
                wasAlreadyDead: false,
                nowTicks: now,
                lastTriggerTicks: 0,
                cooldownSeconds: 3,
                out string reason);

            Assert.False(ok);
            Assert.Null(reason);
        }

        [Fact]
        public void ShouldTriggerOnDeath_WhenOtherDies_AndOnlyMyDeathIsFalse_ReturnsTrue()
        {
            long now = 10000000;
            bool ok = TurnEndPolicy.ShouldTriggerOnDeath(
                enabled: true,
                triggerOnDeath: true,
                deadPlayerId: 1002,
                selfPlayerId: 1001,
                onlyMyDeath: false,
                oriHp: 3,
                newHp: 0,
                wasAlreadyDead: false,
                nowTicks: now,
                lastTriggerTicks: 0,
                cooldownSeconds: 3,
                out string reason);

            Assert.True(ok);
            Assert.Equal("玩家 1002 死亡", reason);
        }

        [Fact]
        public void ShouldTriggerOnDeath_WhenTriggerOnDeathIsFalse_ReturnsFalse()
        {
            long now = 10000000;
            bool ok = TurnEndPolicy.ShouldTriggerOnDeath(
                enabled: true,
                triggerOnDeath: false,
                deadPlayerId: 1001,
                selfPlayerId: 1001,
                onlyMyDeath: true,
                oriHp: 5,
                newHp: 0,
                wasAlreadyDead: false,
                nowTicks: now,
                lastTriggerTicks: 0,
                cooldownSeconds: 3,
                out string reason);

            Assert.False(ok);
            Assert.Null(reason);
        }

        [Fact]
        public void ShouldTriggerOnDeath_WhenNotZeroHp_ReturnsFalse()
        {
            long now = 10000000;
            // 只是扣血，生命值仍然大于 0
            bool ok = TurnEndPolicy.ShouldTriggerOnDeath(
                enabled: true,
                triggerOnDeath: true,
                deadPlayerId: 1001,
                selfPlayerId: 1001,
                onlyMyDeath: true,
                oriHp: 5,
                newHp: 2,
                wasAlreadyDead: false,
                nowTicks: now,
                lastTriggerTicks: 0,
                cooldownSeconds: 3,
                out string reason);

            Assert.False(ok);
            Assert.Null(reason);
        }

        [Theory]
        [InlineData("https://bilibili.com/", true)]
        [InlineData("http://bilibili.com", true)]
        [InlineData("HTTPS://WWW.BILIBILI.COM/VIDEO/BV123", true)]
        [InlineData("", false)]
        [InlineData(null, false)]
        [InlineData("ftp://example.com", false)]
        [InlineData("file:///c:/test", false)]
        public void IsValidUrl_ValidatesProtocol(string url, bool expected)
        {
            Assert.Equal(expected, TurnEndPolicy.IsValidUrl(url));
        }

        [Theory]
        [InlineData(-5, 0)]
        [InlineData(0, 0)]
        [InlineData(10, 10)]
        [InlineData(500, 300)]
        public void ClampCooldownSeconds_BoundsValues(int input, int expected)
        {
            Assert.Equal(expected, TurnEndPolicy.ClampCooldownSeconds(input));
        }
    }
}
