﻿using System;
using System.Collections.Generic;
using System.Text;
using RogueElements;
using RogueEssence.LevelGen;
using RogueEssence.Data;
using RogueEssence;
using System.Diagnostics;
using RogueEssence.Dungeon;
using Microsoft.Win32;

namespace MapGenTest
{
    public static class Example
    {
        static Dictionary<int, ZoneData> loadedZones;

        private static ZoneData getCachedZone(int zoneIndex)
        {
            ZoneData zone;
            if (loadedZones.TryGetValue(zoneIndex, out zone))
                return zone;
            zone = DataManager.Instance.GetZone(zoneIndex);
            loadedZones[zoneIndex] = zone;
            return zone;
        }

        public static void Run()
        {
            loadedZones = new Dictionary<int, ZoneData>();
            try
            {
                List<string> zoneNames = new List<string>();
                for (int ii = 0; ii < DataManager.Instance.DataIndices[DataManager.DataType.Zone].Count; ii++)
                    zoneNames.Add(DataManager.Instance.DataIndices[DataManager.DataType.Zone].Entries[ii].Name.ToLocal());

                string state = "Zones";
                while (true)
                {
                    Console.Clear();
                    Console.WriteLine(state);
                    Console.WriteLine("Choose a zone|ESC=Exit|F2=Stress Test");

                    int longestWidth = 0;
                    for (int ii = 0; ii < zoneNames.Count; ii++)
                    {
                        string label = GetSelectionString(ii, zoneNames[ii]);
                        if (label.Length > longestWidth)
                            longestWidth = label.Length;
                    }
                    int cols = Math.Min(3, (Console.WindowWidth - 1) / longestWidth + 1);
                    int rows = Math.Max(Math.Min(12, zoneNames.Count), (zoneNames.Count - 1) / cols + 1);

                    for (int ii = 0; ii < rows; ii++)
                    {
                        string choiceStr = "";
                        List<string> choiceList = new List<string>();
                        for (int jj = 0; jj < cols; jj++)
                        {
                            int index = ii + rows * jj;
                            if (index < zoneNames.Count)
                            {
                                choiceStr += "{" + jj + "," + "-" + longestWidth + "}  ";
                                choiceList.Add(GetSelectionString(index, zoneNames[index]));
                            }
                        }
                        Console.WriteLine(String.Format(choiceStr, choiceList.ToArray()));
                    }

                    int zoneIndex = (int)Registry.GetValue(DiagManager.REG_PATH, "ZoneChoice", -1);
                    if (zoneIndex == -1)
                    {
                        ConsoleKeyInfo key = Console.ReadKey();
                        if (key.Key == ConsoleKey.Escape)
                            break;
                        else if (key.Key == ConsoleKey.F2)
                        {
                            while (true)
                            {
                                Console.Clear();
                                Console.WriteLine(state + ">Bulk Gen");
                                Console.WriteLine("Specify amount to bulk gen");
                                int amt = GetInt(false);
                                if (amt > -1)
                                {
                                    Console.WriteLine("Generating all zones " + amt + " times.");
                                    StressTestAll(amt);
                                    ConsoleKeyInfo afterKey = Console.ReadKey();
                                    if (afterKey.Key == ConsoleKey.Escape)
                                        break;
                                }
                                else if (amt == -1)
                                    break;
                            }
                        }

                        if (key.KeyChar >= '0' && key.KeyChar <= '9')
                            zoneIndex = key.KeyChar - '0';
                        if (key.KeyChar >= 'a' && key.KeyChar <= 'z')
                            zoneIndex = key.KeyChar - 'a' + 10;
                    }
                    if (zoneIndex > -1 && zoneIndex < zoneNames.Count)
                    {
                        Registry.SetValue(DiagManager.REG_PATH, "ZoneChoice", zoneIndex);
                        StructureMenu(state, zoneIndex, getCachedZone(zoneIndex));
                        Registry.SetValue(DiagManager.REG_PATH, "ZoneChoice", -1);
                    }
                }

            }
            catch (Exception ex)
            {
                PrintError(ex);
                Registry.SetValue(DiagManager.REG_PATH, "SeedChoice", "");
                Registry.SetValue(DiagManager.REG_PATH, "FloorChoice", -1);
                Registry.SetValue(DiagManager.REG_PATH, "StructChoice", -1);
                Registry.SetValue(DiagManager.REG_PATH, "ZoneChoice", -1);
                Console.ReadKey();
            }
        }

