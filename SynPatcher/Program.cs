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

using SynACSF.Structures;

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

        public static void GenPerk(IPatcherState<ISkyrimMod, ISkyrimModGetter> state, IPerkGetter PerkForm, SkillTree tree)
        {
            SkillTreePerk perk = new();
            perk.Perk = $"__formData|{PerkForm.FormKey.ModKey.FileName}|0x{PerkForm.FormKey.IDString()}";
            perk.Conditions = new();
            perk.Name = PerkForm.Name?.String ?? "";
            var msg = state.PatchMod.Messages.AddNew();
            msg.Description = PerkForm.Description?.String ?? ""; ;
            msg.INAM = new MemorySlice<byte>(new byte[4]);
            if (tree.PerkPoints == TypedMethod.GLOB)
            {
                msg.MenuButtons.Add(new MessageButton()
                {
                    Text = "Yes",
                    Conditions = PerkForm.Conditions.Select(x => x.DeepCopy()).Concat(new ExtendedList<Condition>() {
                        new ConditionFloat() {
                            CompareOperator = CompareOperator.GreaterThanOrEqualTo,
                            ComparisonValue = 1.0F,
                            Data = new GetGlobalValueConditionData() {
                                Reference = tree.PP_GV.ToLink(),
                            },
                        }
                    }).ToExtendedList(),
                });
            }
            else
            {
                //we can't get the perk point AV :/
                msg.MenuButtons.Add(new MessageButton()
                {
                    Text = "Yes",
                    Conditions = PerkForm.Conditions.Select(x => x.DeepCopy()).ToExtendedList()
                });
            }
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
                if (cond.GetType().ToString() == typeof(IConditionFloatGetter).ToString())
                {
                    Console.WriteLine(cond.GetType().ToString());
                    IConditionFloatGetter conditionFloat = (IConditionFloatGetter)cond.DeepCopy();
                    var condition = new SkillCondition();
                    condition.Comparison = cond.CompareOperator.ToString();
                    conditionFloat.Data.GetType();
                    Console.WriteLine(conditionFloat.Data.GetType().ToString());
                    if (conditionFloat.GetType().ToString() == typeof(GetGlobalValueConditionData).GetType().ToString())
                    {
                        var conditionData = (GetGlobalValueConditionData)conditionFloat.Data;
                        condition.Function = conditionFloat.CompareOperator.ToString();
                            condition.Arg1 = $"__formData|{conditionData.Global.Link.FormKey.ModKey.FileName}|0x{conditionData.Global.Link.FormKey.IDString()}";
                        condition.Value = $"{conditionFloat.ComparisonValue}";
                        perk.Conditions.Add(condition);
                    }
                }
            }
            tree.Perks.Add(perk);
            if (!PerkForm.NextPerk.IsNull)
            {
                GenPerk(state, PerkForm.NextPerk.Resolve<IPerkGetter>(state.LinkCache), tree);
            }
        }
        public static void ReadNodes(IPatcherState<ISkyrimMod, ISkyrimModGetter> state, SynACSF.NetScriptFramework.ConfigFile cv, SkillTree tree, string NodeID, List<string> CompletedLinks)
        {
            string Node = $"Node{NodeID}";
            uint formID = uint.Parse(cv.Entries[$"{Node}.PerkId"].Substring(2), System.Globalization.NumberStyles.HexNumber);
            FormKey key = new FormKey(cv.Entries[$"{Node}.PerkFile"], formID);
            IPerkGetter PerkForm = GetPerkFromFile(state, key);
            GenPerk(state, PerkForm, tree);
            CompletedLinks.Add(NodeID);
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

        public static void ReadNode0(IPatcherState<ISkyrimMod, ISkyrimModGetter> state, SynACSF.NetScriptFramework.ConfigFile cv, SkillTree tree, List<string> CompletedLinks)
        {
            string Node = $"Node0";
            if (!cv.Entries.GetValueOrDefault($"{Node}.Links").IsNullOrWhitespace())
            {
                foreach (var node in cv.Entries[$"{Node}.Links"].Replace(",", "").Split(" "))
                {
                    ReadNodes(state, cv, tree, node, CompletedLinks);
                }
            }
            else
            {
                throw new Exception("Non-Custom Skill Tree Framework Config");
            }
        }

        public static SkillTree ReadConfigFile(IPatcherState<ISkyrimMod, ISkyrimModGetter> state, string Path)
        {
            var cv = new SynACSF.NetScriptFramework.ConfigFile(Path);
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
                tree.PP_GV = state.LinkCache.Resolve<IGlobalGetter>(new FormKey(ModKey.FromFileName(cv.Entries?.GetValueOrDefault("PerkPointsFile") ?? ""), uint.Parse(cv.Entries?.GetValueOrDefault("PerkPointsId")?.Substring(2) ?? "", System.Globalization.NumberStyles.HexNumber)));
                tree.PerkPointsGLOB = $"__formData|{cv.Entries?.GetValueOrDefault("PerkPointsFile") ?? "Skyrim.esm"}|{cv.Entries?.GetValueOrDefault("PerkPointsId") ?? ""}";
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
                var gval = state.LinkCache.Resolve<IGlobalGetter>(new FormKey(ModKey.FromFileName(cv.Entries?.GetValueOrDefault("LevelFile") ?? ""), uint.Parse(cv.Entries?.GetValueOrDefault("LevelId")?.Substring(2) ?? "", System.Globalization.NumberStyles.HexNumber)));
                tree.StartingLevel = ((IGlobalShortGetter)gval)?.Data.ToString() ?? "0";
            }
            ReadNode0(state, cv, tree, CompletedLinks);
            return tree;
        }

        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            if (!Directory.Exists(Path.Combine(state.DataFolderPath, "NetScriptFramework", "Plugins")))
            {
                Console.WriteLine($"Folder for CSF Trees does not exist, this is caused by having no skill trees installed");
                Console.WriteLine($"YOU DO NOT NEED TO INSTALL CSF OR NETSCRIPTFRAMEWORK FOR THIS PATCHER!! THIS ERROR JUST INDICATES A DIRECTORY NOT EXISITNG!!");
                return;
            }
            var files = Directory.GetFiles(Path.Combine(state.DataFolderPath, "NetScriptFramework", "Plugins")).Where(x => x.StartsWith("CustomSkill.", true, System.Globalization.CultureInfo.InvariantCulture)).Where(x => x.EndsWith(".config.txt", true, System.Globalization.CultureInfo.InvariantCulture)).ToList();
            if (files.Count == 0)
            {
                Console.WriteLine($"No skill trees installed skipping patch");
                Console.WriteLine($"To use this patcher you MUST install a skill tree");
                return;
            }
            foreach (var file in files)
            {
                var filePath = Path.Combine(state.DataFolderPath, "NetScriptFramework", "Plugins", file);
                try
                {
                    Console.WriteLine(filePath);
                    SkillTree tree = ReadConfigFile(state, filePath);
                    File.WriteAllText(Path.Combine(state.DataFolderPath, "ACSF", $"{tree.Name}.json"), JsonConvert.SerializeObject(tree));
                }
                catch (Exception)
                {
                    Console.WriteLine("Found a Non-Skill Tree Framework config, ignoring: " + filePath);
                }
            }
        }
    }
}