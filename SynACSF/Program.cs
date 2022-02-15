using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;

using Noggog;

using Newtonsoft.Json;

using SynASTF.Structures;

namespace SynACSF
{
    internal class Program
    {
        public static async Task<int> Main(string[] args)
        {
            return await SynthesisPipeline.Instance
                .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch)
                .SetTypicalOpen(GameRelease.SkyrimSE, "SynACSF.esp")
                .Run(args);
        }

        public static IPerkGetter GetPerkFromFile(IPatcherState<ISkyrimMod, ISkyrimModGetter> state, FormKey form)
        {
            return state.LinkCache.Resolve<IPerkGetter>(form);
        }

        public static void ReadPerk(IPatcherState<ISkyrimMod, ISkyrimModGetter> state, IFormLinkNullableGetter<IPerkGetter> Perk, SkillTree tree) {
            SkillTreePerk perk = new();
            var PerkForm = Perk.Resolve<IPerkGetter>(state.LinkCache);
            perk.Perk = $"__formData|{PerkForm.FormKey.ModKey.FileName}|0x{PerkForm.FormKey.IDString()}";
            perk.Conditions = new();
            perk.Name = PerkForm.Name?.String ?? "";
            var msg = state.PatchMod.Messages.AddNew();
            msg.Description = PerkForm.Description?.String ?? ""; ;
            msg.Flags = Message.Flag.MessageBox;
            msg.INAM = new MemorySlice<byte>(new byte[4]);
            msg.MenuButtons.Add(new MessageButton()
            {
                Text = "Yes"
            });
            msg.MenuButtons.Add(new MessageButton()
            {
                Text = "No"
            });
            msg.Name = $"Take This Perk {PerkForm.Name?.String}?";
            msg.EditorID = PerkForm.EditorID + "_TAKE";
            perk.Description = $"__formData|{msg.FormKey.ModKey.FileName}|0x{msg.FormKey.IDString()}";
            foreach (var cond in PerkForm.Conditions)
            {
                if (cond.GetType().ToString() == "Mutagen.Bethesda.Skyrim.Internals.ConditionFloatBinaryOverlay")
                {
                    IConditionFloatGetter conditionFloat = (IConditionFloatGetter)cond.DeepCopy();
                    IFunctionConditionDataGetter conditionData = (IFunctionConditionDataGetter)conditionFloat.Data;
                    var condition = new SkillCondition();
                    condition.Comparison = cond.CompareOperator.ToString();
                    condition.Function = conditionData.Function.ToString();
                    if (!conditionData.ParameterOneRecord.IsNull)
                    {
                        condition.Arg1 = $"__formData|{conditionData.ParameterOneRecord.FormKey.ModKey.FileName}|0x{conditionData.ParameterOneRecord.FormKey.IDString()}";
                        if (condition.Function == "GetGlobalValue" && cond.CompareOperator == CompareOperator.GreaterThanOrEqualTo)
                        {
                            msg.Description += $"\nREQUIRES {conditionFloat.ComparisonValue} {conditionData.ParameterOneRecord.Resolve(state.LinkCache).EditorID}";
                        }
                    }
                    else
                    {
                        //Console.WriteLine(conditionData.ParameterOneNumber);
                        //Console.WriteLine(conditionData.ParameterOneString);
                    }
                    if (!conditionData.ParameterTwoRecord.IsNull)
                    {
                        condition.Arg2 = $"__formData|{conditionData.ParameterTwoRecord.FormKey.ModKey.FileName}|0x{conditionData.ParameterTwoRecord.FormKey.IDString()}";
                    }
                    else
                    {
                        //Console.WriteLine(conditionData.ParameterTwoNumber);
                        //Console.WriteLine(conditionData.ParameterTwoString);
                    }
                    condition.Value = $"{conditionFloat.ComparisonValue}";
                    perk.Conditions.Add(condition);
                }
            }
            tree.Perks.Add(perk);
        }

