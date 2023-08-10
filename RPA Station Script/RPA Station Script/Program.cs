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

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        /// <summary>
        /// "Robotic Printing Automation" by Reckless
        /// Current Version: V 3.3.0
        /// Script == Station
        /// Guide's link: https://steamcommunity.com/sharedfiles/filedetails/?id=2965554098
        ///
        /// CHANGELOG V 3.3.0 (x/08/2023)
        /// -Heavy clean up of the code from useless computations in order to improve performance;
        /// -Rework of start and toggle commands. See guide;
        /// -Add an oprional LCD called .ACTIVE to print infos about active welded block and printing in general;
        /// Changelog V 3.2.5 (03/08/2023)
        /// -Improved the Logics to handle the rotor's Speed;
        /// -Added a "stuck" status to check when rotor will move backward to weld some missed blocks
        /// Changelog V 3.2.4 (31/07/2023)
        /// -Polished the code;
        /// -Add several verbose checks , to catch more exceptions, point out when you have to run setup or have different versions of script
        /// -Add a "changelog" command. Run "changelog -off" to delete it from the Status LCD
        /// Changelog V 3.2.3 (30/07/2023)
        /// -Added security check if you don't have run setup before starting the print;
        /// -Added several report in case setup is not completed correctly;
        /// -caught several exceptions;
        /// -send to LOG lcd all main problems regarding uncompleted setup;
        /// Changelog V 3.2.2 (30/07/2023)
        /// -If the tug is tilted more than 25 degrees, it won't auto align;
        /// -During the Active Printing, the time will stop, until not a single block is being printed;
        /// -Wait variable during the printing will blink to point out that printing is in progress;
        /// -Add a random music to play if sound block is present;
        /// -Add command "music" to play random music; Add " -off" to turn it off;
        /// Changelog V 3.2.1 (29/07/2023)
        /// -Better tuning for aligning portion of the script;
        /// -Add a check for the scripts version, who detects differences between station's and drone's
        /// Changelog V 3.2.0 (28/07/2023)
        /// -Fixed several bugs;
        /// -Added a check for aligning condition;
        /// -Now the rotor will stop when a functional block is being printed;
        /// Changelog V 3.2.0 (28/07/2023)
        /// -Fixed several bugs;
        /// -Added a check for aligning condition;
        /// -Now the rotor will stop when a functional block is being printed; 
        /// Changelog V 3.1.0 (27/07/2023)
        /// -Bugs fixed
        /// -Added an automatic alignment between the tug and the drone for the entire duration of the print
        /// -Added a new command "align" to force the alignment
        /// -DRONE ONLY: COCKPIT FORWARD direction must be perpendicular to printers!!!
        /// -DRONE ONLY: BACKWARD thrusters only used!!
        /// Changelog V 3.0.5 (25/07/2023)
        /// Fix welders group setting
        /// Changelog V 3.0.4 (24/07/2023)
        /// -Add the tag to the tug's hydrongen tank for a better check on hydro %
        /// Changelog V 3.0.3 (23/07/2023)
        /// -"Toggle" now turn off all tools and projs too
        /// /// Changelog V 3.0.2 (19/07/2023)
        /// Added Projectioin status, Tug's H2 level and printing percentage
        /// Changelog V 3.0.1 (19/07/2023)
        /// Fixed some typos and formattings;
        /// Changelog V 3.0.0 (17/07/2023)
        /// Rewritten the hud;
        /// Added hudlcd functionalitites;
        /// Added "guide" command for easy reference in game
        /// Changelog V 2.9 (17/07/2023)
        /// Add new QoL commands;
        /// Add sound blocks to Fancy blocks;
        /// Add Antenna block check;
        /// Add hudlcd auto added to LCD, and "hudlcd:toggle" as command
        /// Changelog V 2.8 (10/07/2023)
        /// QoL Changes:
        /// All commands are now case insensitive;
        /// If the script is used for no "SIGMA DRACONIS EXPANSE Server" there should be no more errors;
        /// If you use the standard configuration for the drone (using backward thrusters to move away from the welders, 1 cockpit, 1 projector), you don't need to add the tag [RPA];
        /// If you use the standard configuration for the station (only 1 rotor and all welders for printing), you have to put the tag [RPA] only for the other needed blocks;
        /// Changelog V 2.7 (01/07/2023)
        /// Add a new command: "toggle" that will toggle on every blocks of the tug's grid, except for Epstein drivers (Hydrazine are toggled on thou)
        /// Changelog V 2.6 (29/06/2023)
        /// Add a check to the block's integrity: now, every block has to reach 100% integrity before the scripts move the Drone back
        /// Changelog V 2.5 (18/06/2023)
        /// Deleted static printing;
        /// fixed bug that didn't allow to change distance movement of the drone after have finished the section;
        /// Changelog V 2.4 (01/06/2023):
        /// Fixed a bug with Safety Distance that was not calculated;
        /// Added the "DroneMovementDistance(meters)" in customData, in order to set the meters the tug thrusts in every iteration (stopping distance is more or less 1/3 of the thrusting distance);
        /// Changelog V 2.3 (31/05/2023):
        /// Added the "update" command, to set to ignore remaning unwelded blocks, in order to continue the welding
        /// Added a "forced stop" if the drone goes too far from starting point
        /// Added the optional group with [RPA-Fancy] to toggle on/off lcd,lights and welders during start and stop
        /// Fixed some minor bugs
        /// Changelog V 2.2 (05/05/2023):
        /// After several ships printed, i fixed some minor bugs and tuned some numbers;
        /// Change the Torque value of the rotor to 40 MN to have a better acceleration;
        /// Finally understood how to update scripts instead to delete-reupload.From now on, this shouldn't happen anymore;
        /// Changelog V 2.1 (27/04/2023):
        /// Fix bug for Drone's moving distance (it moves twice the wanted distance)
        /// Chngelog V 2.0 (26/04/2023):
        /// Fix bug of rotor speed;
        /// Added rotor speed to log and lcd panel
        /// Fix bug when setup command was ran, that forced the drone to start the process
        /// Get rid of naming conventio, and add tag requirment to the blocks/groups.Default is [RPA], but you can always change it in CustomData of the Station's script;
        /// All logs should be now polished, meaning, if you forgot any tag or block, you'll see in the PB log or the station's LCD panel; In general, more informations are printed;
        /// All variables that can be changed in CustomData of the Drone, have been moved in the Station's PB CustomData, so, if you want to change any of them, youu don't need anymore to reach the Drone's PB (Engineers are lazy!);
        /// Station's PB CustomData have more variables: RotorSpeed; DynamicRotorCheck; DynamicSpeed (see Station Setup chapter);
        /// A new configuration is possible: DynamicRotorCheck, to have better chance to cover all blocks(See Tuning Chapter);
        /// Added list of commands in the Station's PB log;
        /// 
        /// Needed blocks for station:
        /// 1)LCD for log
        /// 2)Rotor
        /// 3)Antenna
        /// 4)PB to put the script
        /// 5)OPTIONAL: Put in a single group (with the tag: [RPA-Fancy]): lights, LCDs and 1 sound block. Will be toggle on or off after start and stop;
        /// 6)OPTIONAL: 1 LCD for Status
        /// </summary>

        readonly string stationVersion = "V: 3.3.0";
        const string lcd_changelog =
            "\nCHANGELOG VERSION 3.3.0:\n" +
            "-Heavy clean up of the code from useless computations\nin order to improve performance;" +
            "-Rework of start and toggle commands. See guide" +
            "-Add an oprional LCD called .ACTIVE\nto print infos about active welded block and printing in general;" +
            "\n--------------------------------\n" +
            "\nCHANGELOG VERSION 3.2.5:\n" +
            "-Improved the Logics to handle the rotor's Speed;\n" +
            "-Added a rotor control to help welding process\n" +
            "--------------------------------\n" +
            "CHANGELOG OLD VERSION 3.2.4:\n" +
            "-Polished the code;\n" +
            "-Add several verbose checks, to catch more exceptions,\n point out when you have to run setup\n or have different versions of script;\n" +
            "-Add a changelog command. Run changelog -off to delete it from the Status LCD;\n" +
            "--------------------------------\n";
        string droneVersion;
        bool correctVersion = false;
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
        //Fancy stuff
        readonly List<IMySoundBlock> FancySoundList = new List<IMySoundBlock>();
        readonly List<IMyTextPanel> FancyLCDList = new List<IMyTextPanel>();
        readonly List<IMyLightingBlock> FancyLightList = new List<IMyLightingBlock>();
        readonly List<IMyRadioAntenna> antennaList = new List<IMyRadioAntenna>();

        IMySoundBlock soundBlock;

        const string TAGFancy = "[RPA-Fancy]";
        IMyTextPanel LCDLog;
        IMyTextPanel LCDStatus;
        IMyTextPanel LCDActive;
        readonly float fontsize = 0.5f; // font of lcd panel
        const string logLCD = ".LOG";
        const string statusLCD = ".STATUS";
        const string activePrinterLCD = ".ACTIVE";
        bool statusLCDFound=false;
        bool activeLCDFound = false;
        /// <Rotor
        readonly List<IMyMotorAdvancedStator> RotorList = new List<IMyMotorAdvancedStator>();
        IMyMotorAdvancedStator Rotor;

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
            $"update: force the weld even\n with missing TB;\n\n" +
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
            $"waiting x: changes the Wait;]\n\n" +
            $"music -off: play a random music\nAdd -off if want music to stop";

        readonly string compact_commands =
            lcd_divider + "\n" +
            $"[setup\n" +
            $"start x y ... -toggle\n" +
            $"stop\n" +
            $"update\n" +
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
            $"waiting x;]\n" +
            $"music -off";

        Color lcd_font_colour = new Color(30, 144, 255, 255);
        readonly string[] lcd_spinners = new string[] { "-", "\\", "|", "/" };
        double lcd_spinner_status;
        const string lcd_divider = "--------------------------------";
        const string lcd_title =         "  RECKLESS PRINTING AUTOMATION  ";
        const string lcd_command_title = "COMMANDS GUIDE:";
        const string lcd_version = "RPA ";
        
        readonly string lcd_header;
        readonly string lcd_printing_version;
        readonly string header;

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
            WaitingCustom = _ini.Get("data", "Wait").ToDouble(WaitingDefault);
            DroneMovDistanceCustom = _ini.Get("data", "DroneMovementDistance(meters)").ToDouble(DroneMovDistanceDefault);
            RotorSpeedCustom = _ini.Get("data", "RotorSpeed").ToSingle(RotorSpeedDefault);
            DynamicSpeedCustom = _ini.Get("data", "DynamicSpeed(RPM)").ToSingle(DynamicSpeedDefault);
            maxDistanceStopCustom = _ini.Get("data", "MaxDistanceStop(meters)").ToDouble(maxDistanceStopDefault);

            if (!wasParsed)
            {
                _ini.Clear();
            }
            // Set the values to make sure they exist. They could be missing even when parsed ok.
            _ini.Set("data", "TAG", TagCustom);
            _ini.Set("data", "Wait", WaitingCustom);
            _ini.Set("data", "DroneMovementDistance(meters)", DroneMovDistanceCustom);
            _ini.Set("data", "RotorSpeed", RotorSpeedCustom);
            _ini.Set("data", "DynamicSpeed(RPM)", DynamicSpeedCustom);
            _ini.Set("data", "MaxDistanceStop(meters)", maxDistanceStopCustom);

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
            commandDict["update"] = Update;
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
            //commandDict["untag_d"] = Untag_d;
        }
        public void Changelog()
        {
            bool changelogOFF = _commandLine.Switch("off");
            try
            {
                if (changelogOFF)
                {
                    LCDStatus.WriteText(centreText(lcd_header, 32));
                }
                else
                {
                    string changelog = lcd_header + "\n" + lcd_printing_version + lcd_changelog;
                    LCDStatus.WriteText(centreText(changelog, 32));
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
            if(resetGuide)
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
                    LCDStatus.CustomData =LCDStatus.CustomData.Replace("hudlcd", "hudOFFlcd");
                }
                else if (!resetHUD && LCDStatus.CustomData.Contains("hudOFFlcd"))
                {
                    LCDStatus.CustomData=LCDStatus.CustomData.Replace("hudOFFlcd", "hudlcd");
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
            if (correctVersion && setupAlreadySent)
            {
                IGC.SendBroadcastMessage(BroadcastTag, "start");
                string output = $"Sending message: start\n{commands}";
                Echo(output);
                if (toggleAfterFinish) 
                { 
                    IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool>("toggleAfterFinish", true)); 
                }
                else if (!toggleAfterFinish) {
                    IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool>("toggleAfterFinish", false)); 
                }
                //list of blocks to ignore
                for (int i = 0; i<_commandLine.ArgumentCount; i++)
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
                LCDLog.WriteText($"{header} \nRUN SETUP AND ALIGN FIRST");
            }
            else if (!correctVersion)
            {
                LCDLog.WriteText($"{header} \nDrone script {droneVersion}\nStation script {stationVersion}\n{lcd_divider}\n" +
                            $"Different Version of scripts found,\n DONWLOAD THE UPDATED ONE");
            }
        }

        public void Stop()
        {
            IGC.SendBroadcastMessage(BroadcastTag, "stop");
            Echo($"Sending message: stop\n{commands}");
        }
        public void Projector()
        {
            IGC.SendBroadcastMessage(BroadcastTag, "projector");
            Echo($"Sending message: projector\n{commands}");
        }
        public void Skip()
        {
            if (correctVersion && setupAlreadySent)
            {
                IGC.SendBroadcastMessage(BroadcastTag, "skip");
                Echo($"Sending message: skip\n{commands}"); 
            }
            else if (!setupAlreadySent)
            {
                LCDLog.WriteText($"{header} \nRUN SETUP AND ALIGN FIRST");
            }
            else if (!correctVersion)
            {
                LCDLog.WriteText($"{header} \n{droneVersion}\n{stationVersion}\n{lcd_divider}\n" +
                            $"Different Version of scripts found,\n DONWLOAD THE UPDATED ONE");
            }
        }
        public void Update()
        {
            if (correctVersion && setupAlreadySent)
            {
                IGC.SendBroadcastMessage(BroadcastTag, "update");
                Echo($"Sending message: update\n{commands}"); 
            }
            else if (!setupAlreadySent)
            {
                LCDLog.WriteText($"{header} \nRUN SETUP AND ALIGN FIRST");
            }
            else if (!correctVersion)
            {
                LCDLog.WriteText($"{header} \n{droneVersion}\n{stationVersion}\n{lcd_divider}\n" + $"Different Version of scripts found,\n DONWLOAD THE UPDATED ONE");
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
        //public void Untag_d()
        //{
        //    IGC.SendBroadcastMessage(BroadcastTag, "untag_d");
        //    Echo($"Sending message: untag_d\n" +
        //        $"{commands}");
        //}
        public void Setup()
        {
            setupAlreadySent = true;
            CustomData();
            IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<double, float, double, float, double, MatrixD>(
                WaitingCustom,
                DynamicSpeedCustom,
                DroneMovDistanceCustom,
                RotorSpeedCustom,
                maxDistanceStopCustom,
                rotorMatrix)
                );
            Echo($"Sending message: setup\n{commands}");
        }
        public void Rotor_ws()
        {
            if (correctVersion && setupAlreadySent)
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
                LCDLog.WriteText($"{header} \nRUN SETUP AND ALIGN FIRST");
            }
            else if (!correctVersion)
            {
                LCDLog.WriteText($"{header} \n{droneVersion}\n{stationVersion}\n{lcd_divider}\n" +
                            $"Different Version of scripts found,\n DONWLOAD THE UPDATED ONE");
            }
        }
        public void Max_distance()
        {
            if (correctVersion && setupAlreadySent)
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
                LCDLog.WriteText($"{header} \nRUN SETUP AND ALIGN FIRST");
            }
            else if (!correctVersion)
            {
                LCDLog.WriteText($"{header} \n{droneVersion}\n{stationVersion}\n{lcd_divider}\n" +
                            $"Different Version of scripts found,\n DONWLOAD THE UPDATED ONE");
            }
        }
        public void Drone_move()
        {
            if (correctVersion && setupAlreadySent)
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
                LCDLog.WriteText($"{header} \nRUN SETUP AND ALIGN FIRST");
            }
            else if (!correctVersion)
            {
                LCDLog.WriteText($"{header} \n{droneVersion}\n{stationVersion}\n{lcd_divider}\n" +
                            $"Different Version of scripts found,\n DONWLOAD THE UPDATED ONE");
            }
        }
        public void Waiting()
        {
            if (correctVersion && setupAlreadySent)
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
                LCDLog.WriteText($"{header} \nRUN SETUP AND ALIGN FIRST");
            }
            else if (!correctVersion)
            {
                LCDLog.WriteText($"{header} \n{droneVersion}\n{stationVersion}\n{lcd_divider}\n" +
                            $"Different Version of scripts found,\n DONWLOAD THE UPDATED ONE");
            }
        }
        public void Align()
        {
            if (correctVersion && setupAlreadySent)
            {
                string s = _commandLine.Argument(0);
                var rotorOrientation = Rotor.WorldMatrix.GetOrientation();
                IGC.SendBroadcastMessage(BroadcastTag, rotorOrientation);
                Echo($"Sending message: allign\n{commands}"); 
            }
            else if (!setupAlreadySent)
            {
                LCDLog.WriteText($"{header} \nRUN SETUP AND ALIGN FIRST");
            }
            else if (!correctVersion)
            {
                LCDLog.WriteText($"{header} \n{droneVersion}\n{stationVersion}\n{lcd_divider}\n" +
                            $"Different Version of scripts found,\n DONWLOAD THE UPDATED ONE");
            }
        }
        public void SetupBlocks()
        {
            //Antenna setup
            GridTerminalSystem.GetBlocksOfType(antennaList);
            if (antennaList == null || antennaList.Count == 0)
            {
                Echo($"No Antenna found. Please, add one Antenna");
                IGC.SendBroadcastMessage(BroadcastTag, $"No Antenna found. Please, add one Antenna");
                return;
            }
            var antenna = antennaList[0];
            antenna.Enabled = true;
            antenna.EnableBroadcasting = true;
            antenna.Radius = 1000;

            //LCD LOG SETUP
            GridTerminalSystem.GetBlocksOfType(LcdList, x => x.CustomName.Contains(TagCustom+logLCD));
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
            if(!LCDLog.CustomData.Contains("hudlcd"))LCDLog.CustomData = defaultHudLcd;
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
                (WeldersGroupList == null || WeldersGroupList.Count == 0 || WeldersGroupList.Count>1)
                )
                || 
                // tagged welders and one or more welder groups
                (taggedWelders.Count>0 && WeldersGroupList.Count>1)
                ||
                //tagged welders and welder group
                (taggedWelders.Count >0 && WeldersGroupList.Count==1))
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
                if(FancySoundList != null && FancySoundList.Count==1)
                soundBlock = FancySoundList[0];
            }
            if (groups.Count != 1)
            {
                Echo(groups.Count == 0 ? $"No {TAGFancy} group" : $"Too many groups found: {groups.Count}");
            }

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
                    commandString= commandString.ToLower();
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
        
        string centreText(string Text, int Width)
        {
            int spaces = Width - Text.Length;
            int padLeft = spaces / 2 + Text.Length;
            return Text.PadLeft(padLeft).PadRight(Width);
        }
        public void TextWriting(string text)
        {
            string input = lcd_header + "\n" + text;
            LCDLog.WriteText(centreText(input, 32));
        }
        public void GuideWriting(string text)
        {
            string input = lcd_header + "\n" + text;
            LCDStatus.WriteText(centreText(input, 32));
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
                    if (log=="droneVersion")
                    {
                        droneVersion = status;
                        if(droneVersion == stationVersion)
                        { LCDStatus.WriteText($"{header} \nDrone Script {droneVersion}\nStation Script {stationVersion}\n\nDETECTED CORRECT VERSIONS\n" +
                            $"{lcd_divider}\n{lcd_divider}\n" +
                            $"{lcd_changelog}");
                            correctVersion = true;
                        }
                        else {
                            LCDLog.WriteText($"{header} \n{droneVersion}\n{stationVersion}\n{lcd_divider}\n" +
                            $"Different Version of scripts found,\n please DONWLOAD THE UPDATED ONE");
                            LCDStatus.WriteText($"{header}\nDrone Script {droneVersion}\nStation Script {stationVersion}\n{lcd_divider}");
                            correctVersion = false;
                            return;
                        }
                    }
                    else if(log=="ActiveWelding")
                    {
                        try
                        {
                            LCDActive.WriteText($"{status}");
                        }
                        catch { }
                    }

                    else 
                    {
                        string stuckStatus;
                        string stuckedY = "Stucked... Unstacking soon";
                        string stuckedN = "Unstucked";
                        if (Rotor.TargetVelocityRPM!=RotorSpeedCustom && Rotor.TargetVelocityRPM != 0) stuckStatus = stuckedY;
                        else stuckStatus = stuckedN;
                        LCDLog.WriteText($"{HeaderCreation()} \n{log}");
                        LCDStatus.WriteText($"{status}\n{lcd_divider}\n         WELDERS STATUS\n{lcd_divider}\n{stuckStatus}");
                        Echo(compact_commands);
                    }
                }
                if(myIGCMessage_fromDrone.Tag==BroadcastTag && myIGCMessage_fromDrone.Data is MyTuple<string, string, string,string, string>)
                {
                    try
                    {
                        var tuple = (MyTuple<string, string, string, string, string>)myIGCMessage_fromDrone.Data;
                        string time = tuple.Item1;
                        string name = tuple.Item2;
                        string integrity = tuple.Item3;
                        string newIntegrity = tuple.Item4;
                        string difference = tuple.Item5;
                        debug.WriteText($"DEBUG\nStuck Time check: {time}\nBlock Name: {name}\n" +
                            $"Integrity: {integrity}\nNewIntegrity: {newIntegrity}\ndifference: {difference}");
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
                        bool activation = tuple.Item2;
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
                    else if(myString == "DroneSetup")
                    {
                        bool DroneSetup = tuple.Item2;
                        //Echo($"setup: {DroneSetup}");
                        if(!DroneSetup)
                        {
                            IGC.DisableBroadcastListener(_myBroadcastListener_station);
                            _myBroadcastListener_station = IGC.RegisterBroadcastListener(BroadcastTag);
                            _myBroadcastListener_station.SetMessageCallback(BroadcastTag);
                            return;
                        }
                    }
                }
            }
        }




    }
}