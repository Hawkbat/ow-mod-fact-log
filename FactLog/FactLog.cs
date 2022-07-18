using System;
using System.Linq;
using System.Collections.Generic;
using OWML.Common;
using OWML.ModHelper;
using UnityEngine;

namespace FactLog
{
    public class FactLog : ModBehaviour
    {
        const string FACT_LOG_FILE_SUFFIX = ".fact-log.json";
        const string FACT_LOG_VIEWER_SUFFIX = "_Fact_Log_Viewer.html";

        ShipLogManager shipLog;
        FactLogData factLogData;
        readonly List<FactLogDataEntry> newEntries = new List<FactLogDataEntry>();

        bool factLogOpen;
        readonly List<OWML.Common.Menus.IModButton> hiddenButtons = new List<OWML.Common.Menus.IModButton>();
        Vector2 scrollPosition;

        private void Start()
        {
            ModHelper.Console.WriteLine($"{nameof(FactLog)} is loaded!", MessageType.Success);

            LoadManager.OnCompleteSceneLoad += (scene, loadScene) =>
            {
                shipLog = null;
                if (loadScene != OWScene.SolarSystem) return;

                string profileName = StandaloneProfileManager.SharedInstance.currentProfile.profileName;

                shipLog = FindObjectOfType<ShipLogManager>();

                factLogData = null;

                string path = $"{ModHelper.Manifest.ModFolderPath}{profileName}{FACT_LOG_FILE_SUFFIX}";
                if (System.IO.File.Exists(path))
                {
                    try
                    {
                        var json = System.IO.File.ReadAllText(path);
                        factLogData = Newtonsoft.Json.JsonConvert.DeserializeObject<FactLogData>(json);
                    }
                    catch (Exception e)
                    {
                        ModHelper.Console.WriteLine(e.Message, MessageType.Error);
                    }
                }

                if (factLogData == null)
                {
                    factLogData = new FactLogData() { ProfileName = profileName, Entries = new List<FactLogDataEntry>() };
                    ModHelper.Storage.Save(factLogData, $"{profileName}{FACT_LOG_FILE_SUFFIX}");
                    ModHelper.Console.WriteLine($"Creating fresh fact log for profile {profileName}.", MessageType.Success);
                }
                else
                {
                    ModHelper.Console.WriteLine($"Loaded existing fact log for profile {profileName}.", MessageType.Success);
                }
            };

            ModHelper.Menus.PauseMenu.OnInit += PauseMenu_OnInit;
            ModHelper.Menus.PauseMenu.OnClosed += PauseMenu_OnClosed;
        }

        private void PauseMenu_OnInit()
        {
            var openFactLogBtn = ModHelper.Menus.PauseMenu.OptionsButton.Duplicate("OPEN FACT LOG");
            openFactLogBtn.OnClick += () =>
            {
                ToggleFactLog();
            };
        }

        private void PauseMenu_OnClosed()
        {
            if (factLogOpen) ToggleFactLog();
        }

        private void ToggleFactLog()
        {
            if (factLogOpen)
            {
                factLogOpen = false;
                foreach (var btn in hiddenButtons)
                {
                    btn.Show();
                }
                hiddenButtons.Clear();
            }
            else
            {
                factLogOpen = true;
                foreach (var btn in ModHelper.Menus.PauseMenu.Buttons)
                {
                    if (btn.Button.gameObject.activeSelf)
                    {
                        btn.Hide();
                        hiddenButtons.Add(btn);
                    }
                }
            }
        }

