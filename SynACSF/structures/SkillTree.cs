using System.Collections.Generic;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json;
namespace SynASTF.Structures
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum TypedMethod
    {
        NONE, GLOB, AV
    }
    public struct SkillTree
    {
        public string Name;
        public string Description;
        public TypedMethod Level;
        public string LevelGLOB;
        public TypedMethod PerkPoints;
        public string PerkPointsGLOB;
        public TypedMethod Legendary;
        public string LegendaryGLOB;
        public List<SkillTreePerk> Perks;
    }
    public struct SkillTreePerk
    {
        public List<SkillCondition> Conditions;
        public string Perk;
        public string Name;
        public string Description;
    }
    public struct SkillCondition
    {
        public string Function;
        public string Comparison;
        public string Arg1;
        public string Arg2;
        public string Value;
    }
}