        public static string GetSelectionString(int index, string str)
        {
            char select = (char)(index > 9 ? 'A' + index - 10 : '0' + index);
            return select.ToString() + ") " + str;
        }

        public static void StructureMenu(string prevState, int zoneIndex, ZoneData zone)
        {
            try
            {
                string state = prevState + ">" + zoneIndex + ": " + zone.Name.DefaultText;
                while (true)
                {
                    Console.Clear();
                    Console.WriteLine(state);
                    Console.WriteLine("Choose a structure|ESC=Back|F2=Stress Test");

                    int longestWidth = 0;
                    for (int ii = 0; ii < zone.Structures.Count; ii++)
                    {
                        string label = GetSelectionString(ii, zone.Structures[ii].FloorCount + " Floors");
                        if (label.Length > longestWidth)
                            longestWidth = label.Length;
                    }
                    int cols = Math.Min(3, (Console.WindowWidth - 1) / longestWidth + 1);
                    int rows = Math.Max(Math.Min(12, zone.Structures.Count), (zone.Structures.Count - 1) / cols + 1);

                    for (int ii = 0; ii < rows; ii++)
                    {
                        string choiceStr = "";
                        List<string> choiceList = new List<string>();
                        for (int jj = 0; jj < cols; jj++)
                        {
                            int index = ii + rows * jj;
                            if (index < zone.Structures.Count)
                            {
                                choiceStr += "{" + jj + "," + "-" + longestWidth + "}";
                                choiceList.Add(GetSelectionString(index, zone.Structures[index].FloorCount + " Floors"));
                            }
                        }
                        Console.WriteLine(String.Format(choiceStr, choiceList.ToArray()));
                    }

                    int structureIndex = (int)Registry.GetValue(DiagManager.REG_PATH, "StructChoice", -1);
                    if (structureIndex == -1)
                    {
                        ConsoleKeyInfo key = Console.ReadKey();
                        if (key.Key == ConsoleKey.Escape)
                        {
                            Registry.SetValue(DiagManager.REG_PATH, "StructChoice", -1);
                            break;
                        }
                        else if (key.Key == ConsoleKey.F2)
                        {
                            while (true)
                            {
                                Console.Clear();
                                Console.WriteLine(state + ">Bulk Gen");
                                Console.WriteLine("Specify amount to bulk gen");
                                int amt = GetInt(false);
                                if (amt > -1)
                                {
                                    Console.WriteLine("Generating zone " + amt + " times.");
                                    StressTestZone(zone, amt);
                                    ConsoleKeyInfo afterKey = Console.ReadKey();
                                    if (afterKey.Key == ConsoleKey.Escape)
                                        break;
                                }
                                else if (amt == -1)
                                    break;
                            }
                        }

                        if (key.KeyChar >= '0' && key.KeyChar <= '9')
                            structureIndex = key.KeyChar - '0';
                        if (key.KeyChar >= 'a' && key.KeyChar <= 'z')
                            structureIndex = key.KeyChar - 'a' + 10;
                    }
                    if (structureIndex > -1 && structureIndex < zone.Structures.Count)
                    {
                        Registry.SetValue(DiagManager.REG_PATH, "StructChoice", structureIndex);
                        FloorMenu(state, structureIndex, zone.Structures[structureIndex]);
                        Registry.SetValue(DiagManager.REG_PATH, "StructChoice", -1);
                    }
                }
            }
            catch (Exception ex)
            {
                DiagManager.Instance.LogInfo("ERROR at Zone " + zoneIndex);
                PrintError(ex);
                Registry.SetValue(DiagManager.REG_PATH, "SeedChoice", "");
                Registry.SetValue(DiagManager.REG_PATH, "FloorChoice", -1);
                Registry.SetValue(DiagManager.REG_PATH, "StructChoice", -1);
                Console.ReadKey();
            }
        }

