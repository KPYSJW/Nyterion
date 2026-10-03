using System.Collections.Generic;

namespace Nytherion.Editor.Localization
{
    internal readonly struct TranslationEntry
    {
        public readonly string Key;
        public readonly string Korean;
        public readonly string English;

        public TranslationEntry(string key, string korean, string english)
        {
            Key = key;
            Korean = korean;
            English = english;
        }
    }

    internal static class LocalizationTranslationCatalog
    {
        public static readonly IReadOnlyDictionary<string, TranslationEntry> UIEntries =
            new Dictionary<string, TranslationEntry>
            {
                ["ui.settings.language"] = new TranslationEntry("ui.settings.language", "언어 :", "Language :"),
                ["ui.settings.audio"] = new TranslationEntry("ui.settings.audio", "오디오", "Audio"),
                ["ui.settings.graphics"] = new TranslationEntry("ui.settings.graphics", "그래픽", "Graphics"),
                ["ui.settings.controls"] = new TranslationEntry("ui.settings.controls", "조작", "Controls"),
                ["ui.settings.master"] = new TranslationEntry("ui.settings.master", "전체 음량", "Master"),
                ["ui.settings.bgm"] = new TranslationEntry("ui.settings.bgm", "BGM", "BGM"),
                ["ui.settings.sfx"] = new TranslationEntry("ui.settings.sfx", "효과음", "SFX"),
                ["ui.settings.fullscreen"] = new TranslationEntry("ui.settings.fullscreen", "전체 화면 :", "Fullscreen :"),
                ["ui.settings.resolution"] = new TranslationEntry("ui.settings.resolution", "해상도 :", "Resolution :"),
                ["ui.common.close"] = new TranslationEntry("ui.common.close", "닫기", "Close"),
                ["ui.common.confirm"] = new TranslationEntry("ui.common.confirm", "확인", "Confirm"),
                ["ui.common.cancel"] = new TranslationEntry("ui.common.cancel", "취소", "Cancel"),
                ["ui.common.exit"] = new TranslationEntry("ui.common.exit", "종료", "Exit"),
                ["ui.common.resume"] = new TranslationEntry("ui.common.resume", "계속하기", "Resume"),
                ["ui.shop.title"] = new TranslationEntry("ui.shop.title", "상점", "Shop"),
                ["ui.shop.sell"] = new TranslationEntry("ui.shop.sell", "판매", "Sell"),
                ["ui.shop.no_items"] = new TranslationEntry("ui.shop.no_items", "판매 중인 아이템이 없습니다.", "No items on sale."),
                ["ui.gacha.title"] = new TranslationEntry("ui.gacha.title", "가챠", "Gacha"),
                ["ui.gacha.relic"] = new TranslationEntry("ui.gacha.relic", "유물", "Relic"),
                ["ui.gacha.relic_one"] = new TranslationEntry("ui.gacha.relic_one", "유물 x 1", "Relic x 1"),
                ["ui.gacha.relic_ten"] = new TranslationEntry("ui.gacha.relic_ten", "유물 x 10", "Relic x 10"),
                ["ui.gacha.weapon_one"] = new TranslationEntry("ui.gacha.weapon_one", "무기 x 1", "Weapon x 1"),
                ["ui.gacha.weapon_ten"] = new TranslationEntry("ui.gacha.weapon_ten", "무기 x 10", "Weapon x 10"),
                ["ui.progression.milestone"] = new TranslationEntry("ui.progression.milestone", "마일스톤", "Milestone"),
                ["ui.tooltip.attack"] = new TranslationEntry("ui.tooltip.attack", "공격력: {0:F1}", "Attack: {0:F1}"),
                ["ui.tooltip.attack_speed"] = new TranslationEntry("ui.tooltip.attack_speed", "공격 속도: {0:0.##}", "Attack Speed: {0:0.##}"),
                ["ui.tooltip.additional_stats"] = new TranslationEntry("ui.tooltip.additional_stats", "추가 능력치", "Additional Stats"),
                ["ui.tooltip.inverted"] = new TranslationEntry("ui.tooltip.inverted", "반전됨!", "Inverted!"),
                ["ui.tooltip.skill_stats"] = new TranslationEntry("ui.tooltip.skill_stats", "Lv.{0} / 경험치 {1}/{2}\n피해 {3} / 쿨타임 {4}초 / 사거리 {5}\n\n{6}", "Lv.{0} / EXP {1}/{2}\nDamage {3} / Cooldown {4}s / Range {5}\n\n{6}"),
                ["ui.skill_tooltip.progress"] = new TranslationEntry("ui.skill_tooltip.progress", "Lv.{0} / 경험치 {1}/{2}", "Lv.{0} / EXP {1}/{2}"),
                ["ui.skill_tooltip.stats"] = new TranslationEntry("ui.skill_tooltip.stats", "피해  {0}\n쿨타임  {1}초", "Damage  {0}\nCooldown  {1}s"),
                ["ui.tooltip.milestone.completed"] = new TranslationEntry("ui.tooltip.milestone.completed", "달성 완료", "Completed"),
                ["ui.tooltip.milestone.in_progress"] = new TranslationEntry("ui.tooltip.milestone.in_progress", "진행 중 ({0} / {1})", "In progress ({0} / {1})"),
                ["ui.tooltip.milestone.content"] = new TranslationEntry("ui.tooltip.milestone.content", "{0}\n\n상태: {1}", "{0}\n\nStatus: {1}"),
                ["ui.tooltip.reward.skill"] = new TranslationEntry("ui.tooltip.reward.skill", "\n보상: {0} 스킬 획득", "\nReward: Unlock {0}"),
                ["ui.tooltip.reward.gold"] = new TranslationEntry("ui.tooltip.reward.gold", "\n보상: 골드 {0}", "\nReward: {0} Gold"),
                ["ui.tooltip.reward.token"] = new TranslationEntry("ui.tooltip.reward.token", "\n보상: 토큰 {0}", "\nReward: {0} Tokens"),
                ["ui.relic.effect_description_missing"] = new TranslationEntry("ui.relic.effect_description_missing", "효과 설명이 설정되지 않았습니다.", "No effect description has been configured."),
                ["ui.relic.transcendence.active"] = new TranslationEntry("ui.relic.transcendence.active", "\n활성화됨", "\nActive"),
                ["ui.relic.transcendence.inactive"] = new TranslationEntry("ui.relic.transcendence.inactive", "\n활성 조건 미달", "\nActivation requirements not met"),
                ["ui.stat.maxHealth"] = new TranslationEntry("ui.stat.maxHealth", "최대 체력", "Max Health"),
                ["ui.stat.defense"] = new TranslationEntry("ui.stat.defense", "방어력", "Defense"),
                ["ui.stat.moveSpeed"] = new TranslationEntry("ui.stat.moveSpeed", "이동 속도", "Move Speed"),
                ["ui.stat.meleeDamage"] = new TranslationEntry("ui.stat.meleeDamage", "근접 공격력", "Melee Damage"),
                ["ui.stat.rangedDamage"] = new TranslationEntry("ui.stat.rangedDamage", "원거리 공격력", "Ranged Damage"),
                ["ui.stat.meleeSpeed"] = new TranslationEntry("ui.stat.meleeSpeed", "근접 공격 속도", "Melee Attack Speed"),
                ["ui.stat.rangedSpeed"] = new TranslationEntry("ui.stat.rangedSpeed", "원거리 공격 속도", "Ranged Attack Speed"),
                ["ui.stat.extraProjectiles"] = new TranslationEntry("ui.stat.extraProjectiles", "추가 투사체 수", "Extra Projectiles"),
                ["ui.stat.lifesteal"] = new TranslationEntry("ui.stat.lifesteal", "생명력 흡수", "Lifesteal"),
                ["ui.stat.chargeTimeReduction"] = new TranslationEntry("ui.stat.chargeTimeReduction", "충전 시간 감소", "Charge Time Reduction"),
                ["ui.stat.critChance"] = new TranslationEntry("ui.stat.critChance", "치명타 확률", "Critical Chance"),
                ["ui.stat.critDamageMultiplier"] = new TranslationEntry("ui.stat.critDamageMultiplier", "치명타 피해량", "Critical Damage")
            };

