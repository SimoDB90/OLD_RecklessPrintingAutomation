using EmptyKeys.UserInterface.Generated.DataTemplatesContractsDataGrid_Bindings;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Lights;
using Sandbox.Game.WorldEnvironment.Modules;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.Entities.Blocks;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Input;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;
using VRageRender.Messages;
using static VRage.Game.MyObjectBuilder_BehaviorTreeDecoratorNode;
using static VRage.Game.MyObjectBuilder_Toolbar;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        readonly string stationVersion = "V: 4.0.0";
        const string lcd_changelog =
            "CHANGELOG VERSION 4.0.0 (11/10/2023):\n" +
            "-Fixed some initi bugs;\n" +
            "-Deleted \"Slow mode\";\n" +
            "-Huge rework of some logics of the scripts,\n" +
            "to slow them down when runtime exceeds thresholds;\n" +
            "-Added an automatic way to store some variables,\n" +
            " to calculate ETA, to persist through recompiles;" +
            "\n--------------------------------\n" +
            "CHANGELOG VERSION 3.6.0 (06/10/2023)\r\n" +
            "-Check for drone's initialization done\n" +
            "(otherwise, you can't start printing);\r\n" +
            "-Some checks here and there to avoid exceptions;\r\n" +
            "-Added some extra logs;\r\n" +
            "-Added the stop command from Drone too;\n" +
            "-Slow Mode improved;\n" +
            "-Added the max Runtime variable (for the server);\n" +
            "\n--------------------------------\n" +
            "CHANGELOG VERSION 3.5.4 (04/10/2023):\n" +
            "-Some minor bugs fix;\n" +
            "-Fix start -toggle command;" +
            "\n--------------------------------\n"
            ;

        string droneVersion;
        bool correctVersion = false;
        bool initializedRequired = true;
        bool setupAlreadySent = false;
        readonly MyIni _ini = new MyIni();
        readonly MyCommandLine _commandLine = new MyCommandLine();

        bool setupCompleted;
        readonly string BroadcastTag = "channel_1";
        IMyBroadcastListener _myBroadcastListener_station;
        // Wait variable
        double WaitingCustom;
        const double WaitingDefault = 7;

        const string TagDefault = "[RPA]";
        string TagCustom;

        readonly List<IMyShipWelder> WelderList = new List<IMyShipWelder>();
        readonly List<IMyTextPanel> LcdList = new List<IMyTextPanel>();

        //stuff for rotor control
        double farestWelderDistance = 0;
        IMyShipWelder farestWelder;
        double welderAngle = 0;//angle between projected farest welder and rotor
        int welderSign; //to check is parallel or antiparallel to forward or right
        bool welder_forward; //if welder is oriented as the rotor.Forward
        bool welder_right; // if welder is oriented as the rotor.right

        //stuff for Fancy Group
        readonly List<IMySoundBlock> FancySoundList = new List<IMySoundBlock>();
        readonly List<IMyTextPanel> FancyLCDList = new List<IMyTextPanel>();
        readonly List<IMyLightingBlock> FancyLightList = new List<IMyLightingBlock>();
        readonly List<IMyRadioAntenna> antennaList = new List<IMyRadioAntenna>();
        const string TAGFancy = "[RPA-Fancy]";
        IMySoundBlock soundBlock;
        IMyTextPanel LCDLog;
        IMyTextPanel LCDStatus;
        IMyTextPanel LCDActive;
        readonly float fontsize = 0.5f; // font of lcd panel
        const string logLCD = ".LOG";
        const string statusLCD = ".STATUS";
        const string activePrinterLCD = ".ACTIVE";
        bool statusLCDFound = false;
        bool activeLCDFound = false;
        bool activation = false; //received from the drone to turn on and off all stuff
        /// <Rotor
        readonly List<IMyMotorAdvancedStator> RotorList = new List<IMyMotorAdvancedStator>();
        IMyMotorAdvancedStator Rotor;
        IMyMotorAdvancedRotor rotorHead;

        //CD Variable Creation
        const float DynamicSpeedDefault = 25f;
        float DynamicSpeedCustom;
        const float RotorSpeedDefault = 4f;
        float RotorSpeedCustom;
        readonly float RotorTorqueValue = 40000000f;
        const double maxDistanceStopDefault = 150;
        double maxDistanceStopCustom;
        //max movement of the drone
        const double DroneMovDistanceDefault = 1.7;
        double DroneMovDistanceCustom;
        const double maxRTDefault = 0.5; //max runtime that server allows to have per player
        double maxRTCustom;
        // welde while moving bool
        const bool weldWhileMovingDefault = false;
        bool weldWhileMovingCustom;
        /// set the position of the rotor in order to retrieve the distance between the rotor and the tug to safety stop 
        MatrixD rotorMatrix = new MatrixD();
        //commands:
        //hudlcd
        const string defaultHudLcd = "hudlcd:.60:.99:0.65";
        const string defaultHudStatus = "hudlcd:-.99:.79:0.7";
        const string defaultHudActive = "hudlcd:-.2:.99:0.55";


        //Dictionary of actions used for parsing arguments in main--> see SetupActionDict()
        readonly Dictionary<string, Action> commandDict = new Dictionary<string, Action>();

        readonly string commands =
            lcd_divider + "\n" +
            $"[setup: send CustomData to Drone\n\n" +
            $"start x y z -toggle: start the process;\nadd name of blocks as arguments\nto ignore them during printing\nAdd -toggle if you want to toggle\n    whence finished;\n\n" +
            $"stop: stop the process;\n" +
            $"ignore_all: force the weld even\n with missing TB;\n\n" +
            $"ignore1: skip the active printed block\n\n" +
            $"init_d: read the CD of the drone\nand add the tag to the blocks" +
            lcd_divider + "\n" +
            $"Utility commands:\n" +
            lcd_divider + "\n" +
            $"guide -off: in depth commands\nAdd -off only to delete\nthe LCD from the guide\n\n" +
            $"align: force the tug to \nalign to the rotor\n\n" +
            $"projector: turn on/off the \nDrone's projector\n\n" +
            $"skip: force drone to move back;\n\n" +
            $"toggle x y ...: toggle on \nall blocks (no Epsteins or Tools);\nAdd x y ... to IGNORE THESE BLOCKS\n\n" +
            $"hudlcd:toggle -reset: toggle on/off\n the hudlcd.\nAdd -reset only to reset it;\n\n" +
            $"changelog -off: print the changelog on the STATUS LCD;\nAdd -off to delete it;\n\n" +
            lcd_divider + "\n" +
            $"Quality of Life Commands:\n" +
            lcd_divider + "\n" +
            $"rotor_ws x y: changes \nDynamiSpeed(RPM)-RotorSpeed(RPM);\n\n" +
            $"drone_move x: changes \nDroneMovementDistance(meters);\n\n" +
            $"max_distance x: changes \nMaxDistanceStop(meters);\n\n" +
            $"waiting x: changes the Wait;\n\n" +
            $"music -off: play a random music\nAdd -off if want music to stop\n\n" +
            $"untag_d x: where x is the tag you\nwant to remove from the drone;\n\n" +
            $"untag_s x: where x is the tag you\nwant to remove from the station;\n\n";

        readonly string compact_commands =
            lcd_divider + "\n" +
            $"[setup\n" +
            $"start x y ... -toggle\n" +
            $"stop\n" +
            $"ignore_all\n" +
            $"ignore1\n" +
            $"init_d]\n" +
            lcd_divider + "\n" +
            $"Utility commands:\n" +
            lcd_divider + "\n" +
            $"guide -off\n" +
            $"align\n" +
            $"projector\n" +
            $"skip\n" +
            $"toggle x y z ...\n" +
            $"hudlcd:toggle -reset\n" +
            $"changelog -off\n" +
            lcd_divider + "\n" +
            $"Quality of Life Commands:\n" +
            lcd_divider + "\n" +
            $"rotor_ws x y\n" +
            $"drone_move x\n" +
            $"max_distance x\n" +
            $"waiting x;\n" +
            $"music -off\n" +
            $"untag_d\n" +
            $"untag_s]";

        Color lcd_font_colour = new Color(30, 144, 255, 255);
        readonly string[] lcd_spinners = new string[] { "-", "\\", "|", "/" };
        double lcd_spinner_status;
        const string lcd_divider = "--------------------------------";
        const string lcd_title = "  RECKLESS PRINTING AUTOMATION  ";
        const string lcd_status_title = "     RPA Status Report      ";
        const string lcd_command_title = "COMMANDS GUIDE:";
        const string lcd_version = "RPA ";

        readonly string lcd_header;
        readonly string lcd_printing_version;
        readonly string header;
        string stuckStatus; //message for status of welder

        //debug LCD
        readonly List<IMyTextPanel> LCDDebug = new List<IMyTextPanel>();
        IMyTextPanel debug;
        const string defaultHudDebug = "hudlcd:-.69:.99:0.7";

        //immutable list builder for toggle arguments
        readonly ImmutableList<string>.Builder toggleBuilder = ImmutableList.CreateBuilder<string>();
        readonly ImmutableList<string>.Builder startBuilder = ImmutableList.CreateBuilder<string>();
        public Program()
        {
            lcd_printing_version = $"{lcd_version + stationVersion}";
            lcd_header = $"{lcd_divider}\n{lcd_title}\n{lcd_divider}";
            /////////////////////////
            ///Listener (Antenna Inter Grid Communication)
            _myBroadcastListener_station = IGC.RegisterBroadcastListener(BroadcastTag);
            _myBroadcastListener_station.SetMessageCallback(BroadcastTag);
            ////////////////////////////////////////
            header = HeaderCreation();
            CustomData();
            SetupBlocks();
            if (setupCompleted)
            {
                SetupActionDict(); // load the dictionary with actions for the commands of the main
                string output = CommandHeaderCreation();
                Echo(output);
                LCDLog.WriteText(output);
                Changelog();
            }
        }

        public void CustomData()
        {
            //get and set for customdata
            bool wasParsed = _ini.TryParse(Me.CustomData);
            TagCustom = _ini.Get("data", "TAG").ToString(TagDefault);
            weldWhileMovingCustom = _ini.Get("data", "WeldWhileMoving").ToBoolean(weldWhileMovingDefault);
            WaitingCustom = _ini.Get("data", "Wait").ToDouble(WaitingDefault);
            DroneMovDistanceCustom = _ini.Get("data", "DroneMovementDistance(meters)").ToDouble(DroneMovDistanceDefault);
            RotorSpeedCustom = _ini.Get("data", "RotorSpeed").ToSingle(RotorSpeedDefault);
            DynamicSpeedCustom = _ini.Get("data", "DynamicSpeed(RPM)").ToSingle(DynamicSpeedDefault);
            maxDistanceStopCustom = _ini.Get("data", "MaxDistanceStop(meters)").ToDouble(maxDistanceStopDefault);
            maxRTCustom = _ini.Get("data", "MaxServerRuntime(ms)").ToDouble(maxRTDefault);

            if (!wasParsed)
            {
                _ini.Clear();
            }
            // Set the values to make sure they exist. They could be missing even when parsed ok.
            _ini.Set("data", "TAG", TagCustom);
            _ini.Set("data", "WeldWhileMoving", weldWhileMovingCustom);
            _ini.Set("data", "Wait", WaitingCustom);
            _ini.Set("data", "DroneMovementDistance(meters)", DroneMovDistanceCustom);
            _ini.Set("data", "RotorSpeed", RotorSpeedCustom);
            _ini.Set("data", "DynamicSpeed(RPM)", DynamicSpeedCustom);
            _ini.Set("data", "MaxDistanceStop(meters)", maxDistanceStopCustom);
            _ini.Set("data", "MaxServerRuntime(ms)", maxRTCustom);

            Me.CustomData = _ini.ToString();
        }

        ///we create a key for the commandDict-->["key"] and a value as a method.
        ///Every method is created splitted. Thus when an argumebt in main is passed
        /// if match with key in dictionary, the method is invoked.
        /// 
        public void SetupActionDict()
        {
            commandDict["start"] = Start;
            commandDict["stop"] = Stop;
            commandDict["projector"] = Projector;
            commandDict["skip"] = Skip;
            commandDict["ignore1"] = IgnoreOne;
            commandDict["setup"] = Setup;
            commandDict["toggle"] = Toggle;
            commandDict["rotor_ws"] = Rotor_ws;
            commandDict["max_distance"] = Max_distance;
            commandDict["drone_move"] = Drone_move;
            commandDict["waiting"] = Waiting;
            commandDict["hudlcd:toggle"] = HUD;
            commandDict["guide"] = Guide;
            commandDict["align"] = Align;
            commandDict["music"] = Music;
            commandDict["changelog"] = Changelog;
            commandDict["ignore_all"] = IgnoreAll;
            commandDict["untag_d"] = UntagDrone;
            commandDict["untag_s"] = UntagStation;
            commandDict["init_d"] = InitDrone;
        }

        public void IgnoreAll()
        {
            if(correctVersion && setupAlreadySent && !initializedRequired)
            {
                IGC.SendBroadcastMessage(BroadcastTag, "ignore_all");
                Echo($"Sending message: ignore_all");
            }
            else if (!setupAlreadySent)
            {
                LCDLog.WriteText($"{header} \nRUN SETUP FIRST");
                DeactivateAll();
                return;
            }
            else if (!correctVersion)
            {
                LCDLog.WriteText($"{header} \nDrone script {droneVersion}\nStation script {stationVersion}\n{lcd_divider}\n" +
                            $"Different Version of scripts found,\n DONWLOAD THE UPDATED ONE");
                DeactivateAll();
                return;
            }
            else if (initializedRequired)
            {
                LCDLog.WriteText($"{header} \nDrone script {droneVersion}\nStation script {stationVersion}\n{lcd_divider}\n" +
                            $"Drone needs init: run \"init_d\"");
                DeactivateAll();
                return;
            }
        }
        public void InitDrone()
        {
            if(correctVersion)
            {
                IGC.SendBroadcastMessage(BroadcastTag, "init_d");
                LCDLog.WriteText($"{header} \nInit Drone: blocks tagged as drone's CD");
                Echo($"Sending message: init_d\n{commands}");
            }
            else
            {
                LCDLog.WriteText($"{header} \nDrone script {droneVersion}\nStation script {stationVersion}\n{lcd_divider}\n" +
                            $"Different Version of scripts found,\n DONWLOAD THE UPDATED ONE");
                DeactivateAll();
                return;
            }
        }
        public void Changelog()
        {
            bool changelogOFF = _commandLine.Switch("off");
            try
            {
                if (changelogOFF)
                {
                    LCDStatus.WriteText(CentreText(lcd_header, 32));
                }
                else
                {
                    string changelog = lcd_header + "\n" + lcd_printing_version + lcd_changelog;
                    LCDStatus.WriteText(CentreText(changelog, 32));
                }
            }
            catch { }
        }
        public void Music()
        {
            bool stopPlaying = _commandLine.Switch("off");

            try
            {
                if (stopPlaying)
                {
                    soundBlock.Stop();
                    soundBlock.Volume = 0;
                }
                else
                {
                    soundBlock.Stop();
                    soundBlock.Range = 100f;
                    List<string> allSounds = new List<string>();
                    List<string> wantedSounds = new List<string>();
                    string[] musicTracks = new string[] { "mystery", "calm", "build", "space", "Eros Music", "Expanse Theme" };
                    var randomMusic = new Random();
                    soundBlock.GetSounds(allSounds);
                    foreach (var m in musicTracks)
                    {
                        foreach (var s in allSounds)
                        {
                            if (s.ToLower().Contains(m))
                            {
                                wantedSounds.Add(s);
                            }
                        }
                    }
                    //Echo($"all: {allSounds.Count}\nwanted:{wantedSounds.Count}");
                    var myTrack = wantedSounds[randomMusic.Next(wantedSounds.Count)];
                    //Echo($"track:{myTrack}");
                    soundBlock.Enabled = true;
                    soundBlock.Volume = 1f;
                    soundBlock.SelectedSound = myTrack;
                    soundBlock.Play();
                }
            }
            catch { }
        }
        public void Guide()
        {
            bool resetGuide = _commandLine.Switch("off");
            if (resetGuide)
            {
                IGC.SendBroadcastMessage(BroadcastTag, "stop");
                LCDStatus.WriteText("");
            }
            else
            {
                IGC.SendBroadcastMessage(BroadcastTag, "stop");
                LCDStatus.WriteText("");
                GuideWriting($"Stopped the print to let you read the guide in peace.\n{commands}");
            }
        }
        public void HUD()
        {
            try
            {
                bool resetHUD = _commandLine.Switch("reset");
                //Echo($"reset? {resetHUD}");
                if (resetHUD)
                {
                    LCDLog.CustomData = defaultHudLcd;
                    LCDStatus.CustomData = defaultHudStatus;
                    LCDActive.CustomData = defaultHudActive;
                    Echo($"hudlcd set to default\n{commands}");
                    return;
                }
                else if (!resetHUD && LCDLog.CustomData.Contains("hudlcd"))
                {
                    var split = LCDLog.CustomData.Split(new char[] { ':' }, 2);
                    split[0] = "hudOFFlcd:";
                    LCDLog.CustomData = split[0] + split[1];
                    //Echo($"hud off {split[0]}\n{split[1]}");
                    Echo($"hud off\n{commands}");
                    //Status lcd
                }
                else if (!resetHUD && LCDLog.CustomData.Contains("hudOFFlcd"))
                {
                    var split = LCDLog.CustomData.Split(new char[] { ':' }, 2);
                    split[0] = "hudlcd:";
                    LCDLog.CustomData = split[0] + split[1];
                    //Echo($"hud on {split[0]}\n{split[1]}");
                    Echo($"hud on\n{commands}");
                }
                if (!resetHUD && LCDStatus.CustomData.Contains("hudlcd"))
                {
                    LCDStatus.CustomData = LCDStatus.CustomData.Replace("hudlcd", "hudOFFlcd");
                }
                else if (!resetHUD && LCDStatus.CustomData.Contains("hudOFFlcd"))
                {
                    LCDStatus.CustomData = LCDStatus.CustomData.Replace("hudOFFlcd", "hudlcd");
                }
                if (!resetHUD && LCDActive.CustomData.Contains("hudlcd"))
                {
                    LCDActive.CustomData = LCDActive.CustomData.Replace("hudlcd", "hudOFFlcd");
                }
                else if (!resetHUD && LCDActive.CustomData.Contains("hudOFFlcd"))
                {
                    LCDActive.CustomData = LCDActive.CustomData.Replace("hudOFFlcd", "hudlcd");
                }
            }
            catch
            {
            }
        }
        public void Start()
        {
            bool toggleAfterFinish = _commandLine.Switch("toggle");
            if (correctVersion && setupAlreadySent && !initializedRequired)
            {
                IGC.SendBroadcastMessage(BroadcastTag, "start");
                string output = $"Sending message: start\n{commands}";
                Echo(output);
                if (toggleAfterFinish)
                {
                    IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool>("toggleAfterFinish", true));
                }
                else if (!toggleAfterFinish)
                {
                    IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool>("toggleAfterFinish", false));
                }
                //list of blocks to ignore
                for (int i = 0; i < _commandLine.ArgumentCount; i++)
                {
                    startBuilder.Add(_commandLine.Argument(i));
                }
                ImmutableList<string> ignoringBlocksList = startBuilder.ToImmutable();
                startBuilder.Clear();
                IGC.SendBroadcastMessage(BroadcastTag, ignoringBlocksList);

                //Me.CustomData+=($"sent: {ignoringBlocksList.Count}");
            }
            else if (!setupAlreadySent)
            {
                LCDLog.WriteText($"{header} \nRUN SETUP FIRST");
                DeactivateAll();
                return;
            }
            else if (!correctVersion)
            {
                LCDLog.WriteText($"{header} \nDrone script {droneVersion}\nStation script {stationVersion}\n{lcd_divider}\n" +
                            $"Different Version of scripts found,\n DONWLOAD THE UPDATED ONE");
                DeactivateAll();
                return;
            }
            else if (initializedRequired)
            {
                LCDLog.WriteText($"{header} \nDrone script {droneVersion}\nStation script {stationVersion}\n{lcd_divider}\n" +
                            $"Drone needs init: run \"init_d\"");
                DeactivateAll();
                return;
            }
        }

        public void Stop()
        {
            IGC.SendBroadcastMessage(BroadcastTag, "stop");
            Rotor.TargetVelocityRPM = 0;
            Echo($"Sending message: stop\n{commands}");
        }
        public void Projector()
        {
            IGC.SendBroadcastMessage(BroadcastTag, "projector");
            Echo($"Sending message: projector\n{commands}");
        }
        public void Skip()
        {
            if (correctVersion && setupAlreadySent && !initializedRequired)
            {
                IGC.SendBroadcastMessage(BroadcastTag, "skip");
                Echo($"Sending message: skip\n{commands}");
            }
            else if (!setupAlreadySent)
            {
                LCDLog.WriteText($"{header} \nRUN SETUP FIRST");
                DeactivateAll();
                return;
            }
            else if (!correctVersion)
            {
                LCDLog.WriteText($"{header} \nDrone script {droneVersion}\nStation script {stationVersion}\n{lcd_divider}\n" +
                            $"Different Version of scripts found,\n DONWLOAD THE UPDATED ONE");
                DeactivateAll();
                return;
            }
            else if (initializedRequired)
            {
                LCDLog.WriteText($"{header} \nDrone script {droneVersion}\nStation script {stationVersion}\n{lcd_divider}\n" +
                            $"Drone needs init: run \"init_d\"");
                DeactivateAll();
                return;
            }
        }
        //skip the actual block been welded and delete it from the list of blocks to weld
        public void IgnoreOne()
        {
            if (correctVersion && setupAlreadySent && !initializedRequired)
            {
                IGC.SendBroadcastMessage(BroadcastTag, "ignore1");
                Echo($"Sending message: ignore1\n{commands}");
            }
            else if (!setupAlreadySent)
            {
                LCDLog.WriteText($"{header} \nRUN SETUP FIRST");
                DeactivateAll();
                return;
            }
            else if (!correctVersion)
            {
                LCDLog.WriteText($"{header} \nDrone script {droneVersion}\nStation script {stationVersion}\n{lcd_divider}\n" +
                            $"Different Version of scripts found,\n DONWLOAD THE UPDATED ONE");
                DeactivateAll();
                return;
            }
            else if (initializedRequired)
            {
                LCDLog.WriteText($"{header} \nDrone script {droneVersion}\nStation script {stationVersion}\n{lcd_divider}\n" +
                            $"Drone needs init: run \"init_d\"");
                DeactivateAll();
                return;
            }
        }
        public void Toggle()
        {
            //builder.Clear();
            for (int i = 0; i < _commandLine.ArgumentCount; i++)
            {
                toggleBuilder.Add(_commandLine.Argument(i));
            }
            ImmutableList<string> toggleList = toggleBuilder.ToImmutable();
            toggleBuilder.Clear();
            //Me.CustomData+=($"{toggleList.Count}");
            //foreach(var toggle in toggleList)
            //{
            //    Echo($"{toggle}");
            //}
            IGC.SendBroadcastMessage(BroadcastTag, toggleList);
            Echo($"Sending message: toggle\n{commands}");
        }
        public void Setup()
        {
            CustomData();
            IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<double, float, double, float,
                MyTuple<double, bool, double>,
                MyTuple<MatrixD, int>>(
                WaitingCustom,
                DynamicSpeedCustom,
                DroneMovDistanceCustom,
                RotorSpeedCustom,
                new MyTuple<double, bool, double>(maxDistanceStopCustom, weldWhileMovingCustom, maxRTCustom),
                new MyTuple<MatrixD, int>(
                rotorMatrix, welderSign))
                );
            Echo($"Sending message: setup\n{commands}");
        }
        public void Rotor_ws()
        {
            if (correctVersion && setupAlreadySent && !initializedRequired)
            {
                string s = _commandLine.Argument(0);
                float workingSpeed;
                float burstSpeed;

                if (float.TryParse(_commandLine.Argument(1), out workingSpeed)
                    &&
                    float.TryParse(_commandLine.Argument(2), out burstSpeed))
                {
                    MyTuple<string, float, float> rotorWorkingSpeed = new MyTuple<string, float, float>(
                                s, workingSpeed, burstSpeed);
                    IGC.SendBroadcastMessage(BroadcastTag, rotorWorkingSpeed);
                    Echo($"Sending message: rotor_ws {workingSpeed} {burstSpeed}\n{commands}");
                }
                else
                {
                    Echo("Insert a valid value for rotor speed"); TextWriting("Insert a valid value for rotor speed");
                }
            }
            else if (!setupAlreadySent)
            {
                LCDLog.WriteText($"{header} \nRUN SETUP FIRST");
                DeactivateAll();
                return;
            }
            else if (!correctVersion)
            {
                LCDLog.WriteText($"{header} \nDrone script {droneVersion}\nStation script {stationVersion}\n{lcd_divider}\n" +
                            $"Different Version of scripts found,\n DONWLOAD THE UPDATED ONE");
                DeactivateAll();
                return;
            }
            else if (initializedRequired)
            {
                LCDLog.WriteText($"{header} \nDrone script {droneVersion}\nStation script {stationVersion}\n{lcd_divider}\n" +
                            $"Drone needs init: run \"init_d\"");
                DeactivateAll();
                return;
            }
        }
        public void Max_distance()
        {
            if (correctVersion && setupAlreadySent && !initializedRequired)
            {
                string s = _commandLine.Argument(0);
                double distance;
                if (double.TryParse(_commandLine.Argument(1), out distance))
                {
                    MyTuple<string, double> maxDistance = new MyTuple<string, double>(s, distance);
                    IGC.SendBroadcastMessage(BroadcastTag, maxDistance);
                    Echo($"Sending message: max_distance {distance} meters\n{commands}");
                }
                else
                {
                    Echo("Insert a valid value Drone Max Distance"); TextWriting("Insert a valid value for Drone Max Distance");
                }
            }
            else if (!setupAlreadySent)
            {
                LCDLog.WriteText($"{header} \nRUN SETUP FIRST");
                DeactivateAll();
                return;
            }
            else if (!correctVersion)
            {
                LCDLog.WriteText($"{header} \nDrone script {droneVersion}\nStation script {stationVersion}\n{lcd_divider}\n" +
                            $"Different Version of scripts found,\n DONWLOAD THE UPDATED ONE");
                DeactivateAll();
                return;
            }
            else if (initializedRequired)
            {
                LCDLog.WriteText($"{header} \nDrone script {droneVersion}\nStation script {stationVersion}\n{lcd_divider}\n" +
                            $"Drone needs init: run \"init_d\"");
                DeactivateAll();
                return;
            }
        }
        public void Drone_move()
        {
            if (correctVersion && setupAlreadySent && !initializedRequired)
            {
                string s = _commandLine.Argument(0);
                double movement;
                if (double.TryParse(_commandLine.Argument(1), out movement))
                {
                    MyTuple<string, double> droneMove = new MyTuple<string, double>(s, movement);
                    IGC.SendBroadcastMessage(BroadcastTag, droneMove);
                    Echo($"Sending message: drone_move {movement}\n{commands}");
                }
                else
                {
                    Echo("Insert a valid value Drone Movement"); TextWriting("Insert a valid value for Drone Movement");
                }
            }
            else if (!setupAlreadySent)
            {
                LCDLog.WriteText($"{header} \nRUN SETUP FIRST");
                DeactivateAll();
                return;
            }
            else if (!correctVersion)
            {
                LCDLog.WriteText($"{header} \nDrone script {droneVersion}\nStation script {stationVersion}\n{lcd_divider}\n" +
                            $"Different Version of scripts found,\n DONWLOAD THE UPDATED ONE");
                DeactivateAll();
                return;
            }
            else if (initializedRequired)
            {
                LCDLog.WriteText($"{header} \nDrone script {droneVersion}\nStation script {stationVersion}\n{lcd_divider}\n" +
                            $"Drone needs init: run \"init_d\"");
                DeactivateAll();
                return;
            }
        }
        public void Waiting()
        {
            if (correctVersion && setupAlreadySent && !initializedRequired)
            {
                string s = _commandLine.Argument(0);
                double waiting;
                if (double.TryParse(_commandLine.Argument(1), out waiting))
                {
                    MyTuple<string, double> Imwaiting = new MyTuple<string, double>(s, waiting);
                    IGC.SendBroadcastMessage(BroadcastTag, Imwaiting);
                    Echo($"Sending message: waiting {waiting}\n{commands}");
                }
                else
                {
                    Echo("Insert a valid value for Wait"); TextWriting("Insert a valid value for Wait");
                }
            }
            else if (!setupAlreadySent)
            {
                LCDLog.WriteText($"{header} \nRUN SETUP FIRST");
                DeactivateAll();
                return;
            }
            else if (!correctVersion)
            {
                LCDLog.WriteText($"{header} \nDrone script {droneVersion}\nStation script {stationVersion}\n{lcd_divider}\n" +
                            $"Different Version of scripts found,\n DONWLOAD THE UPDATED ONE");
                DeactivateAll();
                return;
            }
            else if (initializedRequired)
            {
                LCDLog.WriteText($"{header} \nDrone script {droneVersion}\nStation script {stationVersion}\n{lcd_divider}\n" +
                            $"Drone needs init: run \"init_d\"");
                DeactivateAll();
                return;
            }
        }
        public void Align()
        {
            if (correctVersion && setupAlreadySent && !initializedRequired)
            {
                string s = _commandLine.Argument(0);
                var rotorOrientation = Rotor.WorldMatrix.GetOrientation();
                IGC.SendBroadcastMessage(BroadcastTag, rotorOrientation);
                Echo($"Sending message: allign\n{commands}");
            }
            else if (!setupAlreadySent)
            {
                LCDLog.WriteText($"{header} \nRUN SETUP FIRST");
                DeactivateAll();
                return;
            }
            else if (!correctVersion)
            {
                LCDLog.WriteText($"{header} \nDrone script {droneVersion}\nStation script {stationVersion}\n{lcd_divider}\n" +
                            $"Different Version of scripts found,\n DONWLOAD THE UPDATED ONE");
                DeactivateAll();
                return;
            }
            else if (initializedRequired)
            {
                LCDLog.WriteText($"{header} \nDrone script {droneVersion}\nStation script {stationVersion}\n{lcd_divider}\n" +
                            $"Drone needs init: run \"init_d\"");
                DeactivateAll();
                return;
            }
        }
        public void UntagDrone()
        {
            string s = _commandLine.Argument(0);
            string tag = _commandLine.Argument(1);
            MyTuple<string, string> untagTuple = new MyTuple<string, string>(s, tag);
            IGC.SendBroadcastMessage(BroadcastTag, untagTuple);
            Echo($"Sending message: {s}\n{commands}");
        }
        public void UntagStation()
        {
            string tag = _commandLine.Argument(1);
            List<IMyTerminalBlock> everything = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType(everything, x => x.CustomName.Contains(tag));
            //Echo($"{everything.Count}");
            if (everything != null || everything.Count > 0)
            {
                foreach (var block in everything)
                {

                    block.CustomName = block.CustomName.Trim();
                    //block.CustomName = block.CustomName.Replace("." + TagCustom, "");
                    block.CustomName = block.CustomName.Replace(tag, "");
                    LCDLog.WriteText($"{header} \nTag: {tag} removed from Station.");
                }
            }
            else
            {
                LCDLog.WriteText($"{header} \nNo Tag: {tag} found in Station.");
            }
        }
        public void SetupBlocks()
        {
            //LCD LOG SETUP
            GridTerminalSystem.GetBlocksOfType(LcdList, x => x.CustomName.Contains(TagCustom + logLCD));
            if (LcdList == null || LcdList.Count > 1 || !LcdList.Any())
            {
                Echo($"No LOG LCD found or more than 1 LOG LCD found \nUse [{TagDefault}] tag, or change it in Custom Data");
                //TextWriting($"No LOG LCD found or more than 1 LOG LCD found \nUse [{TagDefault}] tag, or change it in Custom Data");
                return;
            }
            LCDLog = LcdList[0];
            LCDLog.WriteText("");
            LCDLog.FontSize = fontsize;
            LCDLog.Font = "Monospace";
            LCDLog.ContentType = ContentType.TEXT_AND_IMAGE;
            LCDLog.FontColor = lcd_font_colour;
            LCDLog.FontSize = fontsize;
            if (!LCDLog.CustomData.Contains("hudlcd")) LCDLog.CustomData = defaultHudLcd;
            //Antenna setup
            GridTerminalSystem.GetBlocksOfType(antennaList);
            if (antennaList == null || antennaList.Count == 0)
            {
                Echo($"No Antenna found. Please, add one Antenna");
                TextWriting($"No Antenna found. Please, add one Antenna");
                return;
            }
            var antenna = antennaList[0];
            antenna.Enabled = true;
            antenna.EnableBroadcasting = true;
            antenna.Radius = 1000;

            
            //LCD STATUS SETUP
            try
            {
                LcdList.Clear();
                GridTerminalSystem.GetBlocksOfType(LcdList, x => x.CustomName.Contains(TagCustom + statusLCD));
                LCDStatus = LcdList[0];
                LCDStatus.FontSize = fontsize;
                LCDStatus.Font = "Monospace";
                LCDStatus.ContentType = ContentType.TEXT_AND_IMAGE;
                LCDStatus.FontColor = lcd_font_colour;
                if (!LCDStatus.CustomData.Contains("hudlcd")) LCDStatus.CustomData = defaultHudStatus;
                statusLCDFound = true;
            }
            catch
            {

            }
            //ACTIVE WELDED BLOCK LCD
            try
            {
                LcdList.Clear();
                GridTerminalSystem.GetBlocksOfType(LcdList, x => x.CustomName.Contains(TagCustom + activePrinterLCD));
                LCDActive = LcdList[0];
                LCDActive.FontSize = fontsize;
                LCDActive.Font = "Monospace";
                LCDActive.ContentType = ContentType.TEXT_AND_IMAGE;
                LCDActive.FontColor = lcd_font_colour;
                if (!LCDActive.CustomData.Contains("hudlcd")) LCDActive.CustomData = defaultHudActive;
                activeLCDFound = true;
            }
            catch
            {

            }
            //debug LCD
            try
            {
                GridTerminalSystem.GetBlocksOfType(LCDDebug, x => x.CustomName.Contains(TagCustom + ".DEBUG"));
                debug = LCDDebug[0];
                debug.FontSize = fontsize;
                debug.Font = "Monospace";
                debug.ContentType = ContentType.TEXT_AND_IMAGE;
                debug.FontColor = lcd_font_colour;
                if (!debug.CustomData.Contains("hudlcd")) debug.CustomData = defaultHudDebug;
                statusLCDFound = true;
            }
            catch
            { }
            //ROTOR SETUP: if only one-->found it and add the tag automatically
            GridTerminalSystem.GetBlocksOfType(RotorList);
            if (RotorList != null && RotorList.Count == 1)
            {
                Rotor = RotorList[0];
                Rotor.Torque = RotorTorqueValue;
                Rotor.BrakingTorque = RotorTorqueValue;
                rotorMatrix = Rotor.WorldMatrix;
                //Echo($"rotor position: {rotorMatrix.Translation}");
                if (!Rotor.CustomName.Contains(TagCustom))
                {
                    Rotor.CustomName += "." + TagCustom;
                }
            }
            else if (RotorList.Count > 1)
            {
                GridTerminalSystem.GetBlocksOfType(RotorList, x => x.CustomName.Contains(TagCustom));
                if (RotorList == null || RotorList.Count > 1 || !RotorList.Any())
                {
                    Echo($"No Rotor found or more than 1 Rotor found \nUse [{TagDefault}] tag, or change it in Custom Data");
                    TextWriting($"No Rotor found or more than 1 Rotor found \nUse [{TagDefault}] tag, or change it in Custom Data");
                    return;
                }
                Rotor = RotorList[0];
                Rotor.Torque = RotorTorqueValue;
                Rotor.BrakingTorque = RotorTorqueValue;
                rotorMatrix = Rotor.WorldMatrix;
            }
            //SETUP ROTORHEAD
            rotorHead = Rotor.Top as IMyMotorAdvancedRotor;

            //WELDERS SETUP
            List<IMyBlockGroup> WeldersGroupList = new List<IMyBlockGroup>();
            List<IMyShipWelder> taggedWelders = new List<IMyShipWelder>();
            GridTerminalSystem.GetBlockGroups(WeldersGroupList, x => x.Name.Contains(TagCustom));
            GridTerminalSystem.GetBlocksOfType(WelderList);
            GridTerminalSystem.GetBlocksOfType(taggedWelders, x => x.CustomName.Contains(TagCustom));
            //Echo($"{taggedWelders.Count} Welders");
            if (WelderList != null && WelderList.Count > 0 && WeldersGroupList.Count == 0)
            {
                foreach (var welder in WelderList.Where(t => !t.CustomName.Contains(TagCustom)))
                {
                    welder.CustomName += "." + TagCustom;
                }
            }

            //No tagged welder and no welders group/more than one 
            if ((taggedWelders.Count == 0
                &&
                (WeldersGroupList == null || WeldersGroupList.Count == 0 || WeldersGroupList.Count > 1)
                )
                ||
                // tagged welders and one or more welder groups
                (taggedWelders.Count > 0 && WeldersGroupList.Count > 1)
                ||
                //tagged welders and welder group
                (taggedWelders.Count > 0 && WeldersGroupList.Count == 1))
            {
                Echo($"Welders group not found or more than 1 Welders Group found \nUse the [{TagDefault}] tag, or change it in Custom Data");
                TextWriting($"Welders not found or more than 1 Welders Group found \nUse [{TagDefault}] tag, or change it in Custom Data");
                return;
            }
            if (WeldersGroupList != null && WeldersGroupList.Count == 1 && taggedWelders.Count == 0)
            {
                WelderList.Clear();
                WeldersGroupList[0].GetBlocksOfType(WelderList);
            }
            // Fancy stuff
            List<IMyBlockGroup> groups = new List<IMyBlockGroup>();
            GridTerminalSystem.GetBlockGroups(groups, x => x.Name.Contains(TAGFancy));
            if (groups.Count == 1)
            {
                var group = groups[0];
                group.GetBlocksOfType(FancySoundList);
                group.GetBlocksOfType(FancyLCDList);
                group.GetBlocksOfType(FancyLightList);
                if (FancySoundList != null && FancySoundList.Count == 1)
                    soundBlock = FancySoundList[0];
            }
            if (groups.Count != 1)
            {
                Echo(groups.Count == 0 ? $"No {TAGFancy} group" : $"Too many groups found: {groups.Count}");
            }
            FarWerlder(); //determine what is the farest welder from the rotor 
            ///check the orientation of the farest welder: aligned with forward or backward?
            ///is aligned with right or left of the rotor?
            WelderAngle();
            //Me.CustomData += welderSign;

            //setup completed
            setupCompleted = true;
        }

        //in the main we've got the tryparse(argument) into a string and invoke the Action as value of dictionary
        public void Main(string argument, UpdateType updateSource)
        {
            if ((updateSource & (UpdateType.Trigger | UpdateType.Terminal)) > 0) // run by a terminal action
            {
                if (_commandLine.TryParse(argument))
                {
                    Action commandAction;
                    string commandString = _commandLine.Argument(0);
                    commandString = commandString.ToLower();
                    if (commandString == null)
                    {
                        Echo("No command specified");
                        TextWriting("No command specified");
                    }
                    else if (commandDict.TryGetValue(commandString, out commandAction))
                    {
                        commandAction();
                    }
                    else
                    {
                        Echo($"Unknown command {commandString}");
                        TextWriting($"Unknown command {commandString}");
                    }
                }
                else
                {
                    LCDLog.CustomData = defaultHudLcd;
                    Echo("Command not Parsed");
                    TextWriting("Command not Parsed");
                }
            }

            if ((updateSource & UpdateType.IGC) > 0)
            {
                ImListening();
            }
        }

        string CentreText(string Text, int Width)
        {
            int spaces = Width - Text.Length;
            int padLeft = spaces / 2 + Text.Length;
            return Text.PadLeft(padLeft).PadRight(Width);
        }
        public void TextWriting(string text)
        {
            string input = lcd_header + "\n" + text;
            LCDLog.WriteText(CentreText(input, 32));
        }
        public void GuideWriting(string text)
        {
            string input = lcd_header + "\n" + text;
            LCDStatus.WriteText(CentreText(input, 32));
        }
        public string CommandHeaderCreation()
        {
            string output = $"{lcd_header}\n          {lcd_printing_version}  \n" +
                $"{lcd_divider}\n" +
                "     STATION SETUP COMPLETED\n" +
                "|Log LCD found\n" +
                $"|Status LCD found: {statusLCDFound}\n" +
                $"|Active Block LCD found: {activeLCDFound}\n" +
                "|Rotor found\n" +
                "|Fancy LCDs:" + FancyLCDList.Count + "\n" +
                "|Welders group:" + WelderList.Count + "\n" +
                "|Fancy Spotlight: " + FancyLightList.Count + "\n" +
                $"|Torque and Breaking set to {RotorTorqueValue / 1000000} MNm \n" +
                $"|Tag used: {TagCustom}\n"
                + lcd_divider + "\n"
                + lcd_command_title
                + "\n" + lcd_divider + "\n"
                + compact_commands; ;
            return output;
        }
        public string HeaderCreation()
        {
            lcd_spinner_status += Runtime.TimeSinceLastRun.TotalSeconds;
            if (lcd_spinner_status > lcd_spinners.Length) { lcd_spinner_status = 0; }
            string spinner = lcd_spinners[(int)lcd_spinner_status];
            string output = $"{lcd_header}\n        {spinner + spinner + lcd_printing_version + spinner + spinner}\n" +
                $"{lcd_divider}";
            return output;
        }
        public string StatusLCDHeaderCreation()
        {
            //lcd_spinner_status += Runtime.TimeSinceLastRun.TotalSeconds;
            //if (lcd_spinner_status > lcd_spinners.Length) { lcd_spinner_status = 0; }
            string spinner = lcd_spinners[(int)lcd_spinner_status];
            string output = $"{lcd_divider}\n{spinner + spinner + lcd_status_title + spinner + spinner}\n" +
                $"{lcd_divider}";
            return output;
        }
        /// <summary>
        /// Find the farest welder from thew rotor to decide the direction
        /// of the alignment
        /// </summary>
        public void FarWerlder()
        {
            foreach (var w in WelderList)
            {
                var distance = (w.GetPosition() - Rotor.GetPosition()).Length();
                if (distance > farestWelderDistance)
                {
                    farestWelderDistance = distance;
                    farestWelder = w;
                }
            }
            //Me.CustomData+=($"Farest welder: {farestWelderDistance}\nwelder: {farestWelder.CustomName}");
        }
        public void WelderAngle()
        {
            var dir = (farestWelder.GetPosition() - Rotor.GetPosition());
            //Echo($"{dir}");
            var axis = rotorHead.WorldMatrix.Up;
            dir -= (axis * Vector3D.Dot(axis, dir));
            var angle_right = Vector3D.Dot(rotorHead.WorldMatrix.Right, dir);
            var angle_forward = Vector3D.Dot(rotorHead.WorldMatrix.Forward, dir);
            if (Math.Abs(angle_forward) > 0.1) { welderAngle = angle_forward; welder_forward = true; };
            //Me.CustomData += $"angle: {welderAngle}--bool forward {welder_forward}\n";
            if (Math.Abs(angle_right) > 0.1) { welderAngle = angle_right; welder_right = true; }
            //Me.CustomData += $"angle: {welderAngle}--bool right: {welder_right}\n";
            welderSign = Math.Sign(welderAngle);
        }
        public void DeactivateAll()
        {
            Rotor.Enabled = false;
            foreach (var lcd in FancyLCDList)
            {
                lcd.Enabled = false;
            }
            foreach (var welder in WelderList)
            {
                welder.Enabled = false;
            }
            foreach (var light in FancyLightList)
            {
                light.Enabled = false;
            }
            foreach (var sound in FancySoundList) { sound.Volume = 0f; sound.Enabled = false; }
        }
        public void ImListening()
        {
            while (_myBroadcastListener_station.HasPendingMessage)
            {
                var myIGCMessage_fromDrone = _myBroadcastListener_station.AcceptMessage();
                if (myIGCMessage_fromDrone.Tag == BroadcastTag && myIGCMessage_fromDrone.Data is MyTuple<string, string>)
                {
                    var tuple = (MyTuple<string, string>)myIGCMessage_fromDrone.Data;
                    string log = tuple.Item1;
                    string status = tuple.Item2;
                    //Echo($"setup: {status}");
                    if (log == "droneVersion")
                    {
                        droneVersion = status;
                        if (droneVersion == stationVersion)
                        {
                            correctVersion = true;
                            try
                            {
                                LCDStatus.WriteText($"{lcd_header} \nDrone Script   {droneVersion}\nStation Script {stationVersion}\n\nDETECTED CORRECT VERSIONS\n" +
                                                $"{lcd_divider}\n{lcd_divider}\n" +
                                                $"{lcd_changelog}");
                            }

                            catch
                            {
                            }
                        }
                        else
                        {
                            correctVersion = false;
                            try
                            {
                                LCDLog.WriteText($"{lcd_header} \nDrone Script {droneVersion}\nStation Script {stationVersion}\n{lcd_divider}\n" +
                                    $"Different Version of scripts found,\n please DONWLOAD THE UPDATED ONE");
                                Echo("Different Version of scripts found,\n please DONWLOAD THE UPDATED ONE");
                                LCDStatus.WriteText($"{lcd_header}\nDrone Script {droneVersion}\nStation Script {stationVersion}\n{lcd_divider}");
                            }
                            catch { }
                            return;
                        }
                    }
                    if (log == "ActiveWelding")
                    {
                        try
                        {
                            LCDActive.WriteText($"{status}");
                        }
                        catch { }
                    }
                    if(log=="LogWriting")
                    {
                        LCDLog.WriteText($"{HeaderCreation()} \n{status}");
                        //continues stream of rotorHead infos
                        if (activation)
                        {
                            IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool, bool, MatrixD>(
                                                "rotorHead", welder_right, welder_forward, rotorHead.WorldMatrix));
                        }
                    }
                    if(log == "StatusWriting")
                    {

                        string stuckedY = "Looking for the Block";
                        string stuckedN = "Unstuck";
                        string fastTrip = "Fast rotation";
                        if (Rotor.TargetVelocityRPM != RotorSpeedCustom && Rotor.TargetVelocityRPM != 0 &&
                            Rotor.TargetVelocityRPM != DynamicSpeedCustom) stuckStatus = stuckedY;
                        else if (Rotor.TargetVelocityRPM == RotorSpeedCustom) stuckStatus = stuckedN;
                        else if (Rotor.TargetVelocityRPM == 0) stuckStatus = "Welding";
                        else if (Rotor.TargetVelocityRPM == DynamicSpeedCustom) stuckStatus = fastTrip;

                        LCDStatus.WriteText($"{StatusLCDHeaderCreation()} \n{status}\n{lcd_divider}\n         WELDERS STATUS\n{lcd_divider}\n{stuckStatus}");
                        
                        Echo(compact_commands);
                    }
                }
                //DEBUG LCD
                if (myIGCMessage_fromDrone.Tag == BroadcastTag && myIGCMessage_fromDrone.Data is MyTuple<string, string>)
                {
                    try
                    {
                        var tuple = (MyTuple<string, string>)myIGCMessage_fromDrone.Data;
                        string deb = tuple.Item1;
                        var message = tuple.Item2;
                        if(deb == "Debug") debug.WriteText(message);
                        //var tuple = (MyTuple<string, string, string, string, string>)myIGCMessage_fromDrone.Data;
                        //string checkTime = tuple.Item1;
                        //string name = tuple.Item2;
                        //string integrity = tuple.Item3;
                        //string newIntegrity = tuple.Item4;
                        //string angle = tuple.Item5;
                        //debug.WriteText($"DEBUG\nStuck Time check: {checkTime}\nBlock Name: {name}\n" +
                        //    $"Integrity: {integrity}\nNewIntegrity: {newIntegrity}\nAngle: {angle}");

                    }
                    catch
                    { }
                }
                if (myIGCMessage_fromDrone.Tag == BroadcastTag && myIGCMessage_fromDrone.Data is string)
                {
                    string data_log = myIGCMessage_fromDrone.Data.ToString();
                    LCDLog.WriteText($"{header} \n{data_log}");
                }

                if (myIGCMessage_fromDrone.Tag == BroadcastTag && myIGCMessage_fromDrone.Data is float)
                {
                    Rotor.TargetVelocityRPM = (float)(myIGCMessage_fromDrone.Data);
                }

                if (myIGCMessage_fromDrone.Tag == BroadcastTag && myIGCMessage_fromDrone.Data is MyTuple<string, bool>)
                {
                    var tuple = (MyTuple<string, bool>)myIGCMessage_fromDrone.Data;
                    string myString = tuple.Item1;
                    if (myString == "activation")
                    {
                        activation = tuple.Item2;
                        if (activation)
                        {
                            Rotor.RotorLock = false;
                            Rotor.Enabled = true;
                            foreach (var welder in WelderList)
                            {
                                welder.Enabled = true;
                            }
                            foreach (var lcd in FancyLCDList)
                            {
                                lcd.Enabled = true;
                            }
                            foreach (var light in FancyLightList)
                            {
                                light.Enabled = true;
                            }
                            Music();
                        }
                        else if (!activation)
                        {
                            DeactivateAll();
                            IGC.DisableBroadcastListener(_myBroadcastListener_station);
                            _myBroadcastListener_station = IGC.RegisterBroadcastListener(BroadcastTag);
                            _myBroadcastListener_station.SetMessageCallback(BroadcastTag);
                        }
                    }
                    else if (myString == "weldersToggle")
                    {
                        bool weldersToggle = tuple.Item2;
                        if (!weldersToggle)
                        {
                            foreach (var welder in WelderList)
                            { welder.Enabled = false; }
                        }
                        else if (weldersToggle)
                        {
                            foreach (var w in WelderList)
                            {
                                w.Enabled = true;
                            }
                        }
                    }
                    else if (myString == "DroneSetup")
                    {
                        bool DroneSetup = tuple.Item2;
                        //Echo($"setup: {DroneSetup}");
                        if (!DroneSetup)
                        {
                            return;
                        }
                    }
                    else if (myString == "initRequired")
                    {
                        initializedRequired = tuple.Item2;
                        if (initializedRequired)
                        {
                            return;
                        }
                    }
                    else if(myString == "SetupSent")
                    {
                        setupAlreadySent = tuple.Item2;
                    }
                }
            }
        }




    }
}