        public static void FloorMenu(string prevState, int structureIndex, ZoneSegmentBase structure)
        {
            try
            {
                string state = prevState + ">Structure " + structureIndex;
                while (true)
                {
                    Console.Clear();
                    Console.WriteLine(state);
                    Console.WriteLine("Choose a Floor: 0-{0}|ESC=Back|F2=Stress Test", (structure.FloorCount - 1).ToString());

                    int floorNum = (int)Registry.GetValue(DiagManager.REG_PATH, "FloorChoice", -1);
                    if (floorNum == -1)
                    {
                        floorNum = GetInt(true);
                        if (floorNum == -1)
                        {
                            Registry.SetValue(DiagManager.REG_PATH, "FloorChoice", -1);
                            break;
                        }
                        else if (floorNum == -2)
                        {
                            while (true)
                            {
                                Console.Clear();
                                Console.WriteLine(state + ">Bulk Gen");
                                Console.WriteLine("Specify amount to bulk gen");
                                int amt = GetInt(false);
                                if (amt > -1)
                                {
                                    Console.WriteLine("Generating structure " + amt + " times.");
                                    StressTestStructure(structure, amt);
                                    ConsoleKeyInfo afterKey = Console.ReadKey();
                                    if (afterKey.Key == ConsoleKey.Escape)
                                        break;
                                }
                                else if (amt == -1)
                                    break;
                            }
                        }
                    }
                    //TODO: map the floor number to map id
                    if (floorNum > -1 && floorNum < structure.FloorCount)
                    {
                        Registry.SetValue(DiagManager.REG_PATH, "FloorChoice", floorNum);
                        MapMenu(state, structure, floorNum);
                        Registry.SetValue(DiagManager.REG_PATH, "FloorChoice", -1);
                    }
                }

            }
            catch (Exception ex)
            {
                DiagManager.Instance.LogInfo("ERROR at Struct " + structureIndex);
                PrintError(ex);
                Registry.SetValue(DiagManager.REG_PATH, "SeedChoice", "");
                Registry.SetValue(DiagManager.REG_PATH, "FloorChoice", -1);
                Console.ReadKey();
            }
        }


        public static void MapMenu(string prevState, ZoneSegmentBase structure, int floorIndex)
        {
            ulong structSeed = MathUtils.Rand.NextUInt64();
            try
            {
                ulong newSeed;
                if (UInt64.TryParse((string)Registry.GetValue(DiagManager.REG_PATH, "SeedChoice", ""), out newSeed))
                    structSeed = newSeed;

                Registry.SetValue(DiagManager.REG_PATH, "SeedChoice", structSeed.ToString());

                while (true)
                {
                    Console.Clear();

                    ConsoleKey key = ConsoleKey.Enter;
                    string state = prevState + ">" + floorIndex + ": ";
                    bool threwException = false;
                    try
                    {

                        ReRandom structRand = new ReRandom(structSeed);
                        for (int ii = 0; ii < floorIndex; ii++)
                            structRand.NextUInt64();

                        ulong mapSeed = structRand.NextUInt64();

                        //load the struct context
                        ReRandom initRand = new ReRandom(structSeed);
                        ZoneGenContext zoneContext = new ZoneGenContext();
                        foreach (ZonePostProc zoneStep in structure.PostProcessingSteps)
                        {
                            ZonePostProc newStep = zoneStep.Instantiate(initRand.NextUInt64());
                            zoneContext.ZoneSteps.Add(newStep);
                        }

                        zoneContext.CurrentID = floorIndex;
                        zoneContext.Seed = mapSeed;


                        IGenContext context = structure.GetMap(zoneContext);

                        ExampleDebug.SteppingIn = false;

                        BaseMapGenContext stairsMap = context as BaseMapGenContext;
                        state += stairsMap.Map.Name.DefaultText.Replace('\n', ' ');
                        string seedMsg = "SSeed: " + structSeed + "    MSeed: " + mapSeed;
                        //Console.WriteLine(state);

                        key = ExampleDebug.PrintTiles(context, state + "\n" + "Arrow Keys=Navigate|Enter=Retry|ESC=Back|F2=Stress Test|F3=Custom Seed|F4=Step In" + "\n" + seedMsg, true, true, true);


                    }
                    catch (Exception ex)
                    {
                        DiagManager.Instance.LogInfo("ERROR at F" + floorIndex + " SEED:" + structSeed);
                        PrintError(ex);
                        Console.WriteLine("Press Enter to retry error scenario.");
                        key = Console.ReadKey().Key;
                        threwException = true;
                    }


                    if (key == ConsoleKey.Escape)
                    {
                        Registry.SetValue(DiagManager.REG_PATH, "SeedChoice", "");
                        break;
                    }
                    else if (key == ConsoleKey.F2)
                    {
                        while (true)
                        {
                            Console.Clear();
                            Console.WriteLine(state + ">Bulk Gen");
                            Console.WriteLine("Specify amount to bulk gen");
                            int amt = GetInt(false);
                            if (amt > -1)
                            {
                                Console.WriteLine("Generating floor " + amt + " times.");
                                StressTestFloor(structure, floorIndex, amt);
                                ConsoleKeyInfo afterKey = Console.ReadKey();
                                if (afterKey.Key == ConsoleKey.Escape)
                                    break;
                            }
                            else if (amt == -1)
                                break;
                        }
                    }
                    else if (key == ConsoleKey.F3)
                    {
                        Console.Clear();
                        Console.WriteLine(state + ">Custom Seed");
                        Console.WriteLine("Specify a STRUCT seed value");
                        string input = Console.ReadLine();
                        ulong customSeed;
                        if (UInt64.TryParse(input, out customSeed))
                            structSeed = customSeed;
                    }
                    else if (key == ConsoleKey.F4)
                    {
                        ExampleDebug.SteppingIn = true;
                    }
                    else if (key == ConsoleKey.Enter)
                    {
                        if (!threwException)
                            structSeed = MathUtils.Rand.NextUInt64();
                    }
                    Registry.SetValue(DiagManager.REG_PATH, "SeedChoice", structSeed.ToString());
                }
            }
            catch (Exception ex)
            {
                DiagManager.Instance.LogInfo("ERROR at F"+floorIndex+" SEED:" + structSeed);
                PrintError(ex);
                Registry.SetValue(DiagManager.REG_PATH, "SeedChoice", "");
                Console.ReadKey();
            }
        }