        public static readonly IReadOnlyDictionary<string, TranslationEntry> StaticTextBySource =
            new Dictionary<string, TranslationEntry>
            {
                ["Close"] = UIEntries["ui.common.close"],
                ["Confirm"] = UIEntries["ui.common.confirm"],
                ["Cancel"] = UIEntries["ui.common.cancel"],
                ["Exit"] = UIEntries["ui.common.exit"],
                ["계속하기"] = UIEntries["ui.common.resume"],
                ["Audio"] = UIEntries["ui.settings.audio"],
                ["Graphic"] = UIEntries["ui.settings.graphics"],
                ["Graphics"] = UIEntries["ui.settings.graphics"],
                ["Controls"] = UIEntries["ui.settings.controls"],
                ["조작키"] = UIEntries["ui.settings.controls"],
                ["Master"] = UIEntries["ui.settings.master"],
                ["BGM"] = UIEntries["ui.settings.bgm"],
                ["SFX"] = UIEntries["ui.settings.sfx"],
                ["Fullscreen :"] = UIEntries["ui.settings.fullscreen"],
                ["Resolution :"] = UIEntries["ui.settings.resolution"],
                ["상점"] = UIEntries["ui.shop.title"],
                ["Sell"] = UIEntries["ui.shop.sell"],
                ["No items on sale"] = UIEntries["ui.shop.no_items"],
                ["No items on sale."] = UIEntries["ui.shop.no_items"],
                ["가챠"] = UIEntries["ui.gacha.title"],
                ["유물"] = UIEntries["ui.gacha.relic"],
                ["Relic"] = UIEntries["ui.gacha.relic"],
                ["Relic x 1"] = UIEntries["ui.gacha.relic_one"],
                ["Relic x 10"] = UIEntries["ui.gacha.relic_ten"],
                ["Weapon x 1"] = UIEntries["ui.gacha.weapon_one"],
                ["Weapon x 10"] = UIEntries["ui.gacha.weapon_ten"]
            };

