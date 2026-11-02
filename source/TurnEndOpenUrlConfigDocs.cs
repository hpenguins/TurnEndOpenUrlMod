using CesiumLoader.SDK.Configuration;

namespace TurnEndOpenUrlMod
{
    public static class TurnEndOpenUrlConfigDocs
    {
        private static ConfigDoc D(string zhHans, string zhHant, string en, string ja)
        {
            return ConfigDoc.Of(zhHans, zhHant, en, ja);
        }

        public static bool Apply(ModConfig config)
        {
            if (config == null) return false;
            bool changed = false;

            changed |= config.Describe("Enabled", D(
                "模组总开关。",
                "模組總開關。",
                "Master switch for the mod.",
                "Mod全体の有効/無効を切り替えます。"
            ));

            changed |= config.Describe("TargetUrl", D(
                "触发时打开的网页 URL（默认 https://bilibili.com）。",
                "觸發時開啟的網頁 URL（預設 https://bilibili.com）。",
                "The webpage URL to open when triggered (default: https://bilibili.com).",
                "トリガー時に開くWebページのURL（デフォルト: https://bilibili.com）。"
            ));

            changed |= config.Describe("TriggerOnTurnEnd", D(
                "回合结束时自动打开网页（默认关闭）。",
                "回合結束時自動開啟網頁（預設關閉）。",
                "Automatically open the webpage when a turn ends (disabled by default).",
                "ターン終了時にWebページを自動で開きます（デフォルト無効）。"
            ));

            changed |= config.Describe("OnlyMyTurn", D(
                "仅在自己的回合结束时打开网页。关闭后任何玩家或阶段回合结束均会触发。",
                "僅在自己的回合結束時開啟網頁。關閉後任何玩家或階段回合結束均會觸發。",
                "Only open the URL when your own turn ends. If disabled, triggers for any player's turn end.",
                "自分のターン終了時のみWebページを開きます。無効にすると他のプレイヤーのターン終了時にも開きます。"
            ));

            changed |= config.Describe("TriggerOnDeath", D(
                "当前角色死亡时自动打开网页。",
                "當前角色死亡時自動開啟網頁。",
                "Automatically open the webpage when the character dies.",
                "キャラクターが死亡したときにWebページを自動で開きます。"
            ));

            changed |= config.Describe("OnlyMyDeath", D(
                "仅在自己的角色死亡时打开网页。关闭后任何玩家角色死亡均会触发。",
                "僅在自己的角色死亡時開啟網頁。關閉後任何玩家角色死亡均會觸發。",
                "Only open the URL when your own character dies. If disabled, triggers when any player's character dies.",
                "自分のキャラクター死亡時のみWebページを開きます。無効にすると他のプレイヤーの死亡時にも開きます。"
            ));

            changed |= config.Describe("CooldownSeconds", D(
                "触发冷却时间（秒），防止短时间内重复打开多个网页标签页。",
                "觸發冷卻時間（秒），防止短時間內重複開啟多個網頁標籤頁。",
                "Cooldown period in seconds to prevent opening duplicate browser tabs in quick succession.",
                "短時間内に複数のタブが開くのを防ぐためのクールダウン時間（秒）。"
            ));

            return changed;
        }
    }
}