        public static int GetInt(bool includeAmt)
        {
            int result = 0;

            ConsoleKeyInfo key = Console.ReadKey(true);
            while(key.Key != ConsoleKey.Enter)
            {
                if (key.Key == ConsoleKey.Escape)
                    return -1;
                if (includeAmt && key.Key == ConsoleKey.F2)
                    return -2;

                if (key.KeyChar >= '0' && key.KeyChar <= '9')
                {
                    Console.Write(key.KeyChar);
                    result = result * 10 + key.KeyChar - '0';
                }
                else if (key.Key == ConsoleKey.Backspace)
                {
                    Console.Write("\b \b");
                    result = result / 10;
                }
                key = Console.ReadKey(true);
            }
            Console.WriteLine();
            return result;
        }

        public static void StressTestAll(int amount)
        {
            ExampleDebug.Printing = -1;
            int zoneIndex = 0;
            int structureIndex = 0;
            ulong structSeed = 0;
            int floor = 0;
            try
            {
                List<List<TimeSpan>> generationTimes = new List<List<TimeSpan>>();
                for (int ii = 0; ii < DataManager.Instance.DataIndices[DataManager.DataType.Zone].Count; ii++)
                    generationTimes.Add(new List<TimeSpan>());

                Stopwatch watch = new Stopwatch();

                for (int ii = 0; ii < amount; ii++)
                {
                    for (int kk = 0; kk < DataManager.Instance.DataIndices[DataManager.DataType.Zone].Count; kk++)
                    {
                        zoneIndex = kk;
                        ZoneData zone = getCachedZone(kk);

                        for (int nn = 0; nn < zone.Structures.Count; nn++)
                        {
                            structureIndex = nn;
                            ZoneSegmentBase structure = zone.Structures[nn];

                            structSeed = MathUtils.Rand.NextUInt64();
                            ReRandom structRand = new ReRandom(structSeed);

                            //load the struct context
                            ReRandom initRand = new ReRandom(structSeed);
                            ZoneGenContext zoneContext = new ZoneGenContext();
                            foreach (ZonePostProc zoneStep in structure.PostProcessingSteps)
                            {
                                ZonePostProc newStep = zoneStep.Instantiate(initRand.NextUInt64());
                                zoneContext.ZoneSteps.Add(newStep);
                            }

                            foreach (int floorId in structure.GetFloorIDs())
                            {
                                floor = floorId;
                                structRand.NextUInt64();
                                ulong mapSeed = structRand.NextUInt64();

                                zoneContext.CurrentID = floorId;
                                zoneContext.Seed = mapSeed;

                                TestFloor(watch, structure, zoneContext, null, null, generationTimes[kk]);
                            }
                        }
                    }
                }

                PrintTimeAnalysisTier2(generationTimes, "Z");
            }
            catch (Exception ex)
            {
                DiagManager.Instance.LogInfo("ERROR at Z"+zoneIndex+" S" + structureIndex + " F" + floor + " SSeed:" + structSeed);
                PrintError(ex);
            }
            finally
            {
                ExampleDebug.Printing = 0;
            }
        }