        public static void ReadNodes(IPatcherState<ISkyrimMod, ISkyrimModGetter> state, SynASTF.NetScriptFramework.ConfigFile cv, SkillTree tree, string NodeID, List<string> CompletedLinks)
        {
            string Node = $"Node{NodeID}";
            SkillTreePerk perk = new();
            uint formID = uint.Parse(cv.Entries[$"{Node}.PerkId"].Substring(2), System.Globalization.NumberStyles.HexNumber);
            FormKey key = new FormKey(cv.Entries[$"{Node}.PerkFile"], formID);
            var PerkForm = GetPerkFromFile(state, key);
            perk.Perk = $"__formData|{PerkForm.FormKey.ModKey.FileName}|0x{PerkForm.FormKey.IDString()}";
            perk.Conditions = new();
            perk.Name = PerkForm.Name?.String ?? "";
            var msg = state.PatchMod.Messages.AddNew();
            msg.Description = PerkForm.Description?.String ?? ""; ;
            msg.INAM = new MemorySlice<byte>(new byte[4]);
            msg.MenuButtons.Add(new MessageButton()
            {
                Text = "Yes"
            });
            msg.MenuButtons.Add(new MessageButton()
            {
                Text = "No"
            });
            msg.Flags = Message.Flag.MessageBox;
            msg.Name = $"Take This Perk {PerkForm.Name?.String}?";
            msg.EditorID = PerkForm.EditorID + "_TAKE";
            perk.Description = $"__formData|{msg.FormKey.ModKey.FileName}|0x{msg.FormKey.IDString()}";
            foreach (var cond in PerkForm.Conditions)
            {
                if (cond.GetType().ToString() == "Mutagen.Bethesda.Skyrim.Internals.ConditionFloatBinaryOverlay")
                {
                    IConditionFloatGetter conditionFloat = (IConditionFloatGetter)cond.DeepCopy();
                    IFunctionConditionDataGetter conditionData = (IFunctionConditionDataGetter)conditionFloat.Data;
                    var condition = new SkillCondition();
                    condition.Comparison = cond.CompareOperator.ToString();
                    condition.Function = conditionData.Function.ToString();
                    if (!conditionData.ParameterOneRecord.IsNull)
                    {
                        condition.Arg1 = $"__formData|{conditionData.ParameterOneRecord.FormKey.ModKey.FileName}|0x{conditionData.ParameterOneRecord.FormKey.IDString()}";
                        if (condition.Function == "GetGlobalValue" && cond.CompareOperator == CompareOperator.GreaterThanOrEqualTo)
                        {
                            msg.Description += $"\nREQUIRES {conditionFloat.ComparisonValue} {conditionData.ParameterOneRecord.Resolve(state.LinkCache).EditorID}";
                        }
                    }
                    else
                    {
                        //Console.WriteLine(conditionData.ParameterOneNumber);
                        //Console.WriteLine(conditionData.ParameterOneString);
                    }
                    if (!conditionData.ParameterTwoRecord.IsNull)
                    {
                        condition.Arg2 = $"__formData|{conditionData.ParameterTwoRecord.FormKey.ModKey.FileName}|0x{conditionData.ParameterTwoRecord.FormKey.IDString()}";
                    }
                    else
                    {
                        //Console.WriteLine(conditionData.ParameterTwoNumber);
                        //Console.WriteLine(conditionData.ParameterTwoString);
                    }
                    condition.Value = $"{conditionFloat.ComparisonValue}";
                    perk.Conditions.Add(condition);
                }
            }
            tree.Perks.Add(perk);
            CompletedLinks.Add(NodeID);
            if(!PerkForm.NextPerk.IsNull) {
                ReadPerk(state, PerkForm.NextPerk, tree);
            }
            if (cv.Entries.ContainsKey($"{Node}.Links"))
            {
                if (cv.Entries.GetValueOrDefault($"{Node}.Links") != "")
                {
                    foreach (var node in (cv.Entries?.GetValueOrDefault($"{Node}.Links") ?? "").Split(" "))
                    {
                        if (node != NodeID && !CompletedLinks.Contains(node))
                        {
                            ReadNodes(state, cv, tree, node, CompletedLinks);
                        }
                    }
                }
            }
        }

        public static void ReadNode0(IPatcherState<ISkyrimMod, ISkyrimModGetter> state, SynASTF.NetScriptFramework.ConfigFile cv, SkillTree tree, List<string> CompletedLinks)
        {
            string Node = $"Node0";
            if (cv.Entries.GetValueOrDefault($"{Node}.Links") != "")
            {
                foreach (var node in cv.Entries[$"{Node}.Links"].Replace(",", "").Split(" "))
                {
                    ReadNodes(state, cv, tree, node, CompletedLinks);
                }
            }
        }

        public static SkillTree ReadConfigFile(IPatcherState<ISkyrimMod, ISkyrimModGetter> state, string Path)
        {
            var cv = new SynASTF.NetScriptFramework.ConfigFile(Path);
            cv.Load();
            SkillTree tree = new();
            tree.Perks = new();
            tree.Description = cv.Entries?.GetValueOrDefault("Description") ?? "";
            tree.Name = cv.Entries?.GetValueOrDefault("Name") ?? "";
            List<string> CompletedLinks = new();
            if (cv.Entries?.GetValueOrDefault("LegendaryFile") != "")
            {
                tree.Legendary = TypedMethod.GLOB;
                tree.LegendaryGLOB = $"__formData|{cv.Entries?.GetValueOrDefault("LegendaryFile") ?? ""}|{cv.Entries?.GetValueOrDefault("LegendaryId") ?? ""}";
            }
            else
            {
                tree.Legendary = TypedMethod.NONE;
                tree.LegendaryGLOB = "";
            }
            if (cv.Entries?.GetValueOrDefault("PerkPointsFile") != "")
            {
                tree.PerkPoints = TypedMethod.GLOB;
                tree.PerkPointsGLOB = $"__formData|{cv.Entries?.GetValueOrDefault("PerkPointsFile") ?? "Skyrim.esm"}|{cv.Entries?.GetValueOrDefault("PerkPointsId") ?? "646"}";
            }
            else
            {
                tree.PerkPoints = TypedMethod.AV;
                tree.PerkPointsGLOB = "";
            }
            if (cv.Entries?.GetValueOrDefault("LevelFile") != "")
            {
                tree.Level = TypedMethod.GLOB;
                tree.LevelGLOB = $"__formData|{cv.Entries?.GetValueOrDefault("LevelFile") ?? ""}|{cv.Entries?.GetValueOrDefault("LevelId") ?? ""}";
            }
            ReadNode0(state, cv, tree, CompletedLinks);
            return tree;
        }

        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            var files = Directory.GetFiles(Path.Combine(state.DataFolderPath, "NetScriptFramework", "Plugins")).Where(x => x.EndsWith(".config.txt", true, System.Globalization.CultureInfo.InvariantCulture)).ToList();
            foreach (var file in files)
            {
                var filePath = Path.Combine(state.DataFolderPath, "NetScriptFramework", "Plugins", file);
                Console.WriteLine(filePath);
                SkillTree tree = ReadConfigFile(state, filePath);
                File.WriteAllText(Path.Combine(state.DataFolderPath, "ACSF", $"{tree.Name}.json"), JsonConvert.SerializeObject(tree));
            }
        }
    }
}