        public static readonly IReadOnlyDictionary<string, (string Korean, string English)> Skills =
            new Dictionary<string, (string, string)>
            {
                ["skill_all_stat_up"] = ("모든 능력치 증가", "All Stat Up"),
                ["skill_attack_speed"] = ("공격 속도 증가", "Attack Speed Boost"),
                ["skill_Atk_buff"] = ("공격력 강화", "Attack Buff"),
                ["skill_aura"] = ("오라", "Aura"),
                ["skill_blackhole"] = ("블랙홀", "Black Hole"),
                ["skill_Dash_cooldown"] = ("대시 재사용 대기시간 감소", "Dash Cooldown Reduction"),
                ["skill_laser"] = ("레이저", "Laser"),
                ["skill_lifesteal"] = ("생명력 흡수", "Lifesteal"),
                ["skill_meteor_strike"] = ("운석 낙하", "Meteor Strike"),
                ["skill_multishot"] = ("다중 사격", "Multi Shot"),
                ["skill_overdrive"] = ("오버드라이브", "Overdrive"),
                ["skill_shadow_clone"] = ("그림자 분신", "Shadow Clone"),
                ["skill_souleater"] = ("영혼 포식자", "Soul Eater"),
                ["skill_spiral"] = ("나선", "Spiral"),
                ["skill_turret"] = ("루티 포탑", "Rooti Turret"),
                ["skill_pyro_tank"] = ("폭열 전차", "Pyro Tank"),
                ["skill_chain_ignition"] = ("연쇄 점화", "Chain Ignition")
            };

