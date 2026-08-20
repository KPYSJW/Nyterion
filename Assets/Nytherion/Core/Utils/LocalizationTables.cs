namespace Nytherion.Core.Utils
{
    public static class LocalizationTables
    {
        public const string UI = "UI";
        public const string Items = "Items";
        public const string Skills = "Skills";
        public const string Relics = "Relics";
        public const string Progression = "Progression";
        public const string World = "World";
    }

    public static class LocalizationKeys
    {
        public static string ItemName(string itemId) => $"item.{itemId}.name";
        public static string ItemDescription(string itemId) => $"item.{itemId}.description";

        public static string SkillName(string skillId) => $"skill.{skillId}.name";
        public static string SkillDescription(string skillId) => $"skill.{skillId}.description";

        public static string RelicName(string relicId) => $"relic.{relicId}.name";
        public static string RelicDescription(string relicId) => $"relic.{relicId}.description";
        public static string RelicSetName(string seriesId) => $"relic_set.{seriesId}.name";
        public static string RelicSetDescription(string seriesId) => $"relic_set.{seriesId}.description";
        public static string RelicTranscendenceName(string effectId) => $"relic_transcendence.{effectId}.name";
        public static string RelicTranscendenceDescription(string effectId) => $"relic_transcendence.{effectId}.description";
        public static string RelicModuleDescription(string moduleId) => $"relic_module.{moduleId}.description";

        public static string MilestoneTitle(string milestoneId) => $"milestone.{milestoneId}.title";
        public static string MilestoneDescription(string milestoneId) => $"milestone.{milestoneId}.description";

        public static string StageName(string stageId) => $"stage.{stageId}.name";
        public static string EnemyName(string enemyId) => $"enemy.{enemyId}.name";
    }
}
