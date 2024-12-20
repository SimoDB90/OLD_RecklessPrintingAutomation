using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using VRage;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        readonly string stationVersion = "V: 4.1.3";
        const string lcd_changelog =
            "CHANGELOG VERSION 4.1.3 (26/02/2024):\n" +
            "-added support for toolcore welders;\r\n" +
            "-christo was here;\n" +
            "--------------------------------\n" +
            "CHANGELOG VERSION 4.1.2 (x/11/2023):\n" +
            "-skip command now turns off welders and fancy group;\r\n" +
            "-Improved start command to avoid overheating;\n" +
            "--------------------------------\n" +
            "CHANGELOG VERSION 4.1.1 (5/11/2023):\n" +
            "-small improved to performance;\n" +
            "--------------------------------\n" +
            "CHANGELOG VERSION 4.1.0 (4/11/2023):\n" +
            "-skip command now is \"skip -print\", to start \n" +
            "printing after movement, or \"skip\" to NOT \n" +
            "start printing after movement;\r\n" +
            "-Only hydro tanks are now considered as drone's tanks;\r\n" +
            "-Better and more consistent storage of printing variables;\n" +
            "-Precise Drone movement added;\nNow DroneMovementDistance is the wanted one;" +
            "Fix a bug with untag commands;\n" +
            "-Added an allert for low tank level on log LCD;\n" +
            "-Added an allert for high runtime on log LCD;\n" +
            "-Fixed some visual minor bugs;\n" +
            "-Fixed a bug that prevented to send setup;\n" +
            "-Fixed(?) a bug that stops, sometimes, the rotor;\n" +
            "-Added a QoL command: motion_print, to toggle\nprintwhilemoving variable;\n" +
            "-Improved runtime of script;\n"
            ;

        string droneVersion;
        bool correctVersion = false;
        bool initializedRequired = true;
        bool setupAlreadySent = false;
        readonly MyIni _ini = new MyIni();
        readonly MyCommandLine _commandLine = new MyCommandLine();

        bool setupCompleted;
        readonly string BroadcastTag = "channel_1";
        readonly IMyBroadcastListener _myBroadcastListener_station;
        // Wait variable
        double WaitingCustom;
        const double WaitingDefault = 7;

        const string TagDefault = "[RPA]";
        string TagCustom;

        // christo was here.
        // welders have to be IMyTerminalBlocks now because Toolcore. How lame.
        readonly List<IMyTerminalBlock> WelderList = new List<IMyTerminalBlock>();
        readonly List<IMyTextPanel> LcdList = new List<IMyTextPanel>();

        //stuff for rotor control
        double farestWelderDistance = 0;
        IMyTerminalBlock farestWelder;

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
        const double maxDistanceStopDefault = 180;
        double maxDistanceStopCustom;
        //max movement of the drone
        const double DroneMovDistanceDefault = 2.5;
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
            $"-setup: send CustomData to Drone\n\n" +
            $"-start x y z -toggle: start the process;\nadd name of blocks as arguments\nto ignore them during printing\nAdd -toggle if you want to toggle\n    whence finished;\n" +
            $"-stop: stop the process;\n" +
            $"-ignore_all: force the weld even\n with missing TB;\n" +
            $"-ignore1: skip the active printed block\n" +
            $"-init_d: read the CD of the drone\nand add the tag to the blocks\n" +
            lcd_divider + "\n" +
            $"Utility commands:\n" +
            lcd_divider + "\n" +
            $"-guide -off: in depth commands\nAdd -off only to delete\nthe LCD from the guide\n" +
            $"-align: force the tug to \nalign to the rotor\n" +
            $"-projector: turn on/off the \nDrone's projector\n" +
            $"-skip -printing: force drone to move back;\nAdd -printing if you want to\nprint after movement\n" +
            $"-toggle x y ...: toggle on \nall blocks (no Epsteins or Tools);\nAdd x y ... to IGNORE THESE BLOCKS\n" +
            $"-hudlcd:toggle -reset: toggle on/off\n the hudlcd.\nAdd -reset only to reset it;\n" +
            $"-changelog -off: print the changelog on the STATUS LCD;\nAdd -off to delete it;\n" +
            lcd_divider + "\n" +
            $"Quality of Life Commands:\n" +
            lcd_divider + "\n" +
            $"-rotor_ws x y: changes \nDynamiSpeed(RPM)-RotorSpeed(RPM);\n" +
            $"-drone_move x: changes \nDroneMovementDistance(meters);\n" +
            $"-max_distance x: changes \nMaxDistanceStop(meters);\n" +
            $"-motion_print: toggle weldWhileMoving variable; \n" +
            $"-waiting x: changes the Wait;\n" +
            $"-music -off: play a random music\nAdd -off if want music to stop\n" +
            $"-untag_d x: where x is the tag you\nwant to remove from the drone;\n" +
            $"-untag_s x: where x is the tag you\nwant to remove from the station;\n";

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
            $"skip -printing\n" +
            $"toggle x y z ...\n" +
            $"hudlcd:toggle -reset\n" +
            $"changelog -off\n" +
            lcd_divider + "\n" +
            $"Quality of Life Commands:\n" +
            lcd_divider + "\n" +
            $"rotor_ws x y\n" +
            $"drone_move x\n" +
            $"max_distance x\n" +
            $"motion_print\n" +
            $"waiting x;\n" +
            $"music -off\n" +
            $"untag_d\n" +
            $"untag_s";

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

        //runtime
        readonly Profiler profiler;
        double averageRT = 0;
        public Program()
        {
            profiler = new Profiler(this.Runtime, 240);

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
            setupAlreadySent = false;
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
            commandDict["motion_print"] = MotionPrint;
        }
        public void MotionPrint()
        {
            if (weldWhileMovingCustom) weldWhileMovingCustom = false;
            else weldWhileMovingCustom=true;
            IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool>("motion_print", weldWhileMovingCustom));
            Echo($"Sending message: weldWhileMoving: {weldWhileMovingCustom}\n{commands}");
        }
        public void IgnoreAll()
        {
            if (correctVersion && setupAlreadySent && !initializedRequired)
            {
                IGC.SendBroadcastMessage(BroadcastTag, "ignore_all");
                Echo($"Sending message: ignore_all");
            }
            else if (!setupAlreadySent)
            {
                LCDLog.WriteText($"{header} \nRUN SETUP FIRST");
                DeactivateAll();
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
            if (correctVersion)
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
                    string changelog = lcd_header + "\n" + lcd_changelog;
                    LCDStatus.WriteText(CentreText(changelog, 32));
                }
            }
            catch { }
        }

        // christo was here.
        public void toggleWelders(List<IMyTerminalBlock> Welders, bool State)
        {
            foreach (var welder in WelderList)
            {

                try
                { // maybe it's a welder?
                    (welder as IMyShipWelder).Enabled = State;
                }
                catch
                {
                    try
                    { // lol guess not, must be a sorter...
                        (welder as IMyConveyorSorter).Enabled = State;
                    }
                    catch
                    {
                        // well if that didn't work, nothing will. 
                        // so do nothing
                    }
                }
            }
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
                //debug.CustomData="hudlcd:-.5:.99:0.55";
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
            bool printAfetMove = _commandLine.Switch("printing");
            if (correctVersion && setupAlreadySent && !initializedRequired)
            {
                if (printAfetMove) { IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool>("skip", true)); }
                else { IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool>("skip", false)); }
                
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
        //ignore the actual block been welded and delete it from the list of blocks to weld
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
                if (RotorList == null || RotorList.Count > 1)
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
            List<IMyTerminalBlock> taggedWelders = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlockGroups(WeldersGroupList, x => x.Name.Contains(TagCustom));

            // christo was here.
            // get regular vanilla welders
            GridTerminalSystem.GetBlocksOfType<IMyShipWelder>(WelderList);
            // also get annoying af toolcore weapons
            GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(WelderList, x => 
                // is actually a sorter
                x.BlockDefinition.ToString().Contains("MyObjectBuilder_ConveyorSorter/")
                &&
                // but no, it's a welder lol
                x.BlockDefinition.ToString().Contains("Welder")
            );


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
            profiler.Run();
            //debug.WriteText($"AverageRT(ms): {averageRT}");
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
                    else if (commandDict.TryGetValue(commandString.ToLower(), out commandAction))
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
                averageRT = Math.Round(profiler.RunningAverageMs, 2);
                if (averageRT >= maxRTCustom * 0.3)
                {
                    return;
                }
                //debug.WriteText($"act: {activation}");
                ImListening();
                //debug.WriteText($"AverageRT(ms): {averageRT}");
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
            try
            {
                Rotor.Enabled = false;
                foreach (var lcd in FancyLCDList)
                {
                    lcd.Enabled = false;
                }

                toggleWelders(WelderList, false);

                foreach (var light in FancyLightList)
                {
                    light.Enabled = false;
                }
                foreach (var sound in FancySoundList) { sound.Volume = 0f; sound.Enabled = false; }
            }
            catch
            {
            }
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
                            LCDActive.WriteText($"{status}" +
                            $"{"Station Avg RT",-19}{"=  " + averageRT + " ms",13}\n");
                        }
                        catch { }

                    }
                    if (log == "LogWriting")
                    {

                        string RTString;
                        if (averageRT >= 0.25 * maxRTCustom)
                        {
                            RTString = $"\n{lcd_divider}\n CRITICAL RT: STATION SLOWING DOWN";
                        }
                        else RTString = "";
                        LCDLog.WriteText($"{HeaderCreation()} \n{status}" + $"{RTString}");
                        //CONTINUOS STREAM OF INFOS
                        //debug.WriteText("log");
                        if (Rotor.TargetVelocityRPM != DynamicSpeedCustom)
                        {
                            //debug.WriteText("here");
                            IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool, bool, MatrixD, float>(
                                            "rotorHead", welder_right, welder_forward, rotorHead.WorldMatrix, Rotor.TargetVelocityRPM));
                            //debug.WriteText($"{rotorHead.WorldMatrix}");
                        }

                    }
                    //LOG CREATION DURING START, TO AVOID SENDING ROTOR INFOS!
                    if(log == "StartLog")
                    {
                        LCDLog.WriteText($"{HeaderCreation()} \n{status}");
                    }
                    if (log == "StatusWriting")
                    {
                        try
                        {
                            string LFBlock = "Looking for the Block";
                            string stuckedN = "Unstuck";
                            string fastTrip = "Fast rotation";
                            if (Rotor.TargetVelocityRPM != RotorSpeedCustom && Rotor.TargetVelocityRPM != 0 &&
                                Rotor.TargetVelocityRPM != DynamicSpeedCustom) stuckStatus = LFBlock;
                            if (Rotor.TargetVelocityRPM == RotorSpeedCustom) stuckStatus = stuckedN;
                            if (Rotor.TargetVelocityRPM == 0) stuckStatus = "Welding";
                            if (Rotor.TargetVelocityRPM >= DynamicSpeedCustom-3) stuckStatus = fastTrip;

                            LCDStatus.WriteText($"{StatusLCDHeaderCreation()} \n{status}\n{lcd_divider}\n         WELDERS STATUS\n{lcd_divider}\n{stuckStatus}");

                            Echo(compact_commands);
                            
                        }
                        catch
                        {
                        }
                    }
                    ///LOG LCD
                    if (log == "debug")
                    {
                        try
                        {
                            debug.WriteText(status);
                            //debug.CustomData += status;
                        }
                        catch
                        { }
                    }
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
                    //debug.WriteText($"myString: {myString}");
                    if (myString == "activation")
                    {
                        //debug.WriteText($"here");
                        activation = tuple.Item2;
                        //debug.CustomData += $"\nasd activation: {activation}";
                        if (activation)
                        {
                            //debug.CustomData+=$"\nasd activation: {activation}";
                            Rotor.RotorLock = false;
                            Rotor.Enabled = true;
                            //debug.WriteText($"rotor: {Rotor.Enabled}");
                            foreach (var welder in WelderList)
                            {

                                toggleWelders(WelderList, true);

                            }
                            try
                            {
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
                            catch
                            {
                            }
                        }
                        else if (!activation)
                        {
                            DeactivateAll();
                        }
                    }
                    if (myString == "weldersToggle")
                    {

                        toggleWelders(WelderList, tuple.Item2);

                    }
                    if (myString == "DroneSetup")
                    {
                        bool DroneSetup = tuple.Item2;
                        //Echo($"setup: {DroneSetup}");
                        if (!DroneSetup)
                        {
                            return;
                        }
                    }
                    if (myString == "initRequired")
                    {
                        initializedRequired = tuple.Item2;
                        if (initializedRequired)
                        {
                            return;
                        }
                    }
                    if (myString == "SetupSent")
                    {
                        setupAlreadySent = tuple.Item2;
                    }
                }

            }
        }
        internal sealed class Profiler
        {
            public double RunningAverageMs { get; private set; }
            private double AverageRuntimeMs
            {
                get
                {
                    double sum = runtimeCollection[0];
                    for (int i = 1; i < BufferSize; i++)
                    {
                        sum += runtimeCollection[i];
                    }
                    return (sum / BufferSize);
                }
            }
            /// <summary>Use <see cref="MaxRuntimeMsFast">MaxRuntimeMsFast</see> if performance is a major concern</summary>
            public double MaxRuntimeMs
            {
                get
                {
                    double max = runtimeCollection[0];
                    for (int i = 1; i < BufferSize; i++)
                    {
                        if (runtimeCollection[i] > max)
                        {
                            max = runtimeCollection[i];
                        }
                    }
                    return max;
                }
            }
            public double MaxRuntimeMsFast { get; private set; }
            public double MinRuntimeMs
            {
                get
                {
                    double min = runtimeCollection[0];
                    for (int i = 1; i < BufferSize; i++)
                    {
                        if (runtimeCollection[i] < min)
                        {
                            min = runtimeCollection[i];
                        }
                    }
                    return min;
                }
            }
            public int BufferSize { get; }

            private readonly double bufferSizeInv;
            private readonly IMyGridProgramRuntimeInfo runtimeInfo;
            private readonly double[] runtimeCollection;
            private int counter = 0;

            /// <summary></summary>
            /// <param name="runtimeInfo">Program.Runtime instance of this script.</param>
            /// <param name="bufferSize">Buffer size. Must be 1 or higher.</param>
            public Profiler(IMyGridProgramRuntimeInfo runtimeInfo, int bufferSize = 300)
            {
                this.runtimeInfo = runtimeInfo;
                this.MaxRuntimeMsFast = runtimeInfo.LastRunTimeMs;
                this.BufferSize = MathHelper.Clamp(bufferSize, 1, int.MaxValue);
                this.bufferSizeInv = 1.0 / BufferSize;
                this.runtimeCollection = new double[bufferSize];
                this.runtimeCollection[counter] = runtimeInfo.LastRunTimeMs;
                this.counter++;
            }

            public void Run()
            {
                RunningAverageMs -= runtimeCollection[counter] * bufferSizeInv;
                RunningAverageMs += runtimeInfo.LastRunTimeMs * bufferSizeInv;

                runtimeCollection[counter] = runtimeInfo.LastRunTimeMs;

                if (runtimeInfo.LastRunTimeMs > MaxRuntimeMsFast)
                {
                    MaxRuntimeMsFast = runtimeInfo.LastRunTimeMs;
                }

                counter++;

                if (counter >= BufferSize)
                {
                    counter = 0;
                    //Correct floating point drift
                    RunningAverageMs = AverageRuntimeMs;
                    MaxRuntimeMsFast = runtimeInfo.LastRunTimeMs;
                }
            }
        }
        


    }
}