        public static readonly IReadOnlyDictionary<string, string> SkillKoreanDescriptions =
            new Dictionary<string, string>
            {
                ["skill_all_stat_up"] = "일정 시간 모든 능력치 증가",
                ["skill_attack_speed"] = "일정 시간 공격 속도 증가",
                ["skill_Atk_buff"] = "일정 시간 공격력 증가",
                ["skill_aura"] = "주변 적에게 지속 피해를 주는 오라 생성",
                ["skill_blackhole"] = "적을 끌어당기며 피해를 주는 블랙홀 생성",
                ["skill_Dash_cooldown"] = "대시 쿨타임 감소",
                ["skill_laser"] = "적을 관통하는 레이저 발사",
                ["skill_lifesteal"] = "일정 시간 공격 피해로 체력 회복",
                ["skill_meteor_strike"] = "운석을 떨어뜨려 주변 적 공격",
                ["skill_multishot"] = "추가 투사체 발사",
                ["skill_overdrive"] = "일정 시간 공격 속도와 대시 속도 증가",
                ["skill_shadow_clone"] = "함께 공격하는 그림자 분신 소환",
                ["skill_souleater"] = "적을 처치할수록 강해지는 투사체 발사",
                ["skill_spiral"] = "주변 적을 공격하는 회전 구체 소환",
                ["skill_turret"] = "적을 자동으로 공격하는 루티 포탑 설치",
                ["skill_pyro_tank"] = "적에게 돌진해 폭발하는 전차 소환",
                ["skill_chain_ignition"] = "조준 방향으로 연속 화염 폭발"
            };

        public static readonly IReadOnlyDictionary<string, string> SkillEnglishDescriptions =
            new Dictionary<string, string>
            {
                ["skill_all_stat_up"] = "Temporarily boosts all stats.",
                ["skill_attack_speed"] = "Temporarily boosts attack speed.",
                ["skill_Atk_buff"] = "Temporarily boosts attack.",
                ["skill_aura"] = "Creates an aura that damages nearby enemies.",
                ["skill_blackhole"] = "Creates a black hole that pulls in and damages enemies.",
                ["skill_Dash_cooldown"] = "Reduces dash cooldown.",
                ["skill_laser"] = "Fires a laser that pierces enemies.",
                ["skill_lifesteal"] = "Temporarily restores health by dealing damage.",
                ["skill_meteor_strike"] = "Drops a meteor to strike nearby enemies.",
                ["skill_multishot"] = "Fires extra projectiles.",
                ["skill_overdrive"] = "Temporarily boosts attack speed and dash speed.",
                ["skill_shadow_clone"] = "Summons a shadow clone that attacks alongside you.",
                ["skill_souleater"] = "Fires a projectile that grows stronger with each kill.",
                ["skill_spiral"] = "Summons spinning orbs that attack nearby enemies.",
                ["skill_turret"] = "Deploys a Rooti turret that automatically attacks enemies.",
                ["skill_pyro_tank"] = "Summons a tank that charges at enemies and explodes.",
                ["skill_chain_ignition"] = "Unleashes successive fire explosions toward your aim."
            };