        public static void StressTestZone(ZoneData zone, int amount)
        {
            ExampleDebug.Printing = -1;
            int structureIndex = 0;
            ulong structSeed = 0;
            int floor = 0;
            try
            {
                List<List<TimeSpan>> generationTimes = new List<List<TimeSpan>>();
                for (int ii = 0; ii < zone.Structures.Count; ii++)
                    generationTimes.Add(new List<TimeSpan>());

                Stopwatch watch = new Stopwatch();

                for (int ii = 0; ii < amount; ii++)
                {
                    for (int nn = 0; nn < zone.Structures.Count; nn++)
                    {
                        structureIndex = nn;
                        ZoneSegmentBase structure = zone.Structures[nn];

                        structSeed = MathUtils.Rand.NextUInt64();
                        ReRandom structRand = new ReRandom(structSeed);

                        //load the struct context
                        ReRandom initRand = new ReRandom(structSeed);
                        ZoneGenContext zoneContext = new ZoneGenContext();
                        foreach (ZonePostProc zoneStep in structure.PostProcessingSteps)
                        {
                            ZonePostProc newStep = zoneStep.Instantiate(initRand.NextUInt64());
                            zoneContext.ZoneSteps.Add(newStep);
                        }


                        for (int jj = 0; jj < structure.FloorCount; jj++)
                        {
                            floor = jj;
                            structRand.NextUInt64();
                            ulong mapSeed = structRand.NextUInt64();

                            zoneContext.CurrentID = jj;
                            zoneContext.Seed = mapSeed;

                            TestFloor(watch, structure, zoneContext, null, null, generationTimes[nn]);
                        }
                    }
                }

                PrintTimeAnalysisTier2(generationTimes, "S");
            }
            catch (Exception ex)
            {
                DiagManager.Instance.LogInfo("ERROR at S" + structureIndex + " F" + floor + " SSeed:" + structSeed);
                PrintError(ex);
            }
            finally
            {
                ExampleDebug.Printing = 0;
            }
        }


        public static void StressTestStructure(ZoneSegmentBase structure, int amount)
        {
            ExampleDebug.Printing = -1;
            ulong structSeed = 0;
            int floor = 0;
            try
            {
                List<Dictionary<int, int>> generatedItems = new List<Dictionary<int, int>>();
                List<Dictionary<int, int>> generatedEnemies = new List<Dictionary<int, int>>();
                List<List<TimeSpan>> generationTimes = new List<List<TimeSpan>>();
                for (int ii = 0; ii < structure.FloorCount; ii++)
                {
                    generatedItems.Add(new Dictionary<int, int>());
                    generatedEnemies.Add(new Dictionary<int, int>());
                    generationTimes.Add(new List<TimeSpan>());
                }

                Stopwatch watch = new Stopwatch();

                for (int ii = 0; ii < amount; ii++)
                {
                    structSeed = MathUtils.Rand.NextUInt64();
                    ReRandom structRand = new ReRandom(structSeed);

                    //load the struct context
                    ReRandom initRand = new ReRandom(structSeed);
                    ZoneGenContext zoneContext = new ZoneGenContext();
                    foreach (ZonePostProc zoneStep in structure.PostProcessingSteps)
                    {
                        ZonePostProc newStep = zoneStep.Instantiate(initRand.NextUInt64());
                        zoneContext.ZoneSteps.Add(newStep);
                    }


                    for (int jj = 0; jj < structure.FloorCount; jj++)
                    {
                        floor = jj;
                        structRand.NextUInt64();
                        ulong mapSeed = structRand.NextUInt64();

                        zoneContext.CurrentID = jj;
                        zoneContext.Seed = mapSeed;

                        TestFloor(watch, structure, zoneContext, generatedItems[jj], generatedEnemies[jj], generationTimes[jj]);
                    }
                }


                Dictionary<int, int> totalGeneratedItems = new Dictionary<int, int>();
                Dictionary<int, int> totalGeneratedEnemies = new Dictionary<int, int>();
                for (int ii = 0; ii < structure.FloorCount; ii++)
                {
                    DiagManager.Instance.LogInfo("F"+ii+":");
                    PrintContentAnalysis(generatedItems[ii], generatedEnemies[ii]);
                    
                    foreach(int key in generatedItems[ii].Keys)
                        MathUtils.AddToDictionary<int>(totalGeneratedItems, key, generatedItems[ii][key]);

                    foreach (int key in generatedEnemies[ii].Keys)
                        MathUtils.AddToDictionary<int>(totalGeneratedEnemies, key, generatedEnemies[ii][key]);
                }

                DiagManager.Instance.LogInfo("Overall:");
                PrintContentAnalysis(totalGeneratedItems, totalGeneratedEnemies);

                PrintTimeAnalysisTier2(generationTimes, "F");
            }
            catch (Exception ex)
            {
                DiagManager.Instance.LogInfo("ERROR at F" + floor + " SSeed:" + structSeed);
                PrintError(ex);
            }
            finally
            {
                ExampleDebug.Printing = 0;
            }
        }


