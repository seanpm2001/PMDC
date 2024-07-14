﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using RogueElements;
using RogueEssence.Data;
using RogueEssence.Content;
using RogueEssence.Dungeon;
using RogueEssence.LevelGen;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Reflection;
using System.Linq;
using RogueEssence;
using RogueEssence.Dev;
using PMDC.LevelGen;
using System.Diagnostics;
using PMDC.Dungeon;
using RogueEssence.Script;
using System.Text;
using System.Text.RegularExpressions;

namespace PMDC.Dev
{

    public static class DevHelper
    {

        public delegate IEntryData GetNamedData(int ii);

        public static void AddWithEvos(Dictionary<MonsterID, HashSet<(string, ZoneLoc)>> found, MonsterID index, string tag, ZoneLoc encounter)
        {
            addMonster(found, index, tag, encounter);
            //also add all the evos
            foreach(MonsterID evo in findEvos(index, true))
                addMonster(found, evo, "EVOLVE", ZoneLoc.Invalid);
        }

        private static void addMonster(Dictionary<MonsterID, HashSet<(string, ZoneLoc)>> found, MonsterID index, string tag, ZoneLoc encounter)
        {
            if (!found.ContainsKey(index))
                found[index] = new HashSet<(string, ZoneLoc)>();
            found[index].Add((tag, encounter));
        }