        public static readonly IReadOnlyDictionary<string, TranslationEntry> Milestones =
            new Dictionary<string, TranslationEntry>
            {
                ["All_Stat"] = new TranslationEntry("All_Stat", "모든 능력치 강화", "All Stat Buff"),
                ["AS"] = new TranslationEntry("AS", "공격 속도 증가", "Attack Speed Boost"),
                ["Atk_Buff"] = new TranslationEntry("Atk_Buff", "공격력 강화", "Attack Buff"),
                ["Aura"] = new TranslationEntry("Aura", "오라", "Aura"),
                ["Blackhole"] = new TranslationEntry("Blackhole", "블랙홀", "Black Hole"),
                ["Dash_CD"] = new TranslationEntry("Dash_CD", "대시 재사용 대기시간 감소", "Dash Cooldown Reduction"),
                ["Laser"] = new TranslationEntry("Laser", "레이저", "Laser"),
                ["Lifesteal"] = new TranslationEntry("Lifesteal", "생명력 흡수", "Lifesteal"),
                ["Shadow_Clone"] = new TranslationEntry("Shadow_Clone", "그림자 분신", "Shadow Clone"),
                ["Soul_Eater"] = new TranslationEntry("Soul_Eater", "영혼 포식자", "Soul Eater"),
                ["Spiral"] = new TranslationEntry("Spiral", "나선", "Spiral"),
                ["Turret"] = new TranslationEntry("Turret", "포탑", "Turret"),
                ["BreakingThePiggy"] = new TranslationEntry("BreakingThePiggy", "저금통 터는 날", "Breaking the Piggy Bank"),
                ["CenterOfAttention"] = new TranslationEntry("CenterOfAttention", "주인공 체질", "Center of Attention"),
                ["ComfyCorner"] = new TranslationEntry("ComfyCorner", "아늑한 구석탱이", "Comfy Corner"),
                ["Drone"] = new TranslationEntry("Drone", "슬라임 소환수", "Slime Companion"),
                ["EvenTempered"] = new TranslationEntry("EvenTempered", "짝이 맞아야 직성이 풀림", "Even-Tempered"),
                ["ExpiredCoupon"] = new TranslationEntry("ExpiredCoupon", "이거 기한 지났는데요", "Isn't This Expired?"),
                ["milestone_first_steps"] = new TranslationEntry("milestone_first_steps", "첫 걸음마", "First Steps"),
                ["milestone_giant_slayer"] = new TranslationEntry("milestone_giant_slayer", "거인 사냥꾼", "Giant Slayer"),
                ["GlassCannon"] = new TranslationEntry("GlassCannon", "진짜 유리몸", "True Glass Cannon"),
                ["milestone_gold_collector_1"] = new TranslationEntry("milestone_gold_collector_1", "골드 수집가 I", "Gold Collector I"),
                ["milestone_kill_100"] = new TranslationEntry("milestone_kill_100", "적 100마리 처치", "Defeat 100 Enemies"),
                ["LuckOverdose"] = new TranslationEntry("LuckOverdose", "오늘의 행운아", "Lucky Day"),
                ["milestone_nytherion_conqueror"] = new TranslationEntry("milestone_nytherion_conqueror", "심연의 정복자", "Conqueror of the Abyss"),
                ["PouchLeak"] = new TranslationEntry("PouchLeak", "주머니가 샌다?", "Leaky Pouch?"),
                ["milestone_relic_collector"] = new TranslationEntry("milestone_relic_collector", "던전의 정복자", "Dungeon Conqueror"),
                ["milestone_rich_man"] = new TranslationEntry("milestone_rich_man", "재벌 데뷔", "Tycoon Debut"),
                ["milestone_skill_expert_1"] = new TranslationEntry("milestone_skill_expert_1", "스킬 전문가 I", "Skill Expert I"),
                ["milestone_skill_master"] = new TranslationEntry("milestone_skill_master", "마법 학회 졸업", "Arcane Academy Graduate"),
                ["SocialDistancing"] = new TranslationEntry("SocialDistancing", "사회적 거리두기", "Social Distancing"),
                ["milestone_speedrunner"] = new TranslationEntry("milestone_speedrunner", "속도광", "Speedrunner"),
                ["milestone_survivor"] = new TranslationEntry("milestone_survivor", "산전수전", "Battle-Hardened"),
                ["milestone_untouchable"] = new TranslationEntry("milestone_untouchable", "무결점 보스 격파", "Flawless Boss Victory"),
                ["YarnUntangler"] = new TranslationEntry("YarnUntangler", "실타래 풀기 장인", "Master Yarn Untangler")
            };