        public static void StressTestFloor(ZoneSegmentBase structure, int floorIndex, int amount)
        {
            ExampleDebug.Printing = -1;
            ulong structSeed = 0;
            try
            {
                Dictionary<int, int> generatedItems = new Dictionary<int, int>();
                Dictionary<int, int> generatedEnemies = new Dictionary<int, int>();
                List<TimeSpan> generationTimes = new List<TimeSpan>();
                Stopwatch watch = new Stopwatch();

                for (int ii = 0; ii < amount; ii++)
                {
                    structSeed = MathUtils.Rand.NextUInt64();
                    ReRandom structRand = new ReRandom(structSeed);
                    for (int jj = 0; jj < floorIndex; jj++)
                        structRand.NextUInt64();

                    ulong mapSeed = structRand.NextUInt64();

                    //load the struct context
                    ReRandom initRand = new ReRandom(structSeed);
                    ZoneGenContext zoneContext = new ZoneGenContext();
                    foreach (ZonePostProc zoneStep in structure.PostProcessingSteps)
                    {
                        ZonePostProc newStep = zoneStep.Instantiate(initRand.NextUInt64());
                        zoneContext.ZoneSteps.Add(newStep);
                    }

                    zoneContext.CurrentID = floorIndex;
                    zoneContext.Seed = mapSeed;

                    TestFloor(watch, structure, zoneContext, generatedItems, generatedEnemies, generationTimes);

                }

                PrintContentAnalysis(generatedItems, generatedEnemies);

                PrintTimeAnalysis(generationTimes);
            }
            catch (Exception ex)
            {
                DiagManager.Instance.LogInfo("ERROR: " + structSeed);
                PrintError(ex);
            }
            finally
            {
                ExampleDebug.Printing = 0;
            }
        }

        public static void TestFloor(Stopwatch watch, ZoneSegmentBase structure, ZoneGenContext zoneContext, Dictionary<int, int> generatedItems, Dictionary<int, int> generatedEnemies, List<TimeSpan> generationTimes)
        {
            TimeSpan before = watch.Elapsed;
            watch.Start();
            IGenContext context = structure.GetMap(zoneContext);
            watch.Stop();
            TimeSpan diff = watch.Elapsed - before;
            generationTimes.Add(diff);


            BaseMapGenContext mapContext = context as BaseMapGenContext;
            if (generatedItems != null)
            {
                foreach (MapItem mapItem in mapContext.Map.Items)
                {
                    if (mapItem.IsMoney)
                    {
                        MathUtils.AddToDictionary<int>(generatedItems, -1, mapItem.Value);
                        MathUtils.AddToDictionary<int>(generatedItems, 0, 1);
                    }
                    else
                        MathUtils.AddToDictionary<int>(generatedItems, mapItem.Value, 1);
                }
            }
            if (generatedEnemies != null)
            {
                foreach (Team team in mapContext.Map.MapTeams)
                {
                    foreach (Character character in team.Players)
                        MathUtils.AddToDictionary<int>(generatedEnemies, character.BaseForm.Species, 1);
                }
            }
        }