        private void Update()
        {
            if (!shipLog) return;

            if (factLogOpen && !ModHelper.Menus.PauseMenu.IsOpen) ToggleFactLog();

            string realTime = DateTime.Now.ToString();
            float loopTime = TimeLoop.GetFractionElapsed() + TimeLoop.GetLoopCount();

            foreach (var entry in shipLog.GetEntryList())
            {
                var facts = new List<ShipLogFact>();
                facts.AddRange(entry.GetRumorFacts());
                facts.AddRange(entry.GetExploreFacts());

                foreach (var fact in facts)
                {
                    if (!fact.IsRevealed()) continue;
                    if (!factLogData.Entries.Any(e => e.FactID == fact.GetID()))
                    {
                        newEntries.Add(new FactLogDataEntry()
                        {
                            FactID = fact.GetID(),
                            RealTime = realTime,
                            LoopTime = loopTime,
                            RevealOrder = fact.GetRevealOrder(),
                            Location = entry.GetName(false),
                            Fact = fact.GetText(),
                        });
                        ModHelper.Console.WriteLine($"Adding fact {fact.GetID()} to log.", MessageType.Info);
                    }
                }
            }

            if (newEntries.Count > 0)
            {
                newEntries.Sort((a, b) => a.RevealOrder - b.RevealOrder);
                factLogData.Entries.AddRange(newEntries);
                newEntries.Clear();
                ModHelper.Storage.Save(factLogData, factLogData.ProfileName + FACT_LOG_FILE_SUFFIX);
                ModHelper.Console.WriteLine($"Saved fact log for profile {factLogData.ProfileName}.", MessageType.Success);
            }
        }

        private void OnGUI()
        {
            if (factLogData == null || !factLogOpen) return;
            GUILayout.BeginVertical($"Fact Log - {factLogData.ProfileName}", "window");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Close"))
            {
                ToggleFactLog();
            }
            if (GUILayout.Button("Generate Shareable File"))
            {
                string html = $@"<!DOCTYPE html>
                <html>
                <head>
                    <title>Outer Wilds Fact Log - {factLogData.ProfileName}</title>
                    <style>
                        body {{
                            background: #081018;
                            font-family: monospace;
                            font-size: 16px;
                            color: white;
                        }}
                        table {{
                            table-layout: fixed;
                        }}
                        th {{
                            font-family: sans-serif;
                            color: #ff7f25;
                        }}
                        td {{
                            padding: 5px;
                        }}
                        td:not(:last-child) {{
                            white-space: nowrap;
                            text-align: right;
                        }}
                    </style>
                </head>
                <body>
                    <table>
                        <thead>
                            <tr>
                                <th>Real Time</th>
                                <th>Loop Time</th>
                                <th>Location</th>
                                <th>Fact</th>
                            </tr>
                        </thead>
                        <tbody>
                           {string.Join("\n", factLogData.Entries.Select(e => $"<tr><td>{e.RealTime}</td><td>{e.LoopTime}</td><td>{e.Location}</td><td>{e.Fact}</td></tr>"))}
                        </tbody>
                    </table>
                </body>
                </html>";
                System.IO.File.WriteAllText($"{ModHelper.Manifest.ModFolderPath}{factLogData.ProfileName}{FACT_LOG_VIEWER_SUFFIX}", html, System.Text.Encoding.UTF8);
                ModHelper.Console.WriteLine($"Regenerated fact log viewer for profile {factLogData.ProfileName}.", MessageType.Success);
                Application.OpenURL($"file://{ModHelper.Manifest.ModFolderPath}{factLogData.ProfileName}{FACT_LOG_VIEWER_SUFFIX}");
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            float widthRealTime = 150f;
            float widthLoopTime = 100f;
            float widthLocation = 150f;
            float widthFact = Screen.width - widthRealTime - widthLoopTime - widthLocation;

            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>Real Time</b>", GUILayout.Width(widthRealTime));
            GUILayout.Label("<b>Loop Time</b>", GUILayout.Width(widthLoopTime));
            GUILayout.Label("<b>Location</b>", GUILayout.Width(widthLocation));
            GUILayout.Label("<b>Fact</b>", GUILayout.Width(widthFact));
            GUILayout.EndHorizontal();
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            GUILayout.BeginVertical();
            foreach (var entry in factLogData.Entries)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(entry.RealTime, GUILayout.Width(widthRealTime));
                GUILayout.Label(entry.LoopTime.ToString(), GUILayout.Width(widthLoopTime));
                GUILayout.Label(entry.Location, GUILayout.Width(widthLocation));
                GUILayout.Label(entry.Fact, GUILayout.Width(widthFact - 60f));
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }
    }

    [Serializable]
    public class FactLogData
    {
        public string ProfileName;
        public List<FactLogDataEntry> Entries;
    }

    [Serializable]
    public class FactLogDataEntry
    {
        public string FactID;
        public string RealTime;
        public float LoopTime;
        public int RevealOrder;
        public string Location;
        public string Fact;
    }
}