        public static readonly IReadOnlyDictionary<string, string> MilestoneEnglishDescriptions =
            new Dictionary<string, string>
            {
                ["BreakingThePiggy"] = "Reach the maximum +10 attack bonus from 'Golden Coin'.",
                ["CenterOfAttention"] = "Place 'Center Pebble' in the exact center of the relic board.",
                ["ComfyCorner"] = "Clear a stage with 'Corner Pebble' placed along the edge of the board.",
                ["Drone"] = "Acquire the Slime Companion relic.",
                ["EvenTempered"] = "Trigger the effect of 'Squeaky Gear'.",
                ["ExpiredCoupon"] = "Purchase 10 shop items while 'Coupon Fragment' is equipped.",
                ["milestone_first_steps"] = "Clear the first floor of the dungeon.",
                ["milestone_giant_slayer"] = "Defeat the dungeon boss and clear floor 3.",
                ["GlassCannon"] = "Defeat a boss while 'Glass Sword' is equipped and maximum health is locked to 50.",
                ["milestone_gold_collector_1"] = "Collect a total of 1,000 gold in the dungeon.",
                ["milestone_kill_100"] = "Defeat 100 enemies in the dungeon.",
                ["LuckOverdose"] = "Reset dash cooldown with 'Four-Leaf Clover' at least 3 times in a single battle.",
                ["milestone_nytherion_conqueror"] = "Defeat the powerful guardian on the final fifth floor and conquer the depths of Nytherion.",
                ["PouchLeak"] = "Trigger the effect of 'Torn Pouch' a total of 15 times.",
                ["milestone_relic_collector"] = "Conquer the dungeon's midpoint on floor 3 a total of 3 times.",
                ["milestone_rich_man"] = "Collect a total of 5,000 gold during dungeon runs.",
                ["milestone_skill_expert_1"] = "Use skills a total of 50 times in the dungeon.",
                ["milestone_skill_master"] = "Cast skills a total of 200 times during dungeon runs.",
                ["SocialDistancing"] = "Trigger 'Long Branch' while all four adjacent slots are empty.",
                ["milestone_speedrunner"] = "Reach 300 seconds (5 minutes) of total play time.",
                ["milestone_survivor"] = "Take a total of 1,000 damage from enemy attacks.",
                ["milestone_untouchable"] = "Reach floor 5 and prove your mastery of survival.",
                ["YarnUntangler"] = "Clear a room while 'Tangled Yarn' has at least 3 active chain links."
            };

        public static readonly IReadOnlyDictionary<string, string> RelicEnglishDescriptions =
            new Dictionary<string, string>
            {
                ["Blue Mushroom"] = "Melee attack +10%, defense +5.",
                ["Fugitive's Flask"] = "Move speed permanently +5%.",
                ["Leather Bracelet"] = "Melee physical damage +5%.",
                ["Mysterious Flask"] = "Dash cooldown -8%.",
                ["Mystical Crystal"] = "All base stats +6%.",
                ["Golden Chalice"] = "Ranged projectiles +2, ranged attack speed +10%.",
                ["WornShield"] = "Slightly boosts base defense.",
                ["Pouch of Abundance"] = "Ranged projectiles +1, ranged attack +5%.",
                ["Skull Ring"] = "Critical damage +25%.",
                ["Starlight Shard"] = "Critical chance +5%.",
                ["Stone Mask"] = "Maximum health +15 (+5 per level).",
                ["Thread Spindle"] = "Weapon and skill charge time -15%.",
                ["Watcher's Eye"] = "Ranged attack speed +8%.",
                ["Golden Orb"] = "Ranged attack +10%, ranged attack speed +5%.",
                ["Wyvern Emblem"] = "Melee attack speed +8%.",
                ["Wooden Totem"] = "Ranged physical damage +5%."
            };

        public static readonly IReadOnlyDictionary<string, string> ItemKoreanNames =
            new Dictionary<string, string>
            {
                ["ChargingSpread"] = "충전 산탄"
            };

        public static readonly IReadOnlyDictionary<string, string> EnemyKoreanNames =
            new Dictionary<string, string>
            {
                ["Melee"] = "근접형",
                ["Ranged"] = "원거리형",
                ["Hybrid"] = "혼합형"
            };
    }
}