        public static void PrintContentAnalysis(Dictionary<int, int> GeneratedItems, Dictionary<int, int> GeneratedEnemies)
        {
            StringBuilder finalString = new StringBuilder();

            finalString.Append(String.Format("Items:") + "\n");
            List<string> printout = new List<string>();
            int total = 0;
            foreach (int key in GeneratedItems.Keys)
            {
                if (key > -1)
                    total += GeneratedItems[key];
            }
            foreach (int key in GeneratedItems.Keys)
            {
                if (key > 0)
                {
                    ItemData entry = DataManager.Instance.GetItem(key);
                    printout.Add(String.Format("    {0:D5} {1:F5} #{2:0000} {3}", GeneratedItems[key], ((float)GeneratedItems[key] / total), key, entry.Name.DefaultText));
                }
                else if (key == 0)
                    printout.Add(String.Format("    {0:D5} {1:F5} {2}", GeneratedItems[key], ((float)GeneratedItems[key] / total), "Money Spawns"));
                else
                    finalString.Append(String.Format("Money: {0}", GeneratedItems[key]) + "\n");
            }
            printout.Sort();

            foreach (string print in printout)
                finalString.Append(print + "\n");
            finalString.Append("\n");

            finalString.Append("Species:" + "\n");
            foreach (int key in GeneratedEnemies.Keys)
            {
                MonsterData data = DataManager.Instance.GetMonster(key);
                finalString.Append(String.Format("    {0:D5} #{1:000} {2}", GeneratedEnemies[key], key, data.Name) + "\n");
            }
            finalString.Append("\n");
            //DiagManager.Instance.LogInfo(String.Format("Gen Logs Printed"));

            DiagManager.Instance.LogInfo(finalString.ToString());
        }

        public static void PrintTimeAnalysis(List<TimeSpan> generationTimes)
        {
            generationTimes.Sort();

            TimeSpan minTime = generationTimes[0];
            TimeSpan medTime = generationTimes[generationTimes.Count / 2];
            TimeSpan maxTime = generationTimes[generationTimes.Count - 1];

            Console.WriteLine("MIN: {0}    MED: {1}    MAX: {2}", minTime.ToString(), medTime.ToString(), maxTime.ToString());

            TimeSpan totalTime = new TimeSpan();
            for (int ii = 0; ii < generationTimes.Count; ii++)
                totalTime += generationTimes[ii];
            Console.WriteLine("Completed in {0}.  View debug log for more details.", totalTime);
        }

        public static void PrintTimeAnalysisTier2(List<List<TimeSpan>> generationTimes, string category)
        {
            List<TimeSpan> flatTimes = new List<TimeSpan>();
            for (int ii = 0; ii < generationTimes.Count; ii++)
            {
                if (generationTimes[ii].Count > 0)
                {
                    generationTimes[ii].Sort();

                    TimeSpan minTime = generationTimes[ii][0];
                    TimeSpan medTime = generationTimes[ii][generationTimes[ii].Count / 2];
                    TimeSpan maxTime = generationTimes[ii][generationTimes[ii].Count - 1];

                    DiagManager.Instance.LogInfo(String.Format("{3}{4:D3}    MIN: {0}    MED: {1}    MAX: {2}", minTime.ToString(), medTime.ToString(), maxTime.ToString(), category, ii));

                    flatTimes.AddRange(generationTimes[ii]);
                }
            }

            {
                flatTimes.Sort();

                TimeSpan minTime = flatTimes[0];
                TimeSpan medTime = flatTimes[flatTimes.Count / 2];
                TimeSpan maxTime = flatTimes[flatTimes.Count - 1];

                Console.WriteLine("ALL    MIN: {0}    MED: {1}    MAX: {2}", minTime.ToString(), medTime.ToString(), maxTime.ToString());

                TimeSpan totalTime = new TimeSpan();
                for (int ii = 0; ii < flatTimes.Count; ii++)
                    totalTime += flatTimes[ii];
                Console.WriteLine("Completed in {0}.  View debug log for more details.", totalTime);
            }
        }

        public static void PrintError(Exception ex)
        {
            Exception innerException = ex;
            int depth = 0;
            while (innerException != null)
            {
                Console.WriteLine("Exception Depth: " + depth);
                Console.WriteLine(innerException.ToString());
                Console.WriteLine();
                innerException = innerException.InnerException;
                depth++;
            }
        }


    }
}