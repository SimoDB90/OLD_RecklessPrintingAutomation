using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {

        readonly string droneVersion = "V: 4.1.3";
        readonly MyIni _ini = new MyIni();
        double Wait;
        double ImWait = 7;
        IMyBroadcastListener _myBroadcastListener;
        bool setupCompleted; //setup successfull, all blocks are tagged
        bool initializedRequired = true; //need to run setup?
        bool setupAlreadySent = false;
        readonly List<IMyCockpit> CockpitList = new List<IMyCockpit>();
        readonly List<IMyProjector> ProjectorList = new List<IMyProjector>();
        readonly List<IMyThrust> ThrustersList = new List<IMyThrust>();
        readonly List<IMyThrust> forwardThrusters = new List<IMyThrust>();
        readonly List<IMyThrust> NestedThrusters = new List<IMyThrust>();
        readonly List<IMyRadioAntenna> antennaList = new List<IMyRadioAntenna>();
        List<IMyGasTank> tank = new List<IMyGasTank>();
        MyDefinitionId HydrogenGasId = MyDefinitionId.Parse("MyObjectBuilder_GasProperties/Hydrogen");
        MyResourceSinkComponent HydroType;

        IMyShipController Cockpit;
        IMyProjector Projector;
        readonly string BroadcastTag = "channel_1";

        const string TagDefault = "[RPA]";

        Vector3D start;

        float thrust;
        float mass;
        readonly float acceleration = 0.4f; //wanted acceleration in m/s^2
        float maxBreakingThrust;
        float maxDecel;
        string TagCustom;
        int ThrustersInGroup = 0;

        ///safety distance to force stop
        double maxDistanceStop;
        double safetyDistanceStop = 0;
        
        //movement of the drone
        double DroneMovDistance = 2.5f;

        /// Rotor's setting
        bool firstRotation = true;
        float DynamicSpeed = 20f;
        float RotorSpeed;
        float newRotorSpeed;
        MatrixD rotorMatrix;
        MatrixD rotorOrientation = MatrixD.Zero;
        Vector3D rotorPosition;
        MatrixD rotorHeadMatrix;
        Vector3D rotorFacing;
        bool welder_right;
        bool welder_forward;
        double rotorSpeed = 0;
        int welderSign;//check orientation between welders and rotor
        const double rotorToll = 0.04;
        double angle;
        Vector3D planeNormal;

        bool skip = false; //bool for skip command
        Vector3D skipStartingDist;
        bool ignore1TB = false;
        bool ignoreTB = false; // update all non finished blocks-->delete the from the list and go ahead
        int refreshBlocks = 0;
        int ignoredBlocks = 0;
        const double firstRotationTimeMult = 0.3;
        //weld while moving --> during movement, welders are off
        bool weldWhileMoving = false;
        bool printAfterSkip = false;

        //lcd printing strings
        string[] variablesStatus = new string[] { " Running", " Set" };
        string[] variables = new string[] { "Loaded BP data:", "Set starting variables:", "Aligning Drone:", "Reckless wishes you a good printing", "3...", "2...", "1..." };
        readonly string[] lcd_printing_spinners = new string[] { "P", "PR", "PRI", "PRIN", "PRINT", "PRINTI", "PRINTIN", "PRINTING", "PRINTING.", "PRINTING..." };
        double lcd_printing_spinner_status;
        readonly string[] lcd_moving_spinners = new string[] { "MO", "MOVI", "MOVING.", "MOVING..." };
        double lcd_moving_spinner_status;
        double spinningWaitingStatus;
        const string lcd_divider = "--------------------------------";
        const string lcd_title = "  RECKLESS PRINTING AUTOMATION  ";
        const string lcd_h2_level = "         TUG H2 LEVEL";
        const string lcd_proj_level = "     PROJECTION LEVEL";
        string lcd_header;
        bool imMoving = false; //check if the drone is moving
        bool preciseMoving = false;
        double velocity;
        //string printingStatus;
        int totBlocks = 100000;
        bool imProjecting;
        float totBlockPercentage;
        int totBlockMultiplier;
        int tankMultiplier;
        //gyro list
        readonly List<IMyGyro> imGyroList = new List<IMyGyro>();
        bool aligningBool; // is drone aligning?
        bool weldersToggleOn; // have to toggle on welders?
        bool activation; // have to activate fancy rpa??
        bool setupCommand; //are gyros aligning for the setup? (if false, is after every movement)
        bool startCommand; //for alingment purpose at the start
        bool activePrinting; //are TB integrity increasing?
        bool printing = false; //when start is sent
        double timeStep = 0;
        readonly double gyrosTolerance = 0.05; // 2,8 degrees
        readonly double maxGyroRotation = 0.43; // 25 degress
        //integrity check stuff
        double time = 0;
        //double checkTime = 0;
        int remainingTB = 0;
        int builtBlocks = 0;
        int sectionsBuilt = 0;
        int averageTime = 0;
        int totTime = 0;
        int maxTime = 0;
        int minTime = 100;
        int totRemaining = 0;
        int endingBlocks = 0;
        int averageBlocks = 0;
        readonly List<IMyTerminalBlock> integrityListT0 = new List<IMyTerminalBlock>(); //list of all not 100 integrity blocks and not ignored blocks
        readonly List<IMyTerminalBlock> ignoringList = new List<IMyTerminalBlock>(); //list of not 100% integrity blocks ignored

        //Status LCD lIST
        readonly StringBuilder printingStatus = new StringBuilder();
        readonly StringBuilder activeOuput = new StringBuilder();
        //
        IMyTerminalBlock activeWeldedBlockName;
        float activeWeldedBlockIntegrity = 0;
        bool toggleAfterFinish = false; //auto toggle after finish printing, sent by start -toggle command
        readonly ImmutableList<string>.Builder toggleBuilder = ImmutableList.CreateBuilder<string>();
        ImmutableList<string> toggleList;
        readonly ImmutableList<string>.Builder startBuilder = ImmutableList.CreateBuilder<string>();
        ImmutableList<string> startIgnoringBlocks;

        //runtime
        Profiler profiler;
        double averageRT = 0;
        double maxRT = 0;
        double maxRTCustom = 0.5;

        //init Timer State machine
        SimpleTimerSM timerSM;
        SimpleTimerSM statusLCDStateMachine;
        SimpleTimerSM starterPrint;
        readonly StringBuilder starterString = new StringBuilder();
        const double ticksToSeconds = 1d / 60d; //multiplying it by X seconds, returns the number of ticks
        readonly int[] multTickList = new int[] { 1, 2, 3 };
        int multTicks = 1;

        public Program()
        {
            _myBroadcastListener = IGC.RegisterBroadcastListener(BroadcastTag);
            _myBroadcastListener.SetMessageCallback(BroadcastTag);
            profiler = new Profiler(this.Runtime, 240);
            timerSM = new SimpleTimerSM(this, SequenceConditionalRotorSpeed());
            statusLCDStateMachine = new SimpleTimerSM(this, sequence: StatusLCD());
            starterPrint = new SimpleTimerSM(this, sequence: StartPrinting());
            Starter();
        }
        public void Main(string argument, UpdateType updateSource)
        {
            //IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, string>("debug", $"tunrime: {Runtime.TimeSinceLastRun}"));
            profiler.Run();
            timerSM.Run();
            statusLCDStateMachine.Run();
            averageRT = Math.Round(profiler.RunningAverageMs, 2);
            maxRT = Math.Round(profiler.MaxRuntimeMs, 2);
            //Echo($"AverageRT(ms): {averageRT}\nMaxRT(ms): {maxRT}\n" +
            //    $"Ticks Mult: {multTicks}");
            //IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, string>("debug", $"AverageRT(ms): {averageRT}\nMaxRT(ms): {maxRT}\n"));
            if (averageRT <= 0.2 * maxRTCustom)
            {
                multTicks = multTickList[0];
            }
            if (averageRT > 0.2 * maxRTCustom && averageRT <= 0.4 * maxRTCustom)
            {
                multTicks = multTickList[1];
            }
            if (averageRT > 0.4 * maxRTCustom && averageRT <= 0.55 * maxRTCustom)
            {
                multTicks = multTickList[2];
            }
            if (averageRT > 0.55 * maxRTCustom)
            {
                multTicks = multTickList[2];
                return;
            }
            if ((updateSource & (UpdateType.Trigger | UpdateType.Terminal)) > 0)
            {
                if (argument.ToLower() == "init_d" && !printing)
                {
                    CustomData();
                    SetupBlocks();
                    if (setupCompleted)
                    {
                        Echo($"DRONE SETUP COMPLETED!\nVersion: {droneVersion}\nNumbers of thrusters in group: {ThrustersInGroup}\nCockpit Found \nProjector Found \nFuel Tank: {tank.Count}\nTag used: {TagCustom}");
                        IGC.SendBroadcastMessage(BroadcastTag, $"    |DRONE SETUP COMPLETED!\n|Version: {droneVersion}\n|Numbers of active thrusters: {ThrustersInGroup} " +
                            $"\n|Cockpit Found \n|Projector Found\n|Fuel Tank: {tank.Count}\n|Tag used: [{TagCustom}]");
                    }
                }
                if (argument.ToLower() == "stop")
                {
                    Echo($"Stopping");
                    printing = false; //stop the print-->for the main
                    Stop();
                    SaveVariables();
                }
            }
            if (startCommand)
            {
                starterPrint.Run();
            }
            if (skip && !startCommand)
            {
                //Echo($"precMov: {preciseMoving}\nprint {printAfterSkip}\nskip: {skip}");
                PreciseMovement(Cockpit, ThrustersList, totRemaining, remainingTB, skipStartingDist);
                if (preciseMoving == false && printAfterSkip)
                {
                    //IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, string>("debug", $"2=\nprinting: {printing}\nprecMov: {preciseMoving}\nskip: {skip}"));
                    //Echo($"precMov: {preciseMoving}\nprint {printAfterSkip}");
                    printing = true;
                    time = 0;
                    //PrintingBlocksListCreation();
                    statusLCDStateMachine.Start();
                    timerSM.Start();
                    statusLCDStateMachine.AutoStart = true;
                    timerSM.AutoStart = true;
                    setupCommand = false;
                    skip = false;
                }
            }
            if (printing && !startCommand && !skip)
            {
                if (preciseMoving)
                {
                    //Echo($"main precMov: {preciseMoving}");
                    PreciseMovement(Cockpit, ThrustersList, totRemaining, remainingTB, start);
                }
                if (aligningBool)
                {
                    //Echo("aligning");
                    ImAligning();
                }
                if (Wait >= firstRotationTimeMult * ImWait && firstRotation && !aligningBool && !preciseMoving)
                {
                    time = 0;
                    activePrinting = false;
                    //IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, string>("debug", $"OUT == rot speed: {rotorSpeed}"));
                    if (rotorSpeed < DynamicSpeed - 1d || rotorSpeed > DynamicSpeed + 1d)
                    {
                        //IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, string>("debug", $"IN == rot speed: {rotorSpeed}"));
                        RotorSpeedingUp();
                    }
                    ActionTime(Cockpit);
                }
                if ((Wait < firstRotationTimeMult * ImWait || !firstRotation) && !aligningBool && !setupCommand && !preciseMoving)
                {
                    //IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, string>("debug", $"entering"));
                    if (firstRotation)
                    {
                        firstRotation = false; 
                    }
                    ActionTime(Cockpit);
                    IGC.SendBroadcastMessage(BroadcastTag, newRotorSpeed);
                }
            }
            if (!printing && !skip && !startCommand)
            {
                timerSM.AutoStart = false;
                statusLCDStateMachine.AutoStart = false;
                timerSM.Stop();
                statusLCDStateMachine.Stop();
                activation = false;
                starterPrint.Stop();
                IGC.SendBroadcastMessage(BroadcastTag, newRotorSpeed = 0);
                IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool>("activation", activation));
                foreach (var gyro in imGyroList) { gyro.GyroOverride = false; }
                Runtime.UpdateFrequency = UpdateFrequency.None;
                SaveVariables();
            }
            if ((updateSource & UpdateType.IGC) > 0)
            {
                ImListening(Cockpit, Projector);
            }
        }
        public void Starter()
        {
            lcd_header = $"{lcd_divider}\n{lcd_title}\n{lcd_divider}";
            start = Me.GetPosition();
            Echo("Drone Log:\n");
            ///CHECK SETUP//////////////////////
            CustomData();
            setupAlreadySent = false;
            IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool>("SetupSent", setupAlreadySent));
            IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, string>("droneVersion", droneVersion));
            initializedRequired = CheckInit();
            IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool>("initRequired", initializedRequired));
            if (!initializedRequired) LoadStoredData();

            else
            {
                totRemaining = 0;
                totTime = 0;
                averageTime = 0;
                builtBlocks = 0;
                averageBlocks = 0;
                refreshBlocks = 0;
                sectionsBuilt = 0;
                ignoredBlocks = 0;
            }
        }
        public void SaveVariables()
        {
            _ini.Clear();
            _ini.Set("printing", "totBlocks", totBlocks);
            _ini.Set("printing", "totRemaining", totRemaining);
            _ini.Set("printing", "totTime", totTime);
            _ini.Set("printing", "averageTime", averageTime);
            _ini.Set("printing", "builtBlocks", builtBlocks);
            _ini.Set("printing", "averageBlocks", averageBlocks);
            _ini.Set("printing", "refreshBlocks", refreshBlocks);
            _ini.Set("printing", "sectionsBuilt", sectionsBuilt);
            _ini.Set("printing", "ignoredBlocks", ignoredBlocks);
            Storage = _ini.ToString();
            //Echo($"storage: {Storage}");
        }
        public void LoadStoredData()
        {
            if(_ini.TryParse(Storage))
            {
                totBlocks = _ini.Get("printing", "totBlocks").ToInt32();
                totRemaining = _ini.Get("printing", "totRemaining").ToInt32();
                totTime = _ini.Get("printing", "totTime").ToInt32();
                averageTime = _ini.Get("printing", "averageTime").ToInt32();
                builtBlocks = _ini.Get("printing", "builtBlocks").ToInt32();
                averageBlocks = _ini.Get("printing", "averageBlocks").ToInt32();
                refreshBlocks = _ini.Get("printing", "refreshBlocks").ToInt32();
                sectionsBuilt = _ini.Get("printing", "sectionsBuilt").ToInt32();
                ignoredBlocks = _ini.Get("printing", "ignoredBlocks").ToInt32();
                if (Projector.IsProjecting)
                {
                    var totalBlocks = TotalBlocks();
                    var totalRemaining = Projector.RemainingBlocks - refreshBlocks;

                    if (totalBlocks == totBlocks && totalRemaining == totRemaining)
                    {
                        //IGC.SendBroadcastMessage(BroadcastTag, "Stored Data loaded for this BP;");
                        Echo($"Load Stored Data:\nNumber of sections= {sectionsBuilt}\nTotal printing Time= {totTime}\n" +
                            $"Average Time= {averageTime}\nAverage blocks/section= {averageBlocks}\nSkipped Blocks= {ignoredBlocks}");
                    }
                    else
                    {
                        totRemaining = 0;
                        totTime = 0;
                        averageTime = 0;
                        builtBlocks = 0;
                        averageBlocks = 0;
                        refreshBlocks = 0;
                        sectionsBuilt = 0;
                        ignoredBlocks = 0;
                        Echo($"No Stored Data to load for this BP;");
                        //IGC.SendBroadcastMessage(BroadcastTag, "No Stored Data to load for this BP;");
                    }
                }
                else
                {
                    Echo($"Projector is off: turn it on to load Data;");
                    //IGC.SendBroadcastMessage(BroadcastTag, "Projector is off: turn it on to load Data;");
                    return;
                }
            }
            else
                {

                totRemaining = 0;
                totTime = 0;
                averageTime = 0;
                builtBlocks = 0;
                averageBlocks = 0;
                refreshBlocks = 0;
                sectionsBuilt = 0;
                ignoredBlocks = 0;
                Echo($"Can't parse the Storage;");
                //IGC.SendBroadcastMessage(BroadcastTag, "No Stored Data to load for this BP;");
            }
        }
        public void UntagDrone(string tag)
        {
            List<IMyTerminalBlock> everything = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType(everything, x => x.CustomName.Contains(tag));
            //Echo($"{everything.Count}");
            if (everything != null && everything.Count > 0)
            {
                foreach (var block in everything)
                {

                    block.CustomName = block.CustomName.Trim();
                    block.CustomName = block.CustomName.Replace(tag, "");
                    IGC.SendBroadcastMessage(BroadcastTag, $"Tag {tag} removed from\n{everything.Count} Drone's blocks");
                }
            }
            else if (everything==null || everything.Count == 0)
            {
                //Echo($"asd: {everything.Count}");
                IGC.SendBroadcastMessage(BroadcastTag, $"No Tag: {tag} found in the Drone");
            }
        }
        public void CustomData()
        {
            //////////////////
            //get and set for customdata

            bool wasParsed = _ini.TryParse(Me.CustomData);
            TagCustom = _ini.Get("data", "TAG").ToString(TagDefault);

            if (!wasParsed)
            {
                _ini.Clear();
            }
            // Set the values to make sure they exist. They could be missing even when parsed ok.
            _ini.Set("data", "TAG", TagCustom);
            Me.CustomData = _ini.ToString();
            ///////////////////////////
        }
        public void SetupBlocks()
        {
            Me.CustomData += _ini.DeleteSection("Do No Change lines below");
            //gyros
            GridTerminalSystem.GetBlocksOfType(imGyroList);
            foreach (var gyro in imGyroList) { gyro.Enabled = true; }

            //hydrogen tank
            GridTerminalSystem.GetBlocksOfType(tank, x => x.CustomName.Contains(TagCustom));
            if (tank != null && tank.Count > 0)
            {
                List<IMyGasTank> tempList = new List<IMyGasTank>();
                foreach (var t in tank)
                {
                    HydroType = t.Components.Get<MyResourceSinkComponent>();
                    if (HydroType != null && HydroType.AcceptedResources.Contains(HydrogenGasId))
                    {
                        tempList.Add(t);
                    }
                    else if(!HydroType.AcceptedResources.Contains(HydrogenGasId))
                    {
                        t.CustomName += t.CustomName.Replace(TagCustom, "");
                    }
                }
                if(tempList!=null && tempList.Count>0)
                {
                    tank.Clear();
                    tank = tempList;
                    _ini.Set("Do No Change lines below", "Tank", tank.Count);
                }
            }
            if (tank == null || tank.Count == 0)
            {
                GridTerminalSystem.GetBlocksOfType(tank);
                List<IMyGasTank> tempList = new List<IMyGasTank>();
                if (tank != null && tank.Count>0)
                {
                    foreach (var t in tank)
                    {
                        HydroType = t.Components.Get<MyResourceSinkComponent>();
                        if (HydroType != null && HydroType.AcceptedResources.Contains(HydrogenGasId))
                        {
                            tempList.Add(t);
                        }
                    }
                }
                else if(tank == null || tank.Count == 0)
                {
                    Echo("SETUP NOT COMPLETED: Add one Fuel tank for your Tug beratna.. come on you weirdo");
                    IGC.SendBroadcastMessage(BroadcastTag, $"{lcd_header}\n   SETUP NOT COMPLETED:\nAdd one Fuel tank for your Tug beratna.. come on you weirdo");
                    IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool>("DroneSetup", setupCompleted));
                    return;
                }
                if (tempList != null && tempList.Count > 1)
                {
                    Echo("SETUP NOT COMPLETED: If you have more than 1 tank, tag them");
                    IGC.SendBroadcastMessage(BroadcastTag, $"{lcd_header}\n   SETUP NOT COMPLETED:\nIf you have more than 1 tank, tag them");
                    IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool>("DroneSetup", setupCompleted));
                    return;
                }
                if (tempList == null || tempList.Count == 0)
                {
                    Echo("SETUP NOT COMPLETED: Add one Fuel tank for your Tug beratna.. come on you weirdo");
                    IGC.SendBroadcastMessage(BroadcastTag, $"{lcd_header}\n   SETUP NOT COMPLETED:\nAdd one Fuel tank for your Tug beratna.. come on you weirdo");
                    IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool>("DroneSetup", setupCompleted));
                    return;
                }
                if (tempList != null && tempList.Count == 1)
                {
                    IMyGasTank Tank = tempList[0];
                    tank.Clear();
                    tank = tempList;
                    if (!Tank.CustomName.Contains(TagCustom))
                    {
                        Tank.CustomName += "." + TagCustom;
                    }
                    _ini.Set("Do No Change lines below", "Tank", 1);
                }
            }

            //Antenna setup
            GridTerminalSystem.GetBlocksOfType(antennaList);
            if (antennaList == null || antennaList.Count == 0)
            {
                Echo($"No Antenna found. Please, add one Antenna");
                IGC.SendBroadcastMessage(BroadcastTag, $"   SETUP NOT COMPLETED:\nNo Antenna found. Please, add one Antenna");
                IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool>("DroneSetup", setupCompleted));
                return;
            }
            var antenna = antennaList[0];
            antenna.Enabled = true;
            antenna.EnableBroadcasting = true;
            antenna.Radius = 1000;
            _ini.Set("Do No Change lines below", "Antenna", 1);

            //SET COCKPIT, IF ONLY ONE, ASSIGN AUTOMATICALLY THE TAG
            GridTerminalSystem.GetBlocksOfType(CockpitList);
            if (CockpitList != null && CockpitList.Count == 1)
            {
                Cockpit = CockpitList[0];
                if (!Cockpit.CustomName.Contains(TagCustom))
                {
                    Cockpit.CustomName += "." + TagCustom;
                }
                _ini.Set("Do No Change lines below", "Cockpit", 1);

            }
            else if (CockpitList.Count > 1)
            {
                GridTerminalSystem.GetBlocksOfType(CockpitList, x => x.CustomName.Contains(TagCustom));
                if (CockpitList == null || CockpitList.Count > 1 || !CockpitList.Any())
                {
                    Echo($"No Cockpit found or more than 1 Cockpit found \nUse [{TagDefault}] tag, or change it in Custom Data");
                    IGC.SendBroadcastMessage(BroadcastTag, $"   SETUP NOT COMPLETED:\nNo Cockpit found (mandatory) or\n     more than 1 Cockpit found \nUse [{TagDefault}] tag, or change it in Custom Data");
                    IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool>("DroneSetup", setupCompleted));
                    return;
                }
                Cockpit = CockpitList[0];
                Cockpit.DampenersOverride = true;
                _ini.Set("Do No Change lines below", "Cockpit", 1);
            }
            // SET PROJECTOR: IF ONLY ONE, ASSIGN THE TAG AUTOMATICALLY
            GridTerminalSystem.GetBlocksOfType(ProjectorList);
            if (ProjectorList != null && ProjectorList.Count == 1)
            {
                Projector = ProjectorList[0];
                if (!Projector.CustomName.Contains(TagCustom))
                {
                    Projector.CustomName += "." + TagCustom;
                }
                _ini.Set("Do No Change lines below", "Projector", 1);
            }
            else if (ProjectorList.Count > 1)
            {
                GridTerminalSystem.GetBlocksOfType(ProjectorList, x => x.CustomName.Contains(TagCustom));
                if (ProjectorList == null || ProjectorList.Count > 1 || !ProjectorList.Any())
                {
                    Echo($"No Projector found or more than 1 Projector found \nUse [{TagDefault}] tag, or change it in Custom Data");
                    IGC.SendBroadcastMessage(BroadcastTag, $"   SETUP NOT COMPLETED:\nNo Projector found (mandatory) or\n     more than 1 Projector found \nUse [{TagDefault}] tag, or change it in Custom Data");
                    IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool>("DroneSetup", setupCompleted));
                    return;
                }
                Projector = ProjectorList[0];
                _ini.Set("Do No Change lines below", "Projector", 1);
            }

            //SET THRUSTERS
            List<IMyBlockGroup> ThrustersGroupsList = new List<IMyBlockGroup>();
            GridTerminalSystem.GetBlockGroups(ThrustersGroupsList, x => x.Name.Contains(TagCustom));
            GridTerminalSystem.GetBlocksOfType(ThrustersList, t =>
            t.Orientation.Forward == Cockpit.Orientation.Forward);

            if (ThrustersList != null && ThrustersList.Count > 0 && ThrustersGroupsList.Count == 0)
            {
                foreach (var thruster in ThrustersList.Where(t => !t.CustomName.Contains(TagCustom)))
                {
                    thruster.CustomName += "." + TagCustom;
                }
                _ini.Set("Do No Change lines below", "Thrusters", ThrustersList.Count);
            }
            List<IMyThrust> blockTagged = new List<IMyThrust>();
            GridTerminalSystem.GetBlocksOfType(blockTagged, t => t.CustomName.Contains(TagCustom));

            if (blockTagged.Count > 0 && (ThrustersGroupsList == null || ThrustersGroupsList.Count > 0))
            {
                Echo($"Thrusters tagged individually AND\nThrusters Group Found:\n  CHOOSE ONE OR ANOTHER!");
                IGC.SendBroadcastMessage(BroadcastTag, $"   SETUP NOT COMPLETED:\nThrusters tagged individually AND\nThrusters Group Found:\n  CHOOSE ONE OR ANOTHER!");
                IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool>("DroneSetup", setupCompleted));
                return;
            }

            if (blockTagged.Count == 0 && (ThrustersGroupsList == null || ThrustersGroupsList.Count == 0))
            {
                Echo($"Not Backward Thrusters group OR\nTagged Thrusters");
                IGC.SendBroadcastMessage(BroadcastTag, $"   SETUP NOT COMPLETED:\nNot Backward Thrusters group OR\nTagged Thrusters");
                IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool>("DroneSetup", setupCompleted));
                return;
            }
            if (blockTagged.Count == 0 && (ThrustersGroupsList == null || ThrustersGroupsList.Count > 1))
            {
                Echo($"More than 1 Backward Thruster Group Tagged!\nDelete one");
                IGC.SendBroadcastMessage(BroadcastTag, $"   SETUP NOT COMPLETED:\nMore than 1 Backward Thruster Group!\nDelete one");
                IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool>("DroneSetup", setupCompleted));
                return;
            }

            if (ThrustersGroupsList != null && ThrustersGroupsList.Count == 1)
            {
                ThrustersList.Clear();
                ThrustersGroupsList[0].GetBlocksOfType(ThrustersList, x => x.Orientation.Forward == Cockpit.Orientation.Forward);
                if (ThrustersList == null || ThrustersList.Count == 0)
                {
                    Echo($"Tagged Thrusters group has not only:\n        Backward Thrusters!");
                    IGC.SendBroadcastMessage(BroadcastTag, $"   SETUP NOT COMPLETED:\nTagged Thrusters group has not only:\n        Backward Thrusters!");
                    IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool>("DroneSetup", setupCompleted));
                    return;
                }
                _ini.Set("Do No Change lines below", "Thrusters", ThrustersList.Count);
            }
            if (ThrustersList != null && ThrustersList.Count > 0)
            {
                ThrustersInGroup = ThrustersList.Count;
            }
            if (blockTagged != null && blockTagged.Count > 0)
            {
                ThrustersInGroup = blockTagged.Count;
            }
            //Calculate max thrust
            maxBreakingThrust = 0;
            GridTerminalSystem.GetBlocksOfType(forwardThrusters, x => x.WorldMatrix.Forward == Cockpit.WorldMatrix.Backward && x.IsWorking);
            foreach (var t in forwardThrusters)
            {
                maxBreakingThrust += t.MaxEffectiveThrust;
            }

            //finish setting up blocks
            Me.CustomData = _ini.ToString();

            remainingTB = 0;
            builtBlocks = 0;
            maxTime = 0;
            minTime = 100;
            endingBlocks = 0;
            averageBlocks = 0;
            LoadStoredData();
            //SETUP COMPLETED
            setupCompleted = true;
            lcd_header = $"{lcd_divider}\n{lcd_title}\n{lcd_divider}";
            start = Me.GetPosition();
            Echo("Drone Log:\n");
            initializedRequired = CheckInit();
            profiler = new Profiler(this.Runtime, 240);
            timerSM = new SimpleTimerSM(this, SequenceConditionalRotorSpeed());
            statusLCDStateMachine = new SimpleTimerSM(this, sequence: StatusLCD());
            starterPrint = new SimpleTimerSM(this, sequence: StartPrinting());
            _myBroadcastListener = IGC.RegisterBroadcastListener(BroadcastTag);
            _myBroadcastListener.SetMessageCallback(BroadcastTag);
            //sending the version of the script to the station
            IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, string>("droneVersion", droneVersion));
            IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool>("initRequired", initializedRequired));
            setupAlreadySent = false;
            IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool>("SetupSent", setupAlreadySent));
        }
        //check initialization when recompiling
        public bool CheckInit()
        {
            //CHECK CUSTOM DATA
            bool initializedParsed = _ini.TryParse(Me.CustomData);
            int tankCount = 0;
            int AntennaCount = 0;
            int CockpitCount = 0;
            int ProjectorCount = 0;
            int ThrustersCount = 0;
            if (initializedParsed)
            {
                _ini.Get("Do No Change lines below", "Tank").TryGetInt32(out tankCount);
                AntennaCount = _ini.Get("Do No Change lines below", "Antenna").ToInt32();
                CockpitCount = _ini.Get("Do No Change lines below", "Cockpit").ToInt32();
                ProjectorCount = _ini.Get("Do No Change lines below", "Projector").ToInt32();
                ThrustersCount = _ini.Get("Do No Change lines below", "Thrusters").ToInt32();
            }
            if ((tankCount == 0 && AntennaCount == 0 && CockpitCount == 0 && ProjectorCount == 0 && ThrustersCount == 0) || !initializedParsed)
            {
                Echo("Initialization required;\nSet tag in CD, then run \"init_d\"");
                IGC.SendBroadcastMessage(BroadcastTag, "Initialization required;\nSet tag in CD, then run \"init_d\"");
                return true;
            }
            if (tankCount == 0 || AntennaCount == 0 || CockpitCount == 0 || ProjectorCount == 0 || ThrustersCount == 0)
            {
                Echo("Initialization required;\nSet tag in CD, then run \"init_d\"");
                IGC.SendBroadcastMessage(BroadcastTag, "Initialization required;\nSet tag in CD, then run \"init_d\"");
                return true;
            }
            //CHECK BLOCKS AND COMPARE WITH CUSTOM DATA
            //hydro tanks
            GridTerminalSystem.GetBlocksOfType(tank, x => x.CustomName.Contains(TagCustom));
            List<IMyGasTank> tempList = new List<IMyGasTank>();
            if (tank!=null && tank.Count>0)
            {
                foreach (var t in tank)
                {
                    HydroType = t.Components.Get<MyResourceSinkComponent>();
                    if (HydroType != null && HydroType.AcceptedResources.Contains(HydrogenGasId))
                    {
                        tempList.Add(t);
                    }
                } 
            }
            if (tank == null || tank.Count == 0)
            {
                Echo("Tank failed to set up correctly;\nInitialization required:\nSet tag in CD, then run \"init_d\"");
                IGC.SendBroadcastMessage(BroadcastTag, "Tank failed to set up correctly;\nInitialization required:\nSet tag in CD, then run \"init_d\"");
                return true;
            }
            if (tempList == null || tempList.Count != tankCount)
            {
                Echo("Tank failed to set up correctly;\nInitialization required:\nSet tag in CD, then run \"init_d\"");
                IGC.SendBroadcastMessage(BroadcastTag, "Tank failed to set up correctly;\nInitialization required:\nSet tag in CD, then run \"init_d\"");
                return true;
            }
            tank.Clear();
            tank = tempList;

            //Antenna
            GridTerminalSystem.GetBlocksOfType(antennaList);
            if (antennaList == null || antennaList.Count < AntennaCount)
            {
                Echo("Antenna failed to set up correctly;\nInitialization required:\nSet tag in CD, then run \"init_d\"");
                IGC.SendBroadcastMessage(BroadcastTag, "Antenna failed to set up correctly;\nInitialization required:\nSet tag in CD, then run \"init_d\"");
                return true;
            }
            foreach (var a in antennaList) a.EnableBroadcasting = true;
            //Cockpit
            GridTerminalSystem.GetBlocksOfType(CockpitList, x => x.CustomName.Contains(TagCustom));
            if (CockpitList == null || CockpitList.Count != CockpitCount)
            {
                Echo("Cockpit failed to set up correctly;\nInitialization required:\nSet tag in CD, then run \"init_d\"");
                IGC.SendBroadcastMessage(BroadcastTag, "Cockpit failed to set up correctly;\nInitialization required:\nSet tag in CD, then run \"init_d\"");
                return true;
            }
            Cockpit = CockpitList[0];
            Cockpit.DampenersOverride = true;
            //Projector
            GridTerminalSystem.GetBlocksOfType(ProjectorList, x => x.CustomName.Contains(TagCustom));
            if (ProjectorList == null || ProjectorList.Count != ProjectorCount)
            {
                Echo("Projector failed to set up correctly;\nInitialization required:\nSet tag in CD, then run \"init_d\"");
                IGC.SendBroadcastMessage(BroadcastTag, "Projector failed to set up correctly;\nInitialization required:\nSet tag in CD, then run \"init_d\"");
                return true;
            }
            Projector = ProjectorList[0];
            //THRUSTERS CHECK
            GridTerminalSystem.GetBlocksOfType(ThrustersList, t =>
            t.Orientation.Forward == Cockpit.Orientation.Forward
            &&
            t.CustomName.Contains(TagCustom));
            //Echo($"Thrusters: {ThrustersList.Count}\nThrustersCount: {ThrustersCount}");
            if (ThrustersList == null || ThrustersList.Count != ThrustersCount)
            {
                List<IMyBlockGroup> ThrustersGroupsList = new List<IMyBlockGroup>();

                GridTerminalSystem.GetBlockGroups(ThrustersGroupsList, t => t.Name.Contains(TagCustom));
                if (ThrustersGroupsList != null && ThrustersGroupsList.Count == 1)
                {
                    ThrustersGroupsList[0].GetBlocksOfType(NestedThrusters, x => x.Orientation.Forward == Cockpit.Orientation.Forward);
                    //Echo($"Thrusters: {NestedThrusters.Count}\nThrustersCount: {ThrustersCount}");
                    if (NestedThrusters == null || NestedThrusters.Count != ThrustersCount)
                    {
                        Echo("Thrusters failed to set up correctly;\nInitialization required:\nSet tag in CD, then run \"init_d\"");
                        IGC.SendBroadcastMessage(BroadcastTag, "Thrusters failed to set up correctly;\nInitialization required:\nSet tag in CD, then run \"init_d\"");
                        return true;
                    }
                }
                if (ThrustersGroupsList == null && (ThrustersGroupsList.Count == 0 || ThrustersGroupsList.Count > 1))
                {
                    Echo("Thrusters failed to set up correctly;\nInitialization required:\nSet tag in CD, then run \"init_d\"");
                    IGC.SendBroadcastMessage(BroadcastTag, "Thrusters failed to set up correctly;\nInitialization required:\nSet tag in CD, then run \"init_d\"");
                    return true;
                }
                if ((ThrustersList == null && NestedThrusters == null) || (ThrustersList.Count != ThrustersCount && NestedThrusters.Count != ThrustersCount))
                {
                    Echo("Thrusters failed to set up correctly;\nInitialization required:\nSet tag in CD, then run \"init_d\"");
                    IGC.SendBroadcastMessage(BroadcastTag, "Thrusters failed to set up correctly;\nInitialization required:\nSet tag in CD, then run \"init_d\"");
                    return true;
                }

            }
            if (ThrustersList != null && ThrustersList.Count > 0)
            {
                ThrustersInGroup = ThrustersList.Count();
            }
            if (NestedThrusters != null && NestedThrusters.Count > 0)
            {
                ThrustersInGroup = NestedThrusters.Count();
            }
            //Calculate max thrust
            maxBreakingThrust = 0;
            GridTerminalSystem.GetBlocksOfType(forwardThrusters, x => x.WorldMatrix.Forward == Cockpit.WorldMatrix.Backward && x.IsWorking);
            foreach (var t in forwardThrusters)
            {
                maxBreakingThrust += t.MaxEffectiveThrust;
                //Echo($"maxt thrust: {maxBreakingThrust}");
            }
            //INITIALIZATION NOT REQUIRED
            Echo("Drone setup is correct, no need to initialize");
            IGC.SendBroadcastMessage(BroadcastTag, "Drone setup is correct.\nNo need to initialize");
            initializedRequired = false;
            IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool>("initiRequired", initializedRequired));
            //Echo($"Thrusters in group : {ThrustersInGroup}");
            //Echo($"NestedThrusters : {NestedThrusters.Count}");
            //Echo($"ThrustersList : {ThrustersList.Count}");
            timerSM = new SimpleTimerSM(this, SequenceConditionalRotorSpeed());
            statusLCDStateMachine = new SimpleTimerSM(this, sequence: StatusLCD());
            starterPrint = new SimpleTimerSM(this, sequence: StartPrinting());
            return false;
        }
        public IEnumerable<double> SequenceConditionalRotorSpeed()
        {
            while (!firstRotation && printing)
            {
                IMyTerminalBlock firstBlock;
                PrintingBlocksListCreation();
                newRotorSpeed = RotorSpeed;
                yield return 3 * multTicks * ticksToSeconds;
                time += Runtime.TimeSinceLastRun.TotalSeconds;
                if (integrityListT0 != null && integrityListT0.Count > 0)
                {
                    firstBlock = integrityListT0[0];
                    activeWeldedBlockIntegrity = firstBlock.CubeGrid.GetCubeBlock(firstBlock.Min).BuildLevelRatio;
                    activeWeldedBlockName = firstBlock;
                }
                else yield break;
                newRotorSpeed = RotorControl(activeWeldedBlockName);
                PrintingOnActiveLCD();

                yield return 6 * multTicks * ticksToSeconds;
                while (activeWeldedBlockIntegrity < 1)
                {
                    
                    PrintingBlocksListCreation();
                    PrintingResults(totRemaining, remainingTB, safetyDistanceStop);
                    time += Runtime.TimeSinceLastRun.TotalSeconds;
                    newRotorSpeed = RotorControl(activeWeldedBlockName);
                    activeWeldedBlockIntegrity = firstBlock.CubeGrid.GetCubeBlock(firstBlock.Min).BuildLevelRatio;
                    PrintingOnActiveLCD();
                    activePrinting = true;
                    Wait = ImWait;
                    //firstRotation = false;
                    if (ignore1TB)
                    {
                        Wait = ImWait;
                        newRotorSpeed = RotorSpeed / 2;
                        if (!ignoringList.Contains(activeWeldedBlockName))
                        {
                            ignoringList.Add(activeWeldedBlockName);
                            refreshBlocks = ignoringList.Count;
                            //Echo($"refreshed Blocks = {refreshBlocks}");
                            yield return 6 * multTicks * ticksToSeconds;
                        }
                        time += Runtime.TimeSinceLastRun.TotalSeconds;
                        PrintingOnActiveLCD();
                        time = 0;
                        ignore1TB = false;
                        yield break;
                    }
                    if (ignoreTB)
                    {
                        Wait = ImWait;
                        newRotorSpeed = RotorSpeed / 2;
                        List<IMyTerminalBlock> ignoreAllList = new List<IMyTerminalBlock>();
                        GridTerminalSystem.GetBlocksOfType(ignoreAllList, x => !x.CubeGrid.GetCubeBlock(x.Min).IsFullIntegrity);
                        foreach (IMyTerminalBlock block in ignoreAllList)
                        {
                            yield return 7 * multTicks * ticksToSeconds;
                            ignoringList.Add(block);
                        }
                        refreshBlocks = ignoreAllList.Count;
                        ignoreAllList.Clear();
                        activePrinting = false;
                        ignoreTB = false;
                        time += Runtime.TimeSinceLastRun.TotalSeconds;
                        yield return 7 * multTicks * ticksToSeconds;
                        PrintingOnActiveLCD();
                        time = 0;
                        yield break;
                    }
                    yield return 60 * multTicks * ticksToSeconds;
                }
                //Echo("block deleted");
                time += Runtime.TimeSinceLastRun.TotalSeconds;
                activePrinting = false;
                builtBlocks++;
                totTime += (int)time;
                averageTime = totTime / builtBlocks;
                if (time > maxTime) { maxTime = (int)Math.Ceiling(time); }
                if (time < minTime) { minTime = (int)Math.Ceiling(time); }
                activeWeldedBlockIntegrity = firstBlock.CubeGrid.GetCubeBlock(firstBlock.Min).BuildLevelRatio;
                PrintingBlocksListCreation();
                PrintingResults(totRemaining, remainingTB, safetyDistanceStop);
                PrintingOnActiveLCD();
                newRotorSpeed = RotorSpeed / 2;
                time = 0;
                yield break;
            }
            yield break;
        }
        public float RotorControl(IMyTerminalBlock block)
        {
            var blockPos = Vector3D.Normalize(block.GetPosition() - rotorPosition);
            
            //Echo($"blockPos: {blockPos}\nplaneNormal: {planeNormal}");
            var blockProj = Vector3D.ProjectOnPlane(ref blockPos, ref planeNormal);
            if (welder_right) { rotorFacing = welderSign * rotorHeadMatrix.Right; }
            else if (welder_forward) { rotorFacing = welderSign * rotorHeadMatrix.Forward; }
            angle = Math.Atan2(blockProj.Cross(rotorFacing).Dot(planeNormal), rotorFacing.Dot(rotorFacing));
            float mult = SpeedMult(angle);
            //float mult = (float)angle *1.5f;
            //Echo($"rotorHead matrix: {rotorHeadMatrix}\n");
            //Echo($"mult: {mult}");
            //Echo($"angle: {angle / Math.PI * 180}\nblockProj: {blockProj}\nrotorFacing: {rotorFacing}\nx: {blockProj.Cross(rotorFacing).Dot(planeNormal)}\ny: {rotorFacing.Dot(rotorFacing)}");
            if (Math.Abs(angle) > rotorToll)
            {
                return RotorSpeed * mult;
            }
            else { return 0; }
        }
        public float SpeedMult(double angle)
        {
            float firstAngle = 0.2f;
            float secondAngle = -0.2f;
            float mult = 2f;

            if (angle > firstAngle) { return mult; }
            else if (angle <= firstAngle && angle >= 0) { return (float)angle * mult; }

            if (angle < secondAngle) { return -mult; }
            else if (angle >= secondAngle) { return (float)angle * mult; }
            return mult;
        }
        public void RotorSpeedingUp()
        {
            newRotorSpeed = DynamicSpeed;
            IGC.SendBroadcastMessage(BroadcastTag, newRotorSpeed);
        }
        public void ActionTime(IMyShipController Cockpit)
        {
            //PrintingBlocksListCreation();
            if (!imMoving && !weldersToggleOn)
            {
                weldersToggleOn = true;
                IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool>("weldersToggle", weldersToggleOn));
            }

            Wait -= Runtime.TimeSinceLastRun.TotalSeconds;

            remainingTB = integrityListT0.Count;
            totRemaining = Projector.RemainingBlocks - refreshBlocks;

            PrintingResults(totRemaining, remainingTB, safetyDistanceStop);
            if (Wait <= 0)
            {
                remainingTB = integrityListT0.Count;
                totRemaining = Projector.RemainingBlocks - refreshBlocks;
                PrintingResults(totRemaining, remainingTB, safetyDistanceStop);

                if (remainingTB > 0)
                {
                    mass = Cockpit.CalculateShipMass().PhysicalMass;
                    Wait = ImWait;
                    remainingTB = integrityListT0.Count;
                    totRemaining = Projector.RemainingBlocks - refreshBlocks;
                    imMoving = false;
                    PrintingResults(totRemaining, remainingTB, safetyDistanceStop);
                    firstRotation = false;
                }

                if (remainingTB <= 0 && preciseMoving==false)
                {
                    PrintingBlocksListCreation();
                    sectionsBuilt++;
                    endingBlocks = totBlocks - totRemaining;
                    averageBlocks = endingBlocks / sectionsBuilt;
                    start = Me.GetPosition();
                    preciseMoving = true;
                    imMoving = true;
                    mass = Cockpit.CalculateShipMass().PhysicalMass;
                    thrust = (mass * acceleration) / ThrustersInGroup;
                    maxDecel = maxBreakingThrust*1.5f / mass; // 1.5 is the dampeners modifier
                    Runtime.UpdateFrequency = UpdateFrequency.Update1;
                    PrintingResults(totRemaining, remainingTB, safetyDistanceStop);
                }

                //check for stopping status (finished printing)
                if (totRemaining == 0)
                {
                    //Echo("1");
                    Stop();
                    printing = false;
                    //Echo("2");
                    if (toggleList != null && toggleAfterFinish)
                    {
                        ToggleOn(toggleList.Clear());
                    }
                    else if (toggleList == null && toggleAfterFinish)
                    {
                        toggleList = toggleBuilder.ToImmutable();
                        ToggleOn(toggleList.Clear());
                    }
                    //foreach (var gyro in imGyroList) { gyro.GyroOverride = false; }
                    Echo($"Ship Printed!\n{toggleAfterFinish}");
                    IGC.SendBroadcastMessage(BroadcastTag, $"\nShip Printed!\nToggle: {toggleAfterFinish}\nFly safe beratna!");
                    return;
                }
                if (safetyDistanceStop >= maxDistanceStop)
                {
                    Stop();
                    IGC.SendBroadcastMessage(BroadcastTag, "\nSafety Distance Reached. Tug stopped.");
                    SaveVariables();
                }
            }
        }
        public void PrintingBlocksListCreation()
        {
            List<IMyFunctionalBlock> supportBlocks = new List<IMyFunctionalBlock>();
            if (startIgnoringBlocks != null && startIgnoringBlocks.Count > 0)
            {
                GridTerminalSystem.GetBlocksOfType(supportBlocks, x =>
                {
                    foreach (string tag in startIgnoringBlocks)
                    {
                        if (x.CustomName.Contains(tag))
                        {
                            return true;
                        }
                    }
                    return false;
                });
            }
            //Me.CustomData += $"supp: {supportBlocks.Count}";
            ignoredBlocks = supportBlocks.Count;
            GridTerminalSystem.GetBlocksOfType(integrityListT0, b => !b.CubeGrid.GetCubeBlock(b.Min).IsFullIntegrity
                && !ignoringList.Contains(b)
                && !supportBlocks.Contains(b)
                );
        }
        public IEnumerable<double> StartPrinting()
        {
            starterString.Clear();
            starterString.Append(
                $"{lcd_divider}\nHold your cockpit beratna...\n");
            IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, string>("StartLog", starterString.ToString()));
            yield return 1 / ticksToSeconds;
            starterString.Append($"RPA is booting up...\n{ lcd_divider}\n");
            IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, string>("StartLog", starterString.ToString()));
            yield return 0.5/ticksToSeconds;
            for (int i = 0; i < 3; i++)
            {
                yield return 0.5/ticksToSeconds;
                starterString.Append($"{variables[i]}\n");
                IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, string>("StartLog", starterString.ToString()));
            }
            yield return 1/ticksToSeconds;
            starterString.Replace(variables[0], variables[0] + variablesStatus[0]);
            IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, string>("StartLog", starterString.ToString()));
            yield return 0.5/ticksToSeconds;
            LoadStoredData();
            starterString.Replace(variables[0] + variablesStatus[0], variables[0] + variablesStatus[1]);
            starterString.Replace(variables[1], variables[1] + variablesStatus[0]);
            IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, string>("StartLog", starterString.ToString()));
            yield return 0.5/ ticksToSeconds;
            firstRotation = true;
            setupCommand = false;
            aligningBool = true;
            time = 0;
            totBlocks = TotalBlocks();
            Wait = ImWait;
            remainingTB = integrityListT0.Count;
            totRemaining = Projector.RemainingBlocks - refreshBlocks;
            safetyDistanceStop = Math.Round(Vector3D.Distance(rotorPosition, Cockpit.GetPosition()), 2);
            imMoving = false;
            starterString.Replace(variables[1] + variablesStatus[0], variables[1] + variablesStatus[1]);
            starterString.Replace(variables[2], variables[2] + variablesStatus[0]);
            IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, string>("StartLog", starterString.ToString()));
            yield return 0.5 / ticksToSeconds;
            while (aligningBool)
            {
                yield return 5 * multTicks * ticksToSeconds;
                ImAligning();
            }
            IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, string>("StartLog", starterString.ToString()));
            yield return 0.5 / ticksToSeconds;
            starterString.Replace(variables[2] + variablesStatus[0], variables[2] + variablesStatus[1]);
            starterString.Append($"\n{lcd_divider}\n{variables[3]}");
            timerSM.Start();
            statusLCDStateMachine.AutoStart = true;
            timerSM.AutoStart = true;
            statusLCDStateMachine.Start();
            IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, string>("StartLog", starterString.ToString()));
            yield return 1 / ticksToSeconds;
            starterString.Append($"\n{variables[4]}");
            IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, string>("StartLog", starterString.ToString()));
            yield return 1/ ticksToSeconds;
            remainingTB = integrityListT0.Count;
            totRemaining = Projector.RemainingBlocks - refreshBlocks;
            safetyDistanceStop = Math.Round(Vector3D.Distance(rotorPosition, Cockpit.GetPosition()), 2);
            mass = Cockpit.CalculateShipMass().PhysicalMass;
            starterString.Append($"\n{variables[5]}");
            IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, string>("StartLog", starterString.ToString()));
            yield return 1/ ticksToSeconds;
            starterString.Append($"\n{variables[6]}");
            IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, string>("StartLog", starterString.ToString()));
            yield return 1 / ticksToSeconds;
            PrintingResults(totRemaining, remainingTB, safetyDistanceStop);
            activation = true;
            IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool>("activation", activation));
            yield return 10 * ticksToSeconds;
            starterPrint.Stop();
            starterString.Clear();
            printing = true;
            startCommand = false;
            yield break;
        }
        void ImListening(IMyShipController Cockpit, IMyProjector Projector)
        {
            while (_myBroadcastListener.HasPendingMessage)
            {
                var myIGCMessage = _myBroadcastListener.AcceptMessage();
                if (myIGCMessage.Tag == BroadcastTag && myIGCMessage.Data is string)
                {
                    switch (myIGCMessage.Data.ToString())
                    {
                        case "start":
                            if(startCommand)
                            {
                                Stop();
                                printing = false; //stop the print-->for the main
                                startCommand = false;
                            }
                            startCommand = true;
                            starterPrint.Start();
                            Runtime.UpdateFrequency = UpdateFrequency.Update1;
                            break;

                        case "projector":
                            Projecting(Projector);
                            break;

                        case "stop":
                            Stop();
                            printing = false; //stop the print-->for the main
                            startCommand = false;
                            skip = false;
                            IGC.SendBroadcastMessage(BroadcastTag, "Stopping command processed.");
                            break;

                        case "ignore1":
                            ignore1TB = true;
                            break;

                        case "ignore_all":
                            ignoreTB = true;
                            break;

                        case "init_d":
                            CustomData();
                            SetupBlocks();
                            break;
                    }
                }
                
                //immutable list from toggle and start
                if (myIGCMessage.Data is ImmutableList<string>)
                {
                    var immutable = (ImmutableList<string>)myIGCMessage.Data;
                    //Echo($"{immutable.Count}");
                    //\n{toggleList[0]}\n{toggleList[1]}
                    string command = immutable[0].ToLower();
                    if (command == "toggle")
                    {
                        //Echo("yes toggle");
                        if (immutable.Count > 1)
                        {
                            for (int i = 1; i < immutable.Count; i++)
                            {
                                toggleBuilder.Add(immutable[i]);
                                //Me.CustomData+= ($"received element:{immutable[i]}");
                            }
                            toggleList = toggleBuilder.ToImmutable();
                            toggleBuilder.Clear();
                        }
                        //Echo($"asd:{toggleList.Count}");

                        Runtime.UpdateFrequency = UpdateFrequency.None;
                        Wait = 0;
                        //checkDistance = false;
                        Stop();
                        activation = false;
                        ToggleOn(toggleList);
                        IGC.SendBroadcastMessage(BroadcastTag, "Toggle On blocks, except for Epsteins and Tools");
                        IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool>("activation", activation));
                    }
                    else if (command == "start")
                    {
                        //Echo("yes start");
                        if (immutable.Count > 1)
                        {
                            for (int i = 1; i < immutable.Count; i++)
                            {
                                startBuilder.Add(immutable[i]);
                                //Me.CustomData += ($"received element:{immutable[i]}");
                            }
                            startIgnoringBlocks = startBuilder.ToImmutable();
                            startBuilder.Clear();
                        }
                    }
                }

                //activate toggle whence finished printing
                if (myIGCMessage.Data is MyTuple<string, bool>)
                {
                    var tuple = (MyTuple<string, bool>)myIGCMessage.Data;
                    string command = tuple.Item1;
                    if (command == "toggleAfterFinish")
                    {
                        bool toggleYes = tuple.Item2;
                        toggleAfterFinish = toggleYes;
                    }
                }
                //skip command
                if (myIGCMessage.Data is MyTuple<string, bool>)
                {
                    var tuple = (MyTuple<string, bool>)myIGCMessage.Data;
                    string command = tuple.Item1;
                    if (command == "skip")
                    {
                        activation = false;
                        printing = false;
                        startCommand = false;
                        starterPrint.Stop();
                        IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool>("activation", activation));
                        printAfterSkip = tuple.Item2;
                        Runtime.UpdateFrequency = UpdateFrequency.Update1;
                        skipStartingDist = Me.GetPosition();
                        skip = true;
                        remainingTB = integrityListT0.Count;
                        totRemaining = Projector.RemainingBlocks - refreshBlocks;
                        preciseMoving = true;
                        mass = Cockpit.CalculateShipMass().PhysicalMass;
                        thrust = (mass * acceleration) / ThrustersInGroup;
                        maxDecel = maxBreakingThrust * 1.5f / mass; // 1.5 is the dampeners modifier
                        IGC.SendBroadcastMessage(BroadcastTag, $"Backward movement processed\nPrint after movement: {printAfterSkip}");
                    }
                }

                //weld while moving command: motion_print
                if (myIGCMessage.Data is MyTuple<string, bool>)
                {
                    var tuple = (MyTuple<string, bool>)myIGCMessage.Data;
                    string command = tuple.Item1;
                    if (command == "motion_print")
                    {
                        weldWhileMoving = tuple.Item2;
                    }
                }
                //command==drone_movement: change move per section
                if (myIGCMessage.Data is MyTuple<string, double>)
                {
                    var tuple = (MyTuple<string, double>)myIGCMessage.Data;
                    string command = tuple.Item1;
                    if (command == "drone_move")
                    {
                        var movement = tuple.Item2;
                        DroneMovDistance = movement;
                    }
                }
                //command == max_distance: change max distance safe of the tug
                if (myIGCMessage.Data is MyTuple<string, double>)
                {
                    var tuple = (MyTuple<string, double>)myIGCMessage.Data;
                    string command = tuple.Item1;
                    if (command == "max_distance")
                    {
                        maxDistanceStop = tuple.Item2;
                    }
                }
                //commands==rotor_ws: change rotor speed
                if (myIGCMessage.Data is MyTuple<string, float, float>)
                {
                    var tuple = (MyTuple<string, float, float>)myIGCMessage.Data;
                    string command = tuple.Item1;
                    if (command == "rotor_ws")
                    {
                        RotorSpeed = tuple.Item2;
                        DynamicSpeed = tuple.Item3;
                    }
                }
                //commands == waiting: change ImWait
                if (myIGCMessage.Data is MyTuple<string, double>)
                {
                    var tuple = (MyTuple<string, double>)myIGCMessage.Data;
                    string command = tuple.Item1;
                    if (command == "waiting")
                    {
                        ImWait = tuple.Item2;
                    }
                }
                //continue stream of rotorhead infos
                if (myIGCMessage.Data is MyTuple<string, bool, bool, MatrixD, float>)
                {
                    var tuple = (MyTuple<string, bool, bool, MatrixD, float>)myIGCMessage.Data;
                    string command = tuple.Item1;
                    if (command == "rotorHead")
                    {
                        welder_right = tuple.Item2;
                        welder_forward = tuple.Item3;
                        rotorHeadMatrix = tuple.Item4;
                        planeNormal = rotorHeadMatrix.Up;
                        rotorSpeed = tuple.Item5;
                        //Echo($"rotorMatrix: {rotorHeadMatrix}");
                    }
                }
                //Untag sent tag from drone
                if (myIGCMessage.Data is MyTuple<string, string>)
                {
                    printing = false;
                    var tuple = (MyTuple<string, string>)myIGCMessage.Data;
                    string command = tuple.Item1;
                    string tag = tuple.Item2;
                    //Echo($"comm: {command}\ntag: {tag}");
                    if (command == "untag_d")
                    {
                        UntagDrone(tag);
                    }
                }

                //setup commands
                if (myIGCMessage.Data is MyTuple<double, float, double, float, MyTuple<double, bool, double>, MyTuple<MatrixD, int>>)
                {
                    Starter();
                    printing = false;
                    IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, string>("droneVersion", droneVersion));
                    //Echo($"init: {initializedRequired}");
                    IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool>("initRequired", initializedRequired));
                    setupAlreadySent = true;
                    //Echo($"setup: {setupAlreadySent}");
                    IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool>("SetupSent", setupAlreadySent));
                    Runtime.UpdateFrequency = UpdateFrequency.None;
                    activation = false;
                    IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool>("activation", activation));
                    setupCommand = true;
                    Stop();
                    firstRotation = false;
                    var tuple = (MyTuple<double, float, double, float, MyTuple<double, bool, double>, MyTuple<MatrixD, int>>)myIGCMessage.Data;
                    ImWait = tuple.Item1;
                    DynamicSpeed = tuple.Item2;
                    DroneMovDistance = tuple.Item3;
                    RotorSpeed = tuple.Item4;
                    maxDistanceStop = tuple.Item5.Item1;
                    weldWhileMoving = tuple.Item5.Item2;
                    maxRTCustom = tuple.Item5.Item3;
                    rotorMatrix = tuple.Item6.Item1;
                    welderSign = tuple.Item6.Item2;
                    rotorPosition = rotorMatrix.Translation;
                    rotorOrientation = rotorMatrix.GetOrientation();
                    string output_tuple = $"       SETUP COMPLETED       \n{lcd_divider}\n" +
                        $"{"|Weld in Movement",-17} {"= " + weldWhileMoving + ";",-16}\n" +
                        $"{"|Wait",-17} {"= " + ImWait + " seconds;",-16}\n" +
                        $"{"|DroneMovement",-17} {"= " + DroneMovDistance + " meters;",-16}\n" +
                        $"{"|Rotor Speed",-17} {"= " + RotorSpeed + " RPM;",-16}\n" +
                        $"{"|Dynamic Speed",-17} {"= " + DynamicSpeed + " RPM;",-16}\n" +
                        $"{"|Safety Distance",-17} {"= " + maxDistanceStop + " meters",-16}\n" +
                        $"{"|Max Runtime",-17} {"= " + maxRTCustom + " ms",-16}"
                        ;
                    Echo(output_tuple);
                    IGC.SendBroadcastMessage(BroadcastTag, output_tuple);
                }
                //alignment drone
                if (myIGCMessage.Data is MatrixD)
                {
                    Stop();
                    setupCommand = true;
                    Runtime.UpdateFrequency = UpdateFrequency.Update1;
                    //checkDistance = false;
                    firstRotation = false;
                    printing = true;
                    Wait = ImWait;
                    aligningBool = true;
                    rotorOrientation = (MatrixD)myIGCMessage.Data;
                    IGC.SendBroadcastMessage(BroadcastTag, "Drone is aligning.");
                    activation = false;
                    IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool>("activation", activation));
                }
            }
        }
        public void ImAligning()
        {
            if (rotorMatrix == MatrixD.Zero)
            {
                IGC.SendBroadcastMessage(BroadcastTag, "Run setup first!");
            }
            else
            {
                if (weldersToggleOn)
                {
                    weldersToggleOn = false;
                    WeldersToggle(weldersToggleOn); 
                }
                double pitch, yaw, roll;
                //the desiredForward and UP are the rotor's: the cockpit of the tug will be aligned with the downward direction of the rotor!!
                var desiredForward = -rotorOrientation.Up;
                var desiredUp = Vector3D.Zero; // IF the ROLL angle is important use " -rotorOrientation.Forward";
                GetRotationAnglesSimultaneous(desiredForward, desiredUp, Cockpit.WorldMatrix, out yaw, out pitch, out roll);
                ApplyGyroOverride(pitch, yaw, roll, imGyroList, Cockpit.WorldMatrix);
            }
        }

        //turn on all blocks
        public void ToggleOn(ImmutableList<string> toggleList)
        {
            //foreach (var toggle in toggleList)
            //{
            //    Echo($"passing: {toggle}\n");
            //}
            //Echo($"tot passing: {toggleList.Count}");
            //turn on all blocks
            List<IMyShipWelder> allWelders = new List<IMyShipWelder>();
            List<IMyShipDrill> allDrills = new List<IMyShipDrill>();
            List<IMyShipGrinder> allGrinder = new List<IMyShipGrinder>();
            List<IMyProjector> allProj = new List<IMyProjector>();
            List<IMyFunctionalBlock> toggleBlocksList = new List<IMyFunctionalBlock>();
            List<IMyFunctionalBlock> exclusionBlocks = new List<IMyFunctionalBlock>();
            List<IMyThrust> epsteinList = new List<IMyThrust>();
            GridTerminalSystem.GetBlocksOfType(toggleBlocksList);
            foreach (var block in toggleBlocksList)
            {
                if (block != null && !block.Enabled)
                {
                    block.Enabled = true;
                }
            }

            if (toggleList != null && toggleList.Count > 0)
            {
                GridTerminalSystem.GetBlocksOfType(exclusionBlocks, x =>
                {
                    foreach (string tag in toggleList)
                    {
                        if (x.CustomName.Contains(tag))
                            return true;
                    }
                    return false;
                });
                if (exclusionBlocks != null && exclusionBlocks.Count > 0)
                {
                    foreach (var block in exclusionBlocks)
                    {
                        //Echo($"block: {block.CustomName}");
                        { block.Enabled = false; }
                    }
                }
            }
            //Echo($"exlusion: {exclusionBlocks.Count}");

            try
            {
                GridTerminalSystem.GetBlocksOfType(epsteinList);
                List<MyDefinitionId> thrsutersList = new List<MyDefinitionId>()
                {
                    MyDefinitionId.Parse("MyObjectBuilder_Thrust/ARYLNX_Epstein_Drive"),
                    MyDefinitionId.Parse("MyObjectBuilder_Thrust/ARYLNX_MUNR_Epstein_Drive"),
                    MyDefinitionId.Parse("MyObjectBuilder_Thrust/ARYLNX_PNDR_Epstein_Drive"),
                    MyDefinitionId.Parse("MyObjectBuilder_Thrust/ARYLNX_QUADRA_Epstein_Drive"),
                    MyDefinitionId.Parse("MyObjectBuilder_Thrust/ARYLNX_RAIDER_Epstein_Drive"),
                    MyDefinitionId.Parse("MyObjectBuilder_Thrust/ARYLNX_ROCI_Epstein_Drive"),
                    MyDefinitionId.Parse("MyObjectBuilder_Thrust/ARYLNX_Leo_Epstein_Drive"),
                    MyDefinitionId.Parse("MyObjectBuilder_Thrust/ARYLYNX_SILVERSMITH_Epstein_DRIVE"),
                    MyDefinitionId.Parse("MyObjectBuilder_Thrust/ARYLNX_DRUMMER_Epstein_Drive"),
                    MyDefinitionId.Parse("MyObjectBuilder_Thrust/ARYLNX_SCIRCOCCO_Epstein_Drive"),
                    MyDefinitionId.Parse("MyObjectBuilder_Thrust/ARYLNX_Mega_Epstein_Drive")

                };
                //Echo($"{epsteinList.Count}Thrust");
                foreach (var driver in epsteinList)
                {
                    foreach (var thruster in thrsutersList)
                    {
                        //Echo($"{thruster.BlockDefinition}");
                        if (driver.BlockDefinition == thruster)
                        {
                            driver.Enabled = false;
                        }
                    }
                }
                //turn off tools and proj
                GridTerminalSystem.GetBlocksOfType(allWelders);
                GridTerminalSystem.GetBlocksOfType(allGrinder);
                GridTerminalSystem.GetBlocksOfType(allDrills);
                GridTerminalSystem.GetBlocksOfType(allProj, x => !x.CustomName.Contains(TagCustom));
                if (allWelders != null && allWelders.Count > 0)
                {
                    foreach (var w in allWelders)
                    {
                        w.Enabled = false;
                    }
                }
                if (allGrinder != null && allGrinder.Count > 0)
                {
                    foreach (var w in allGrinder)
                    {
                        w.Enabled = false;
                    }
                }
                if (allDrills != null && allDrills.Count > 0)
                {
                    foreach (var w in allDrills)
                    {
                        w.Enabled = false;
                    }
                }
                //toggle off all item in list of toggleList except first one , cause it's the command itself
                if (allProj != null && allProj.Count > 0)
                {
                    foreach (var proj in allProj)
                    { proj.Enabled = false; }
                }
            }
            catch
            {
                string output = "Seems like ya ar not on SIGMA DRACONIS EXPANSE SERVER kopeng.\n" +
                                    "Are you a welwala, ke?\n" +
                                    "Get you blocks toggle on away from here Inyalowda";
                Echo(output);
                IGC.SendBroadcastMessage(BroadcastTag, output);
            }
        }
        public int TotalBlocks()
        {
            if (Projector.IsProjecting)
            {
                imProjecting = true;
                return Projector.TotalBlocks - ignoredBlocks;
            }
            else if (!Projector.IsProjecting)
            {
                imProjecting = false;
                return 100000;
            }
            if (Projector.TotalBlocks == 0)
            {
                return 100000;
            }
            return 100000;
        }
        public void PrintingResults(int totRemaining, int remainingTB, double safetyDisanceStop)
        {
            StringBuilder printingOutput = new StringBuilder();
            printingOutput.Clear();
            double tankLevel = 0;

            if (Projector.IsProjecting && totBlocks > 0)
            {
                totBlockPercentage = (float)Math.Max(Math.Round((double)(totBlocks - totRemaining) / (double)totBlocks, 2), 0.01) * 100;
                totBlockMultiplier = (int)Math.Ceiling(totBlockPercentage / 10);
            }
            else if (!Projector.IsProjecting)
            {
                totBlockPercentage = 100f;
                totBlockMultiplier = 10;
            }
            foreach (var t in tank)
            {
                tankLevel += t.FilledRatio;
            }
            tankLevel /= tank.Count;
            tankMultiplier = (int)Math.Ceiling(tankLevel * 10);

            string[] waitingSpinnerSymbols = new string[] { Math.Round(Wait).ToString(), " " };
            spinningWaitingStatus += Runtime.TimeSinceLastRun.TotalSeconds;
            lcd_moving_spinner_status += Runtime.TimeSinceLastRun.TotalSeconds;
            lcd_printing_spinner_status += Runtime.TimeSinceLastRun.TotalSeconds;

            var remainingDist = maxDistanceStop - safetyDisanceStop;
            string spinner;
            string waitingSpinner;
            if (activePrinting)
            {
                if (spinningWaitingStatus > waitingSpinnerSymbols.Length) spinningWaitingStatus = 0;
                waitingSpinner = waitingSpinnerSymbols[(int)spinningWaitingStatus];
            }
            else waitingSpinner = waitingSpinnerSymbols[0];
            if (imMoving)
            {
                if (lcd_moving_spinner_status > lcd_moving_spinners.Length) lcd_moving_spinner_status = 0;
                spinner = lcd_moving_spinners[(int)lcd_moving_spinner_status];
            }
            else
            {
                if (lcd_printing_spinner_status > lcd_printing_spinners.Length) lcd_printing_spinner_status = 0;
                spinner = lcd_printing_spinners[(int)lcd_printing_spinner_status];
            }
            var instsPos = Math.Round(Vector3D.Distance(start, Me.GetPosition()), 2);
            printingOutput.Append("              " + spinner + "          \n" + lcd_divider + "\n" +
            $"{"Projection",-27}" + $"{imProjecting,5}\n" +
            $"{lcd_divider}\n" +
            $"{"|Total Blocks",-27}" + $"{"= " + totBlocks}\n" +
            $"{"|Total remaining blocks",-27}" + $"{"= " + totRemaining + " (" + refreshBlocks + ")"};\n" +
            $"{"|Missing TB for section",-27}" + $"{"= " + remainingTB + " (" + refreshBlocks + ")"};\n" +
            $"{"|Ignoring TB from Start",-27}" + $"{"= " + ignoredBlocks};\n" +
            $"{"|Seconds till next check",-27}" + $"{"= " + waitingSpinner};\n" +
            $"{"|Distance",-27}" + $"{"= " + instsPos + " meters;\n"}" +
            $"{"|Safety Dist Remaining",-27}" + $"{"= " + remainingDist + " meters"};\n" +
            $"{"|N° Active Gyros",-27}" + $"{"= " + imGyroList.Count};\n" +
            $"{"|Grid Total Mass",-27}" + $"{"= " + Math.Round(mass / 1000f, 0) + " tons;\n"}" +
            $"{"|Rotor Speed",-27}" + $"{"= " + Math.Round(newRotorSpeed, 2) + " RPM;\n"}" +
            $"{lcd_divider}\n" +
            $"{"Toggle After Print:",-27}" + $"{"= " + toggleAfterFinish};\n" +
            $"{"Weld in Movement:",-27}" + $"{"= " + weldWhileMoving};\n" +
            $"{lcd_divider}\n" + $"{lcd_h2_level + ": " + Math.Round(tankLevel * 100, 2) + "%      \n"}" +
            //tank multiplier and totBlockMultiplier are multiplicated by 3 cause the string is long 32 digit
            $"{"0% " + string.Concat(Enumerable.Repeat("=", tankMultiplier * 3)),-33}" + $"{" 100%",4}\n" +
            $"{lcd_proj_level + ": " + totBlockPercentage + "%      "}\n" +
            $"{"0% " + string.Concat(Enumerable.Repeat("=", totBlockMultiplier * 3)),-33}" + $"{" 100%",4}\n" +
            $"{lcd_divider}\n"
            );
            if (tankLevel * 100 < 10 && (lcd_printing_spinner_status < 2 || lcd_printing_spinner_status > 5))
            {
                printingOutput.Append($"      CRITICAL TANK LEVEL!!\n");
            }
            else printingOutput.Append("");
            if(averageRT > 0.50 * maxRTCustom)
            {
                printingOutput.Append($"{lcd_divider}\n CRITICAL RT: DRONE SLOWING DOWN");
            }
            else printingOutput.Append("");

            IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, string>("LogWriting", printingOutput.ToString()));
            printingOutput.Clear();
        }
        public void PrintingOnActiveLCD()
        {
            int totTimeETA;
            int ETA_Extimate;
            int ETA;
            int ETA_Perc_based;
            //extimated time with some semi viable constants, like 60=number of average sections (150 meter long ship)
            //totBlocks/20 ratio between functional and armor blocks
            //divided by 60 to convert in minutes
            totTime = (int)Math.Ceiling((double)totTime);
            totTimeETA = (int)Math.Ceiling((60 * ImWait + averageTime * totBlocks / 20) / 60);
            ETA_Extimate = (int)Math.Abs(totTimeETA - Math.Ceiling(totTime / 60f));
            ETA_Perc_based = (int)Math.Ceiling(Math.Abs(totTime - (100 / totBlockPercentage) * totTime) / 60);
            ETA = (int)Math.Ceiling(((double)ETA_Extimate + ETA_Perc_based) / 2);

            //PRINT ON ACTIVE LCD
            activeOuput.Append($"{lcd_divider}\n          ACTIVE WELDING\n{lcd_divider}\n"
                 + $"{activeWeldedBlockName.CustomName,-26}{Math.Round(activeWeldedBlockIntegrity * 100, 2),5}%\n" +
                 $"{"Active Welding time",-27}{"= " + (int)Math.Round(time, 0) + " s",5}\n" +
                 $"{lcd_divider}\n" +
                 $"             STATS\n{lcd_divider}\n" +
                 $"{"Section N°",-25}{"= " + sectionsBuilt,7}\n" +
                 $"{"Total printing time",-25}{"= " + Math.Ceiling(totTime / 60f) + " m",7}\n" +
                 $"{"Average time",-25}{"= " + averageTime + " s",7}\n" +
                 $"{"Max time",-25}{"= " + maxTime + " s",7}\n" +
                 $"{"Min time",-25}{"= " + minTime + " s",7}\n" +
                 $"{"Average Blocks/section",-24}{"= " + averageBlocks,8}\n" +
                 $"{lcd_divider}\n" +
                 $"{"ETA (EXT+Perc)/2",-19}{"= " + ETA + " minutes",13}\n" +
                 $"{"ETA_EXT",-19}{"= " + ETA_Extimate + " minutes",13}\n" +
                 $"{"ETA_Perc",-19}{"=  " + ETA_Perc_based + " minutes",13}\n" +
                 $"{lcd_divider}\n" +
                 $"{"Drone Avg RT",-19}{"=  " + averageRT + " ms",13}\n"
                 //$"{"totTime",-19}{"= " + totTime + " secs", 13}\n" +
                 //$"{"blocPerc", -19}{" =" + totBlockPercentage, 13}\n" +
                 //$"{"1/blockPerc = " + 100 / totBlockPercentage}\n" +
                 //$"{"1/blockPerc*totTime = " + 100 / totBlockPercentage *totTime}\n" +
                 //$"{"totTime - su = " + (totTime-( 100 / totBlockPercentage * totTime))}\n"

                 );
            IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, string>("ActiveWelding", activeOuput.ToString()));
            activeOuput.Clear();
        }
        public IEnumerable<double> StatusLCD()
        {
            while (integrityListT0 != null && integrityListT0.Count > 0 && printing && !firstRotation)
            {
                List<IMyTerminalBlock> tempList = integrityListT0.ToList();
                for (int i = 0; i < tempList.Count; i++)
                {
                    yield return 10 * multTicks * ticksToSeconds;
                    var block = tempList[i];
                    string name = block.CustomName;
                    if (name.Length > 25)
                    {
                        name = name.Substring(0, 10) + "...." + name.Substring(name.Length - 10);
                    }
                    float integrity = block.CubeGrid.GetCubeBlock(block.Min).BuildLevelRatio * 100;
                    printingStatus.Append($"{name,-26}{Math.Round(integrity, 2),5}%\n");
                }
                IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, string>("StatusWriting", printingStatus.ToString()));
                printingStatus.Clear();
            }
            IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, string>("StatusWriting", ""));
            yield break;
        }
        public void Projecting(IMyProjector Projector)
        {
            Projector.Enabled = !Projector.Enabled;
            IGC.SendBroadcastMessage(BroadcastTag, "Projector On/Off \n");
        }
        public void PreciseMovement(IMyShipController Cockpit, List<IMyThrust> ThrusterGroup, int totRemaining, 
            int remainingTB, Vector3D startingPosition)
        {
            //IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, string>("debug", $"OUT==moving: {imMoving}\nweld: {weldWhileMoving}\ntoggle: {weldersToggleOn}"));
            //Echo($"OUT==moving\n");
            if (!weldWhileMoving && weldersToggleOn)
            {
                weldersToggleOn = false;
                //IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, string>("debug", $"IN==moving: {imMoving}\nweld: {weldWhileMoving}\ntoggle: {weldersToggleOn}"));
                WeldersToggle(weldersToggleOn);
                //Echo($"IN==moving: {imMoving}\nweld: {weldWhileMoving}\ntoggle: {weldersToggleOn}");
            }
            else if (weldWhileMoving && !weldersToggleOn)
            {
                weldersToggleOn = true;
                WeldersToggle(weldersToggleOn);
            }
            //IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, string>("debug", $"start pos: {startingPosition}\ndistance: {Vector3D.Distance(startingPosition, Me.GetPosition())}"));
            Wait = ImWait;
            safetyDistanceStop = Math.Round(Vector3D.Distance(rotorPosition, Me.GetPosition()), 2);
            if(!skip) PrintingResults(totRemaining, remainingTB, safetyDistanceStop);
            foreach (var t in ThrusterGroup)
            {
                t.ThrustOverride = thrust;
                //Echo($"thrust: {t.ThrustOverride}");
            }
            //IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, string>("debug", $"vel: {Cockpit.GetShipVelocities().LinearVelocity}"));
            //instant speed
            Vector3D WorldVelocity = Cockpit.GetShipVelocities().LinearVelocity;
            velocity = WorldVelocity.Dot(Cockpit.WorldMatrix.Backward); //towards backward direction
            //seconds to stop
            var t_stop = velocity / maxDecel + 1d/60d;//1d/60d is to take ticks time into account
            //stopping distance
            var s_stop = velocity * t_stop - maxDecel * t_stop * t_stop / 2;
            //IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, string>("debug", $"start pos: {startingPosition}\ndistance: {Vector3D.Distance(startingPosition, Me.GetPosition())}"));
            if ((Vector3D.Distance(startingPosition, Me.GetPosition())+s_stop) >= DroneMovDistance-0.2d)
            {
                foreach (var bt in ThrusterGroup)
                {
                    bt.ThrustOverridePercentage = 0f;
                    //Echo($"thrust: {bt.ThrustOverride}");
                }
                //IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, string>("debug", $"\ndistance: {Vector3D.Distance(startingPosition, Me.GetPosition())}\nvelocity: {velocity}\nt_stop: {t_stop}"));
                if (Math.Abs(velocity) < 0.05f)
                {
                    preciseMoving = false;
                    Wait = ImWait;
                    aligningBool = true;
                    firstRotation = true;
                    //IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, string>("debug", $"prec mov: {preciseMoving}"));
                }
            }
        }
        public void Stop()
        {
            List<IMyThrust> allThrusters = new List<IMyThrust>();
            GridTerminalSystem.GetBlocksOfType(allThrusters);
            foreach (var thrusters in allThrusters)
            {
                thrusters.ThrustOverride = 0f;
            }

        }
        //this function will retrieve the wanted angles: first and second argument are the wanted forward and upward direction, while the 3rd arg 
        //is needed as referiment-->it's the block used for referiment!!
        void GetRotationAnglesSimultaneous(Vector3D desiredForwardVector, Vector3D desiredUpVector, MatrixD worldMatrix, out double yaw, out double pitch, out double roll)
        {
            desiredForwardVector = SafeNormalize(desiredForwardVector);

            MatrixD transposedWm;
            MatrixD.Transpose(ref worldMatrix, out transposedWm);
            Vector3D.Rotate(ref desiredForwardVector, ref transposedWm, out desiredForwardVector);
            Vector3D.Rotate(ref desiredUpVector, ref transposedWm, out desiredUpVector);

            Vector3D leftVector = Vector3D.Cross(desiredUpVector, desiredForwardVector);
            Vector3D axis;
            double angle;
            if (Vector3D.IsZero(desiredUpVector) || Vector3D.IsZero(leftVector))
            {
                //Echo("vector 0");
                axis = new Vector3D(desiredForwardVector.Y, -desiredForwardVector.X, 0);
                angle = Math.Acos(MathHelper.Clamp(-desiredForwardVector.Z, -1.0, 1.0));
            }
            else
            {
                leftVector = SafeNormalize(leftVector);
                Vector3D upVector = Vector3D.Cross(desiredForwardVector, leftVector);

                // Create matrix
                MatrixD targetMatrix = MatrixD.Zero;
                targetMatrix.Forward = desiredForwardVector;
                targetMatrix.Left = leftVector;
                targetMatrix.Up = upVector;

                axis = new Vector3D(targetMatrix.M23 - targetMatrix.M32,
                                    targetMatrix.M31 - targetMatrix.M13,
                                    targetMatrix.M12 - targetMatrix.M21);

                double trace = targetMatrix.M11 + targetMatrix.M22 + targetMatrix.M33;
                angle = Math.Acos(MathHelper.Clamp((trace - 1) * 0.5, -1, 1));
            }

            if (Vector3D.IsZero(axis))
            {
                angle = desiredForwardVector.Z < 0 ? 0 : Math.PI;
                yaw = angle;
                pitch = 0;
                roll = 0;
                return;
            }

            axis = SafeNormalize(axis);
            yaw = -axis.Y * angle;
            pitch = -axis.X * angle;
            roll = -axis.Z * angle;
            //Echo($"pitch: {pitch}\nyaw: {yaw}\nz: {roll}");
        }
        void ApplyGyroOverride(double pitchSpeed, double yawSpeed, double rollSpeed, List<IMyGyro> gyroList, MatrixD worldMatrix)
        {
            //Echo("applygyro");
            GridTerminalSystem.GetBlocksOfType(imGyroList);
            foreach (var gyro in imGyroList) { gyro.Enabled = true; }
            var rotationVec = new Vector3D(pitchSpeed, yawSpeed, rollSpeed);
            var relativeRotationVec = Vector3D.TransformNormal(rotationVec, worldMatrix);
            Wait = ImWait;
            timeStep += Runtime.TimeSinceLastRun.TotalSeconds;

            foreach (var thisGyro in gyroList)
            {

                var transformedRotationVec = Vector3D.TransformNormal(relativeRotationVec, Matrix.Transpose(thisGyro.WorldMatrix));
                thisGyro.Pitch = (float)transformedRotationVec.X * 0.5f;
                thisGyro.Yaw = (float)transformedRotationVec.Y * 0.5f;
                thisGyro.Roll = (float)transformedRotationVec.Z * 0.5f;
                thisGyro.GyroOverride = true;
                if (Math.Abs(pitchSpeed) < gyrosTolerance && Math.Abs(yawSpeed) < gyrosTolerance && Math.Abs(rollSpeed) < gyrosTolerance)
                {
                    if (setupCommand)
                    {
                        Runtime.UpdateFrequency = UpdateFrequency.None;
                        Wait = ImWait;
                        firstRotation = false;
                        weldersToggleOn = false;
                        WeldersToggle(weldersToggleOn);
                        IGC.SendBroadcastMessage(BroadcastTag, "Drone is aligned.");
                        foreach (var gyro in imGyroList) { gyro.GyroOverride = false; }
                        timeStep = 0;
                    }
                    else if (!setupCommand && !startCommand)
                    {                          
                        activation = true;
                        Activation(activation);
                        //IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, string>("debug", $"activ: {activation}\nwelders: {weldersToggleOn}"));
                        //IGC.SendBroadcastMessage(BroadcastTag, "Drone is aligned.");
                        PrintingResults(totRemaining, remainingTB, safetyDistanceStop);
                        foreach (var gyro in imGyroList) { gyro.GyroOverride = false; }
                        timeStep = 0;
                        Wait = ImWait;
                        aligningBool = false;
                        firstRotation = true;
                        //IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, string>("debug", $"activ: {activation}\nwelders: {weldersToggleOn}"));
                        imMoving = false;
                    }
                    else if(startCommand)
                    {
                        foreach (var gyro in imGyroList) { gyro.GyroOverride = false; }
                        timeStep = 0;
                        
                        aligningBool = false;
                    }
                }
            }
            if (timeStep > 5)
            {
                IGC.SendBroadcastMessage(BroadcastTag, "Drone cannot align. Security stop imminent");
                Runtime.UpdateFrequency = UpdateFrequency.None;
                Wait = 0;
                //checkDistance = false;
                Stop();
                activation = false;
                IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool>("activation", activation));
                foreach (var gyro in imGyroList) { gyro.GyroOverride = false; }
                timeStep = 0;
            }
            if (Math.Abs(pitchSpeed) > maxGyroRotation || Math.Abs(yawSpeed) > maxGyroRotation)
            {
                IGC.SendBroadcastMessage(BroadcastTag, "Align the drone manually below\n  25 degrees, before auto alignment!");
                Runtime.UpdateFrequency = UpdateFrequency.None;
                Wait = 0;
                //checkDistance = false;
                Stop();
                activation = false;
                IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool>("activation", activation));
                foreach (var gyro in imGyroList) { gyro.GyroOverride = false; }
                timeStep = 0;
            }
        }
        public static Vector3D SafeNormalize(Vector3D a)
        {
            if (Vector3D.IsZero(a))
                return Vector3D.Zero;

            if (Vector3D.IsUnit(ref a))
                return a;

            return Vector3D.Normalize(a);
        }
        public void WeldersToggle(bool toggle)
        {
            string myString = "weldersToggle";
            IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool>(myString, toggle));
        }
        public void Activation(bool activation)
        {
            string myString = "activation";
            IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool>(myString, activation));
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
        public class SimpleTimerSM
        {
            public readonly Program Program;

            /// <summary>
            /// Wether the timer starts automatically at initialization and auto-restarts it's done iterating.
            /// </summary>
            public bool AutoStart { get; set; }

            /// <summary>
            /// <para>Returns true if a sequence is actively being cycled through.</para>
            /// <para>False if it ended, got stopped or no sequence is assigned anymore.</para>
            /// </summary>
            public bool Running { get; private set; }

            /// <summary>
            /// <para>The sequence used by Start(). Can be null.</para>
            /// <para>Setting this will not automatically start it.</para>
            /// </summary>
            public IEnumerable<double> Sequence { get; set; }

            /// <summary>
            /// Time left until the next part is called.
            /// </summary>
            public double SequenceTimer { get; private set; }

            private IEnumerator<double> sequenceSM;

            public SimpleTimerSM(Program program, IEnumerable<double> sequence = null, bool autoStart = false)
            {
                Program = program;
                Sequence = sequence;
                AutoStart = autoStart;

                if (AutoStart)
                {
                    Start();
                }
            }

            /// <summary>
            /// <para>Starts or restarts the sequence declared in Sequence property.</para>
            /// <para>If it's already running, it will be stoped and started from the begining.</para>
            /// <para>Don't forget to set Runtime.UpdateFrequency and call this class' Run() in Main().</para>
            /// </summary>
            public void Start()
            {
                SetSequenceSM(Sequence);
            }

            /// <summary>
            /// <para>Stops the sequence from progressing.</para>
            /// <para>Calling Start() after this will start the sequence from the begining (the one declared in Sequence property).</para>
            /// </summary>
            public void Stop()
            {
                SetSequenceSM(null);
            }

            /// <summary>
            /// <para>Call this in your Program's Main() and have a reasonable update frequency, usually Update10 is good for small delays, Update100 for 2s or more delays.</para>
            /// <para>Checks if enough time passed and executes the next chunk in the sequence.</para>
            /// <para>Does nothing if no sequence is assigned or it's ended.</para>
            /// </summary>
            public void Run()
            {
                if (sequenceSM == null)
                    return;
                //multiplying it by 60 returns the number of ticks from last run (60 ticks per second)
                // game time between the previous PB run and now, subtract it from the waiting time.
                SequenceTimer -= Program.Runtime.TimeSinceLastRun.TotalSeconds * 60;

                if (SequenceTimer > 0)
                    return;
                // executes the next part in the sequence.
                // returns true if `yield return` was used.
                // returns false if it reached the end or `yield break` was used.
                bool hasValue = sequenceSM.MoveNext();

                if (hasValue)
                {
                    SequenceTimer = sequenceSM.Current; //value from yield return

                    if (SequenceTimer <= -0.5)
                        hasValue = false;
                }

                if (!hasValue)
                {
                    if (AutoStart)
                        SetSequenceSM(Sequence);
                    else
                        SetSequenceSM(null); //cleanup
                }
            }

            private void SetSequenceSM(IEnumerable<double> seq)
            {
                Running = false;
                SequenceTimer = 0;

                sequenceSM?.Dispose();
                sequenceSM = null;

                if (seq != null)
                {
                    Running = true;
                    sequenceSM = seq.GetEnumerator();
                }
            }
        }



    }
}