        public static IEnumerable<MonsterID> FindMonFamily(string firstStage)
        {
            MonsterData data = DataManager.Instance.GetMonster(firstStage);
            string prevo = data.PromoteFrom;
            while (!String.IsNullOrEmpty(prevo))
            {
                firstStage = prevo;
                data = DataManager.Instance.GetMonster(firstStage);
                prevo = data.PromoteFrom;
            }
            MonsterData preData = DataManager.Instance.GetMonster(firstStage);
            for (int ii = 0; ii < preData.Forms.Count; ii++)
            {
                MonsterID preForm = new MonsterID(firstStage, ii, "", Gender.Unknown);
                yield return preForm;
                foreach (MonsterID evo in findEvos(preForm, false))
                    yield return evo;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="index"></param>
        /// <param name="encountersOnly">For use for encounter data only.</param>
        /// <returns></returns>
        private static IEnumerable<MonsterID> findEvos(MonsterID index, bool encountersOnly)
        {
            MonsterData data = DataManager.Instance.GetMonster(index.Species);
            foreach (PromoteBranch promote in data.Promotions)
            {
                MonsterData evoData = DataManager.Instance.GetMonster(promote.Result);
                for (int jj = 0; jj < evoData.Forms.Count; jj++)
                {
                    if ((!encountersOnly || evoData.Forms[jj].Released && !evoData.Forms[jj].Temporary) && evoData.Forms[jj].PromoteForm == index.Form)
                    {
                        MonsterID altForm = new MonsterID(promote.Result, jj, "", Gender.Unknown);
                        yield return altForm;

                        foreach (MonsterID evo in findEvos(altForm, encountersOnly))
                            yield return evo;
                    }
                }
            }
        }

        private static void extractMobSpawnFromObject(Dictionary<MonsterID, HashSet<(string, ZoneLoc)>> foundSpecies, object member, bool recruitableOnly, string tag, ZoneLoc encounter)
        {
            Type type = member.GetType();

            //members are set when control values are changed?
            try
            {
                if (type.IsEnum)
                {

                }
                else if (type == typeof(MobSpawn))
                {
                    MobSpawn spawn = (MobSpawn)member;
                    bool skip = false;
                    foreach (MobSpawnExtra feature in spawn.SpawnFeatures)
                    {
                        if (feature is MobSpawnUnrecruitable)
                        {
                            skip = true;
                            break;
                        }
                    }
                    if (!skip || !recruitableOnly)
                        AddWithEvos(foundSpecies, new MonsterID(spawn.BaseForm.Species, spawn.BaseForm.Form, "", Gender.Unknown), tag, encounter);
                }
                else if (type.IsArray)
                {
                    Array array = ((Array)member);
                    foreach (object val in array)
                        extractMobSpawnFromObject(foundSpecies, val, recruitableOnly, tag, encounter);
                }
                else if (type == typeof(SpreadVaultZoneStep))
                {
                    extractMobSpawnsFromClass(foundSpecies, member, recruitableOnly, "VAULT", encounter);
                }
                else if (type == typeof(SpreadHouseZoneStep))
                {
                    extractMobSpawnsFromClass(foundSpecies, member, recruitableOnly, "HOUSE", encounter);
                }
                else if (encounter.StructID.ID == -1 && type.GetInterfaces().Contains(typeof(ISpawnRangeList)))
                {
                    ISpawnRangeList enumerable = (ISpawnRangeList)member;
                    for (int nn = 0; nn < enumerable.Count; nn++)
                    {
                        object val = enumerable.GetSpawn(nn);
                        IntRange range = enumerable.GetSpawnRange(nn);
                        for (int kk = range.Min; kk < range.Max; kk++)
                        {
                            ZoneLoc newEnc = encounter;
                            newEnc.StructID.ID = kk;
                            extractMobSpawnFromObject(foundSpecies, val, recruitableOnly, tag, newEnc);
                        }
                    }
                }
                else if (type.GetInterfaces().Contains(typeof(IPriorityList)))
                {
                    IPriorityList enumerable = (IPriorityList)member;
                    foreach (Priority pri in enumerable.GetPriorities())
                    {
                        foreach(object val in enumerable.GetItems(pri))
                            extractMobSpawnFromObject(foundSpecies, val, recruitableOnly, tag, encounter);
                    }
                }
                else if (type.GetInterfaces().Contains(typeof(IEnumerable)))
                {
                    IEnumerable enumerable = (IEnumerable)member;
                    foreach (object val in enumerable)
                        extractMobSpawnFromObject(foundSpecies, val, recruitableOnly, tag, encounter);
                }
                else
                {
                    //members of the type
                    extractMobSpawnsFromClass(foundSpecies, member, recruitableOnly, tag, encounter);
                }
            }
            catch (Exception e)
            {
                DiagManager.Instance.LogError(e);
            }
        }

        private static void extractMobSpawnsFromClass(Dictionary<MonsterID, HashSet<(string, ZoneLoc)>> foundSpecies, object obj, bool recruitableOnly, string tag, ZoneLoc encounter)
        {
            //go through all members and add for them
            //control starts off clean; this is the control that will have all member controls on it
            try
            {
                Type type = obj.GetType();

                List<MemberInfo> myFields = type.GetEditableMembers();

                for (int ii = 0; ii < myFields.Count; ii++)
                {
                    if (myFields[ii].GetCustomAttributes(typeof(NonSerializedAttribute), false).Length > 0)
                        continue;

                    object member = myFields[ii].GetValue(obj);
                    if (member == null)
                        continue;

                    object memberObj = myFields[ii].GetValue(obj);
                    extractMobSpawnFromObject(foundSpecies, memberObj, recruitableOnly, tag, encounter);
                }
            }
            catch (Exception e)
            {
                DiagManager.Instance.LogError(e);
            }
        }

        public static Dictionary<MonsterID, HashSet<(string, ZoneLoc)>> GetAllAppearingMonsters(bool recruitableOnly)
        {
            //go through all dungeons
            //get all potential spawns in the dungeons (make it a table of bools mapping dex num to boolean)
            Dictionary<MonsterID, HashSet<(string, ZoneLoc)>> foundSpecies = new Dictionary<MonsterID, HashSet<(string, ZoneLoc)>>();
            
            //check all structures
            foreach(string zz in DataManager.Instance.DataIndices[DataManager.DataType.Zone].GetOrderedKeys(true))
            {
                if (zz == DataManager.Instance.DefaultZone)
                    continue;
                ZoneData mainZone = DataManager.Instance.GetZone(zz);
                if (!mainZone.Released)
                    continue;

                for (int ii = 0; ii < mainZone.Segments.Count; ii++)
                {
                    //check the postprocs for spawn-related classes
                    if (mainZone.Segments[ii] is LayeredSegment)
                    {
                        LayeredSegment structure = (LayeredSegment)mainZone.Segments[ii];
                        extractMobSpawnFromObject(foundSpecies, structure.ZoneSteps, recruitableOnly, "", new ZoneLoc(zz, new SegLoc(ii, -1)));
                        for (int jj = 0; jj < structure.Floors.Count; jj++)
                            extractMobSpawnFromObject(foundSpecies, structure.Floors[jj], recruitableOnly, "", new ZoneLoc(zz, new SegLoc(ii, jj)));
                    }
                    else
                        extractMobSpawnFromObject(foundSpecies, mainZone.Segments[ii], recruitableOnly, "", new ZoneLoc(zz, new SegLoc(ii, -1)));
                }
            }
            return foundSpecies;
        }


        /// <summary>
        /// Prints all the moves that can appear in the game due to the monsters appearing in the game, and alerts which unfinished ones need to be finished
        /// </summary>
        public static void PrintAllUnfinishedMoves()
        {
            //Dictionary<int, HashSet<(string, ZoneLoc)>> foundSpecies = GetAllAppearingMonsters(false);
            ////check against the regional dex for inconsistencies
            //for (int ii = 1; ii < DataManager.Instance.DataIndices[DataManager.DataType.Monster].Count; ii++)
            //{
            //    //bool hasDex = false;
            //    //for (int jj = 0; jj < MonsterInfo.DEX_MAP.Length; jj++)
            //    //{
            //    //    if (MonsterInfo.DEX_MAP[jj] == ii)
            //    //        hasDex = true;
            //    //}
            //    //if (hasDex != foundSpecies.ContainsKey(ii))
            //    //    Debug.WriteLine(String.Format("{0:D3}: Dex:{1} != Search:{2}", ii, hasDex, foundSpecies[ii]));
            //}

            ////get all learnable moves, keeping a hit count (make it a table mapping move index to number of distinct mons that learn this move)
            //List<string>[] moveHits = new List<string>[DataManager.Instance.DataIndices[DataManager.DataType.Skill].Count];
            //for (int ii = 0; ii < DataManager.Instance.DataIndices[DataManager.DataType.Skill].Count; ii++)
            //    moveHits[ii] = new List<string>();

            //for (int ii = 1; ii < DataManager.Instance.DataIndices[DataManager.DataType.Monster].Count; ii++)
            //{
            //    if (foundSpecies.ContainsKey(ii))
            //    {
            //        MonsterData data = DataManager.Instance.GetMonster(ii);
            //        for (int jj = 0; jj < data.Forms.Count; jj++)
            //        {
            //            BaseMonsterForm form = data.Forms[jj];
            //            for (int kk = 0; kk < form.LevelSkills.Count; kk++)
            //                moveHits[form.LevelSkills[kk].Skill].Add(form.FormName.ToLocal());
            //        }
            //    }
            //}
            //Debug.WriteLine("");
            ////go through the table, print the moves that are "unfinished"
            //for (int ii = 0; ii < moveHits.Length; ii++)
            //{
            //    SkillData skill = DataManager.Instance.GetSkill(ii);
            //    if (moveHits[ii].Count > 0 /*&& (skill.Name.DefaultText.StartsWith("-") || skill.Name.DefaultText.StartsWith("="))*/)
            //    {
            //        if (moveHits[ii].Count > 4)
            //            Debug.WriteLine(String.Format("{1}({0}):\t{2}", ii, skill.Name.ToLocal(), "x" + moveHits[ii].Count));
            //        else
            //        {
            //            string nameList = "";
            //            for (int jj = 0; jj < moveHits[ii].Count; jj++)
            //                nameList += (moveHits[ii][jj] + " ");
            //            Debug.WriteLine(String.Format("{1}({0}):\t{2}", ii, skill.Name.ToLocal(), nameList));
            //        }
            //    }
            //}
        }

        public static List<MonsterID> FindMoveAbilityUsers(string ability, string[] moves)
        {
            List<MonsterID> results = new List<MonsterID>();
            //go through entire dex
            foreach (string key in DataManager.Instance.DataIndices[DataManager.DataType.Monster].GetOrderedKeys(true))
            {
                MonsterData dex = DataManager.Instance.GetMonster(key);
                for (int jj = 0; jj < dex.Forms.Count; jj++)
                {
                    if (HasAbilityMoves(ability, moves, dex.Forms[jj]))
                        results.Add(new MonsterID(key, jj, DataManager.Instance.DefaultSkin, Gender.Genderless));
                }
            }

            return results;
        }

        public static bool HasAbilityMoves(string ability, string[] moves, BaseMonsterForm entry)
        {
            //check if form has ability given
            if (!String.IsNullOrEmpty(ability))
            {
                if (entry.Intrinsic1 != ability && entry.Intrinsic2 != ability && entry.Intrinsic3 != ability)
                    return false;
            }

            //check if form has all moves given
            foreach (string reqMove in moves)
            {
                bool foundMove = false;
                foreach (LevelUpSkill move in entry.LevelSkills)
                {
                    if (move.Skill == reqMove)
                    {
                        foundMove = true;
                        break;
                    }
                }
                if (!foundMove)
                    return false;
            }

            return true;
        }


        /// <summary>
        /// Gets the abilities from a text file in the path, and prints the monsters that are capable of each.
        /// </summary>
        /// <param name="path"></param>
        public static void PrintAbilityUsers(string path)
        {
            try
            {
                using (StreamReader s = new StreamReader(path))
                {
                    while (!s.EndOfStream)
                    {
                        string[] names = s.ReadLine().Split('/');
                        foreach (string name in names)
                        {
                            if (name == "")
                                continue;

                            string ability = "";
                            IntrinsicData entry = null;
                            foreach(string key in DataManager.Instance.DataIndices[DataManager.DataType.Intrinsic].GetOrderedKeys(true))
                            {
                                entry = DataManager.Instance.GetIntrinsic(key);
                                if (entry.Name.ToLocal().ToLower() == name.ToLower())
                                {
                                    ability = key;
                                    break;
                                }
                            }
                            if (ability == "")
                                throw new Exception("Unknown Ability");

                            Debug.WriteLine(String.Format("{0:D3}: {1}", ability, entry.Name.ToLocal()));
                            List<MonsterID> forms = FindMoveAbilityUsers(ability, new string[0]);
                            for (int ii = 0; ii < forms.Count; ii++)
                            {
                                MonsterData dex = DataManager.Instance.GetMonster(forms[ii].Species);
                                Debug.WriteLine(String.Format("    {0:D3}: {1} {2}", forms[ii].Species, dex.Name.ToLocal(),
                                    (dex.Forms[forms[ii].Form].FormName.DefaultText == dex.Name.DefaultText) ? "" : "(" + dex.Forms[forms[ii].Form].FormName.DefaultText + ")"));
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("The file could not be read:");
                Debug.WriteLine(e.Message);
            }
        }

        /// <summary>
        /// Gets the moves from a text file in the path, and prints the monsters that are capable of using each.
        /// </summary>
        /// <param name="path"></param>
        public static void PrintMoveUsers(string path)
        {
            //try
            //{
            //    using (StreamReader s = new StreamReader(path))
            //    {
            //        while (!s.EndOfStream)
            //        {
            //            string[] names = s.ReadLine().Split('/');
            //            foreach (string name in names)
            //            {
            //                if (name == "")
            //                    continue;

            //                int move = -1;
            //                SkillData entry = null;
            //                for (int ii = 0; ii < DataManager.Instance.DataIndices[DataManager.DataType.Skill].Count; ii++)
            //                {
            //                    entry = DataManager.Instance.GetSkill(ii);
            //                    if (entry.Name.DefaultText.ToLower() == name.ToLower())
            //                    {
            //                        move = ii;
            //                        break;
            //                    }
            //                }
            //                if (move == -1)
            //                    throw new Exception("Unknown Move");

            //                Debug.WriteLine(String.Format("{0:D3}: {1}", move, entry.Name.ToLocal()));
            //                List<MonsterID> forms = FindMoveAbilityUsers("", new int[1] { move });
            //                for (int ii = 0; ii < forms.Count; ii++)
            //                {
            //                    MonsterData dex = DataManager.Instance.GetMonster(forms[ii].Species);
            //                    Debug.WriteLine(String.Format("    {0:D3}: {1}  {2}", forms[ii].Species, dex.Name.ToLocal(),
            //                        (dex.Forms[forms[ii].Form].FormName.DefaultText == dex.Name.DefaultText) ? "" : "(" + dex.Forms[forms[ii].Form].FormName.ToLocal() + ")"));
            //                }
            //            }
            //        }
            //    }
            //}
            //catch (Exception e)
            //{
            //    Debug.WriteLine("The file could not be read:");
            //    Debug.WriteLine(e.Message);
            //}
        }


        private static void FindReplaceFiles(string path, bool recursive, string regexFrom, string replace)
        {
            foreach (string file in Directory.GetFiles(path, "*.lua"))
            {
                string inText = File.ReadAllText(file);
                string outText = Regex.Replace(inText, regexFrom, replace);
                File.WriteAllText(file, outText);
            }

            if (recursive)
            {
                foreach (string dir in Directory.GetDirectories(path))
                {
                    FindReplaceFiles(dir, recursive, regexFrom, replace);
                }
            }
        }

        public static void ConvertLua()
        {
            //check against v0.8.1 or lower
            string modulePath = Path.Join(PathMod.HardMod(PathMod.Quest.Path, LuaEngine.SCRIPT_PATH), PathMod.Quest.Namespace);
            if (!Directory.Exists(modulePath))
            //if (true)
            {
                //★ Lua scripts are moved to a subfolder based on the mod's namespace.
                Directory.Move(PathMod.HardMod(PathMod.Quest.Path, LuaEngine.SCRIPT_PATH), Path.Join(PathMod.HardMod(PathMod.Quest.Path, DataManager.DATA_PATH), "tmp"));
                Directory.CreateDirectory(PathMod.HardMod(PathMod.Quest.Path, LuaEngine.SCRIPT_PATH));
                Directory.Move(Path.Join(PathMod.HardMod(PathMod.Quest.Path, DataManager.DATA_PATH), "tmp"), modulePath);

                //delete unnecessary files
                if (Directory.Exists(Path.Join(modulePath, "bin")))
                    Directory.Delete(Path.Join(modulePath, "bin"), true);
                if (Directory.Exists(Path.Join(modulePath, "lib")))
                    Directory.Delete(Path.Join(modulePath, "lib"), true);
                if (File.Exists(Path.Join(modulePath, "debugger.lua")))
                    File.Delete(Path.Join(modulePath, "debugger.lua"));
                if (File.Exists(Path.Join(modulePath, "services", "baseservice.lua")))
                    File.Delete(Path.Join(modulePath, "services", "baseservice.lua"));

                //delete requires to lib
                FindReplaceFiles(modulePath, false, @".*require\s+'lib\.\w+'", "");

                //★ References to your own scripts require "yourscript" changed to include your namespace require "yournamespace.yourscript"

                //go through all base game script modules and create a list (manually.  this will be specified in hardcode)
                List<string> luaModules = new List<string>() { "common", "common_gen", "common_shop", "common_talk", "common_tutor", "common_vars",
                    "event", "event_battle", "event_mapgen", "event_misc", "event_single", "recruit_list", "queue", "services/baseservice",
                    "ai/base_state", "ai/ground_baseai", "ai/ground_default", "ai/ground_partner",
                    "menu/DescriptionSummary", "menu/InventorySelectMenu", "menu/juice/JuiceShopMenu", "menu/juice/SpecialtiesMenu",
                    "menu/skill/SkillSelectMenu", "menu/skill/SkillTutorMenu", "menu/team/AssemblySelectMenu", "menu/team/TeamSelectMenu"
                    };

                //remove the ones that are included in the mod to create a secondary list
                for (int ii = luaModules.Count - 1; ii >= 0; ii--)
                {
                    string existingModLua = LuaEngine.GetModulePath(modulePath, luaModules[ii]);
                    if (existingModLua != null)
                        luaModules.RemoveAt(ii);
                }

                //do a mass find/replace.  for each file...
                //change require "yourscript" to require "yournamespace.yourscript"
                FindReplaceFiles(modulePath, true, "require\\s+(['\"])", String.Format("require $1{0}.", PathMod.Quest.Namespace));
                //do it for AI too: AI:SetCharacterAI(partner, "origin.ai.ground_partner"
                FindReplaceFiles(modulePath, true, "AI:SetCharacterAI\\((.+?),\\s+(['\"])", String.Format("AI:SetCharacterAI($1, $2{0}.", PathMod.Quest.Namespace));

                //change require "yournamespace.yourscript" to "origin.yourscript" SPECIFICALLY for the scripts in the secondary list
                foreach (string luaPath in luaModules)
                {
                    string luaModulePath = luaPath.Replace("/", ".");
                    FindReplaceFiles(modulePath, true, String.Format("require\\s+(['\"]){0}\\.{1}(['\"])", PathMod.Quest.Namespace, luaPath), String.Format("require $1{0}.{1}$2", PathMod.BaseNamespace, luaModulePath));
                    FindReplaceFiles(modulePath, true, String.Format("AI:SetCharacterAI\\((.+?),\\s+(['\"]){0}\\.{1}(['\"])", PathMod.Quest.Namespace, luaPath), String.Format("AI:SetCharacterAI($1, $2{0}.{1}$3", PathMod.BaseNamespace, luaModulePath));
                    FindReplaceFiles(modulePath, true, String.Format("require\\s+(['\"]){0}\\.{1}(['\"])", PathMod.Quest.Namespace, luaModulePath), String.Format("require $1{0}.{1}$2", PathMod.BaseNamespace, luaModulePath));
                    FindReplaceFiles(modulePath, true, String.Format("AI:SetCharacterAI\\((.+?),\\s+(['\"]){0}\\.{1}(['\"])", PathMod.Quest.Namespace, luaModulePath), String.Format("AI:SetCharacterAI($1, $2{0}.{1}$3", PathMod.BaseNamespace, luaModulePath));
                }


                //★ References to require "basegamescript" become require "origin.basegamescript"
                //★ common and event scripts need their empty table declarations removed.
                FindReplaceFiles(modulePath, false, @"COMMON\s*=\s*{\s*}", "");
                FindReplaceFiles(modulePath, false, @"SINGLE_CHAR_SCRIPT\s*=\s*{\s*}", "");
                FindReplaceFiles(modulePath, false, @"BATTLE_SCRIPT\s*=\s*{\s*}", "");
                FindReplaceFiles(modulePath, false, @"STATUS_SCRIPT\s*=\s*{\s*}", "");
                FindReplaceFiles(modulePath, false, @"MAP_STATUS_SCRIPT\s*=\s*{\s*}", "");
                FindReplaceFiles(modulePath, false, @"ITEM_SCRIPT\s*=\s*{\s*}", "");
                FindReplaceFiles(modulePath, false, @"REFRESH_SCRIPT\s*=\s*{\s*}", "");
                FindReplaceFiles(modulePath, false, @"SKILL_CHANGE_SCRIPT\s*=\s*{\s*}", "");
                FindReplaceFiles(modulePath, false, @"GROUND_ITEM_EVENT_SCRIPT\s*=\s*{\s*}", "");
                FindReplaceFiles(modulePath, false, @"ZONE_GEN_SCRIPT\s*=\s*{\s*}", "");
                FindReplaceFiles(modulePath, false, @"FLOOR_GEN_SCRIPT\s*=\s*{\s*}", "");


                //★ Scripts modded from children of common.lua or event.lua must have a modded version of their parent with an include to the child.
                Dictionary<string, string> script_req = new Dictionary<string, string>();
                script_req["common_talk"] = "common";
                script_req["common_shop"] = "common";
                script_req["common_vars"] = "common";
                script_req["common_tutor"] = "common";
                script_req["event_single"] = "event";
                script_req["event_battle"] = "event";
                script_req["event_misc"] = "event";
                script_req["event_mapgen"] = "event";

                Dictionary<string, List<string>> create_files = new Dictionary<string, List<string>>();
                foreach (string key in script_req.Keys)
                {
                    string existingModLua = LuaEngine.GetModulePath(modulePath, key);
                    if (existingModLua != null)
                    {
                        if (!create_files.ContainsKey(script_req[key]))
                            create_files.Add(script_req[key], new List<string>());
                        create_files[script_req[key]].Add(key);
                    }
                }

                foreach (string create_file in create_files.Keys)
                {
                    string existingModLua = LuaEngine.GetModulePath(modulePath, create_file);
                    if (existingModLua == null)
                    {
                        StringBuilder requires = new StringBuilder();
                        foreach (string required_file in create_files[create_file])
                        {
                            requires.AppendLine(String.Format("require '{0}.{1}'", PathMod.Quest.Namespace, required_file));
                        }
                        File.WriteAllText(Path.Join(modulePath, create_file + ".lua"), requires.ToString());
                    }
                }

                //update MapStrings using find/replace
                //Remove variable declaration and initialization statements.
                FindReplaceFiles(Path.Join(modulePath, "ground"), true, @"local\s*MapStrings\s*=\s*{\s*}", "");
                FindReplaceFiles(Path.Join(modulePath, "ground"), true, @"\w+\s*=\s*COMMON\.AutoLoadLocalizedStrings\(\)", "");
                //Instead of MapStrings, use STRINGS.MapStrings
                FindReplaceFiles(Path.Join(modulePath, "ground"), true, @"MapStrings\[", "STRINGS.MapStrings[");

            }
        }

    }
}
