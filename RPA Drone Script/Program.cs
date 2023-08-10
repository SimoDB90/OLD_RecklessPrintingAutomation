using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.GameSystems;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        /// <summary>
        /// "Robotic Printing Automation" by Reckless
        /// Current Version: V 3.4.0
        /// Script == Drone
        /// Guide's link: https://steamcommunity.com/sharedfiles/filedetails/?id=2965554098
        ///  
        /// Changelog V 3.4.0 (09/08/2023)
        /// -New Rotor Controller released;
        /// Changelog V 3.3.0 (07/08/2023)
        /// -Heavy clean up of the code from useless computations in order to improve performance;
        /// -Rework of start and toggle commands. See guide;
        /// -Add an oprional LCD called .ACTIVE to print infos about active welded block;
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
        /// -send to LOG lcd all main problems regarding uncompleted setup
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
        /// Changelog V 3.1.0 (27/07/2023)
        /// -Bugs fixed
        /// -Added an automatic alignment between the tug and the drone for the entire duration of the print
        /// -Added a new command "align" to force the alignment
        /// -DRONE ONLY: COCKPIT FORWARD direction must be perpendicular to printers!!!
        /// -DRONE ONLY: BACKWARD thrusters only used!!
        /// Changelog V 3.0.5 (35/07/3034)
        /// Fix welders group setting
        /// Changelog V 3.0.4 (24/07/2023)
        /// Changelog V 3.0.3 (23/07/2023)
        /// -"Toggle" now turn off all tools and projs too
        /// -You have to Add the tag to the tug's hydrongen tank for a better check on hydro %
        /// Changelog V 3.0.2 (19/07/2023)
        /// Added Projectioin status, Tug's H2 level and printing percentage
        /// Changelog V 3.0.1 (19/07/2023)
        /// Fixed some typos and formattings;
        /// Changelog V 3.0.0 (17/07/2023)
        /// Rewritten the hud;
        /// Added hudlcd functionalitites;
        /// Added "guide" command for easy reference in game
        /// Changelog V 2.9 (16/07/2023)
        /// Add new QoL commands;
        /// Add sound blocks to Fancy blocks;
        /// Add Antenna block check;
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
        /// Added the "update" command, to set to ignore remaning unwelded blocks, in order to continue the welding;
        /// Added a "forced stop" if the drone goes too far from starting point;
        /// Added the optional group with [RPA-Fancy] to toggle on/off lcd,lights and welders during start and stop;
        /// Fixed some minor bugs;
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
        /// Needed blocks for Drone
        /// 1)Cockpit
        /// 2)Group of backward thrusters
        /// 3)Projector
        /// 4)Antenna
        /// 5)PB to put the script
        /// </summary>

        readonly string droneVersion = "V: 3.4.0";
        readonly MyIni _ini = new MyIni();
        double Wait;
        double ImWait = 8;
        IMyBroadcastListener _myBroadcastListener;
        bool setupCompleted;
        //readonly List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
        //readonly List<IMyTerminalBlock> functionBlocks = new List<IMyTerminalBlock>();
        //List<IMyTerminalBlock> allTB = new List<IMyTerminalBlock> ();
        //List<IMyTerminalBlock> updateBlocs = new List<IMyTerminalBlock>();
        readonly List<IMyCockpit> CockpitList = new List<IMyCockpit>();
        readonly List<IMyProjector> ProjectorList = new List<IMyProjector>();
        readonly List<IMyThrust> ThrustersList = new List<IMyThrust>();
        readonly List<IMyRadioAntenna> antennaList = new List<IMyRadioAntenna>();
        readonly List<IMyGasTank> tank = new List<IMyGasTank>();

        //check 100% integrity blocks
        //readonly List<IMyTerminalBlock> integrityBlocksList = new List<IMyTerminalBlock>();

        IMyShipController Cockpit;
        IMyProjector Projector;
        readonly string BroadcastTag = "channel_1";

        const string TagDefault = "[RPA]";

        Vector3D start;

        bool checkDistance;
        float thrust;
        float mass;
        readonly float acceleration = 0.4f; //wanted acceleration in m/s^2

        string TagCustom;
        readonly int ThrustersInGroup;

        ///safety distance to force stop
        double maxDistanceStop;
        double safetyDistanceStop = 0;
        //movement of the drone
        double DroneMovDistance = 1.5f;
        

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
        int welderSign;//check orientation between welders and rotor
        const double rotorToll = 0.04;
        double angle;

        bool updateTB = false;
        int refreshBlocks = 0;
        int ignoredBlocks = 0;
        const double firstRotationTimeMult = 0.8;

        //lcd printing strings
        readonly string[] lcd_printing_spinners = new string[] { "P", "PR", "PRI", "PRIN", "PRINT", "PRINTI", "PRINTIN", "PRINTING", "PRINTING.", "PRINTING..." };
        double lcd_printing_spinner_status;
        readonly string[] lcd_moving_spinners = new string[] {"MO","MOVI","MOVING.", "MOVING..."};
        double lcd_moving_spinner_status;
        readonly string[] lcd_spinners = new string[] { "-", "\\", "|", "/" };
        double lcd_spinner_status;
        double spinningWaitingStatus;
        const string lcd_divider = "--------------------------------";
        const string lcd_title =        "  RECKLESS PRINTING AUTOMATION  ";
        const string lcd_status_title = "     RPA Status Report      ";
        const string lcd_h2_level = "         TUG H2 LEVEL";
        const string lcd_proj_level =   "     PROJECTION LEVEL";
        readonly string lcd_header;
        bool imMoving = false;
        //string printingStatus;
        int totBlocks;
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
        bool activePrinting; //are TB integrity increasinh?
        double timeStep = 0;
        readonly double gyrosTolerance = 0.06; // 5 degrees
        readonly double maxGyroRotation = 0.43; // 25 degress
        //integrity check stuff
        double time=0;
        double checkTime = 0;
        int builtBlocks = 0;
        int sectionsBuilt = 0;
        int averageTime = 0;
        int totTime = 0;
        int maxTime = 0;
        int minTime = 100;
        int totRemaining=0;
        int endingBlocks = 0;
        int averageBlocks = 0;
        readonly List<IMyTerminalBlock> integrityListT0 = new List<IMyTerminalBlock>(); //list of all not 100 integrity blocks and not ignored blocks
        readonly Dictionary<IMyTerminalBlock, float> timeZeroDict = new Dictionary<IMyTerminalBlock, float>();
        readonly List<IMyTerminalBlock> ignoringList = new List<IMyTerminalBlock>(); //list of not 100% integrity blocks ignored
        IMyTerminalBlock activeWeldedBlockName;
        float activeWeldedBlockIntegrity=0;
        float newIntegrity=0;
        bool toggleAfterFinish = false; //auto toggle after finish printing, sent by start -toggle command
        readonly ImmutableList<string>.Builder toggleBuilder = ImmutableList.CreateBuilder<string>();
        ImmutableList<string> toggleList;
        readonly ImmutableList<string>.Builder startBuilder = ImmutableList.CreateBuilder<string>();
        ImmutableList<string> startIgnoringBlocks;

        public Program()
        {
            lcd_header = $"{lcd_divider}\n{lcd_title}\n{lcd_divider}";
            
            start = Me.GetPosition();
            Echo("Drone Log:\n");
            ///Listener (Antenna Inter Grid Communication)
            _myBroadcastListener = IGC.RegisterBroadcastListener(BroadcastTag);
            _myBroadcastListener.SetMessageCallback(BroadcastTag);
            ////////////////////////////////////////
            ///SETUP//////////////////////
            CustomData();
            SetupBlocks();
            if (setupCompleted)
            {
                ThrustersInGroup = ThrustersList.Count;
                Echo("DRONE SETUP COMPLETED!\nNumbers of thrusters in group: " + $"{ThrustersInGroup}\nCockpit Found \nProjector Found \nFuel Tank: {tank.Count}\nTag used: [{TagCustom}]");
                IGC.SendBroadcastMessage(BroadcastTag, $"    |DRONE SETUP COMPLETED!\n|Numbers of active thrusters: {ThrustersInGroup} \n|Cockpit Found \n|Projector Found\n|Fuel Tank: {tank.Count}\n|Tag used: [{TagCustom}]");
                
            }
            //if setup is not completed, the log in the SetupBlock() will tell us
            //else if(!setupCompleted)
            //{
            //    Echo("SETUP NOT COMPLETED!");
            //    IGC.SendBroadcastMessage(BroadcastTag, $"SETUP NOT COMPLETED!");
            //}
            

        }
        public void Main(string argument, UpdateType updateSource)
        {
            if ((updateSource & UpdateType.Update10) != 0 && aligningBool)
            {
                //Echo("aligning");
                ImAligning(ThrustersList);
            }
            if (checkDistance && Vector3D.Distance(start, Me.GetPosition()) >= DroneMovDistance)
            {
                DistanceCheck(ThrusterGroup: ThrustersList);
            }
            if (Wait >= firstRotationTimeMult * ImWait && firstRotation && !aligningBool)
            {
                RotorSpeedingUp();
                ActionTime(Cockpit, ThrustersList);
                time = 0;
            }
            if ((Wait < firstRotationTimeMult * ImWait || !firstRotation) && !aligningBool && !setupCommand)
            {
                //RotorSpeedingDown();
                newRotorSpeed = ConditionalRotorSpeed();
                IGC.SendBroadcastMessage(BroadcastTag, newRotorSpeed);
                ActionTime(Cockpit, ThrustersList);
            }
            if ((updateSource & UpdateType.IGC) > 0)
            {
                ImListening(Cockpit, Projector, ThrustersList);
            }
        }
        //public void UntagDrone()
        //{
        //    List<IMyTerminalBlock> everything = new List<IMyTerminalBlock>();
        //    GridTerminalSystem.GetBlocksOfType(everything, x => x.CustomName.Contains("." + TagCustom)
        //    ||
        //    x.CustomName.Contains(TagCustom));
        //    //Echo($"{everything.Count}");
        //    foreach (var block in everything)
        //    {
        //        try
        //        {
        //            block.CustomName = block.CustomName.Trim();
        //            block.CustomName = block.CustomName.Replace("." + TagCustom, "");
        //            block.CustomName.Replace(TagCustom, "");

        //        }
        //        catch
        //        {
        //            Echo("No tagged blocks found!");
        //        }
        //    }
        //}
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
            //gyros
            GridTerminalSystem.GetBlocksOfType(imGyroList);
            foreach(var gyro in imGyroList) { gyro.Enabled = true; }

            //hydrogen tank
            GridTerminalSystem.GetBlocksOfType(tank, x => x.CustomName.Contains(TagCustom));
            if (tank == null || tank.Count == 0)
            {
                GridTerminalSystem.GetBlocksOfType(tank);
                if (tank == null || tank.Count == 0)
                {
                    Echo("Add one Fuel tank for your Tug beratna.. come on you weirdo");
                    IGC.SendBroadcastMessage(BroadcastTag, $"{lcd_header}\n   SETUP NOT COMPLETED:\nAdd one Fuel tank for your Tug beratna.. come on you weird");
                    IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool>("DroneSetup", setupCompleted));
                    return;
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

            //SET COCKPIT, IF ONLY ONE, ASSIGN AUTOMATICALLY THE TAG
            GridTerminalSystem.GetBlocksOfType(CockpitList);
            if (CockpitList != null && CockpitList.Count == 1)
            {
                Cockpit = CockpitList[0];
                if (!Cockpit.CustomName.Contains(TagCustom))
                {
                    Cockpit.CustomName += "." + TagCustom;
                }

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
            
            if(blockTagged.Count == 0 && (ThrustersGroupsList == null || ThrustersGroupsList.Count == 0))
            {
                Echo($"Not Backward Thrusters group OR\nTagged Thrusters");
                IGC.SendBroadcastMessage(BroadcastTag, $"   SETUP NOT COMPLETED:\nNot Backward Thrusters group OR\nTagged Thrusters");
                IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool>("DroneSetup", setupCompleted));
                return;
            }
            if(blockTagged.Count == 0 && (ThrustersGroupsList == null || ThrustersGroupsList.Count > 1))
            {
                Echo($"More than 1 Backward Thruster Group Tagged!\nDelete one");
                IGC.SendBroadcastMessage(BroadcastTag, $"   SETUP NOT COMPLETED:\nMore than 1 Backward Thruster Group!\nDelete one");
                IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool>("DroneSetup", setupCompleted));
                return;
            }

            if (ThrustersGroupsList != null && ThrustersGroupsList.Count == 1)
            {
                ThrustersList.Clear();
                ThrustersGroupsList[0].GetBlocksOfType(ThrustersList, x=>x.Orientation.Forward == Cockpit.Orientation.Forward);
                if (ThrustersList == null || ThrustersList.Count==0)
                {
                    Echo($"Tagged Thrusters group has not only:\n        Backward Thrusters!");
                    IGC.SendBroadcastMessage(BroadcastTag, $"   SETUP NOT COMPLETED:\nTagged Thrusters group has not only:\n        Backward Thrusters!");
                    IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool>("DroneSetup", setupCompleted));
                    return;
                }
            }

            //sending the version of the script to the station
            IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, string>("droneVersion", droneVersion));
            //
            refreshBlocks = 0;
            builtBlocks = 0;
            sectionsBuilt = 0;
            averageTime = 0;
            totTime = 0;
            maxTime = 0;
            minTime = 100;
            totRemaining = 0;
            endingBlocks = 0;
            averageBlocks = 0;
            //SETUP COMPLETED
            setupCompleted = true;
        }
        public float ConditionalRotorSpeed()
        {
            time += Runtime.TimeSinceLastRun.TotalSeconds;
            if (updateTB)
            {
                time = 0;
                timeZeroDict.Clear();
                return RotorSpeed;
            }
            if (time <= 0.5f)
            {
                timeZeroDict.Clear();
                foreach (var block in integrityListT0)
                {
                    var integrity = block.CubeGrid.GetCubeBlock(block.Min).BuildLevelRatio;
                    if (!timeZeroDict.ContainsKey(block))
                    {
                        timeZeroDict.Add(block, integrity);
                    }
                    else
                    {
                        timeZeroDict[block] = integrity;
                    }
                }
            }

            if (time >= 1.2f)
            {
                foreach (var kv in timeZeroDict)
                {
                    checkTime += Runtime.TimeSinceLastRun.TotalSeconds;
                    activeWeldedBlockName = kv.Key;
                    activeWeldedBlockIntegrity = kv.Value;
                    newIntegrity = activeWeldedBlockName.CubeGrid.GetCubeBlock(activeWeldedBlockName.Min).BuildLevelRatio;
                    PrintingOnActiveBlock();
                    IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, string, string, string, string>(
                        checkTime.ToString(), activeWeldedBlockName.CustomName, activeWeldedBlockIntegrity.ToString(), 
                        newIntegrity.ToString(), angle.ToString()));
                    if (checkTime<=2)
                    {
                        IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, string, string, string, string>(
                        checkTime.ToString(), activeWeldedBlockName.CustomName, activeWeldedBlockIntegrity.ToString(),
                        newIntegrity.ToString(), angle.ToString()));
                        if (newIntegrity == 1)
                        {
                            //Echo("block deleted");
                            integrityListT0.Clear();
                            timeZeroDict.Clear();
                            activePrinting = false;
                            builtBlocks++;
                            totTime += (int)Math.Ceiling(time);
                            averageTime = totTime / builtBlocks;
                            if (time > maxTime) { maxTime = (int)Math.Ceiling(time); }
                            if (time < minTime) { minTime = (int)Math.Ceiling(time); }
                            PrintingOnActiveBlock();
                            time = 0;
                            checkTime = 0;
                            return RotorSpeed/2;
                        }
                        return RotorControl(activeWeldedBlockName);
                    }
                    else
                    {
                        checkTime = 0;
                        return RotorControl(activeWeldedBlockName);
                    } 
                }
            }
            return RotorSpeed;
        }
        
        public float RotorControl(IMyTerminalBlock block)
        {
            var blockPos = Vector3D.Normalize(block.GetPosition() - rotorPosition);
            var planeNormal = rotorHeadMatrix.Up;
            var blockProj = Vector3D.ProjectOnPlane(ref blockPos, ref planeNormal);
            if (welder_right) { rotorFacing = welderSign * rotorHeadMatrix.Right; }
            else if (welder_forward) { rotorFacing = welderSign * rotorHeadMatrix.Forward; }
            angle = Math.Atan2(blockProj.Cross(rotorFacing).Dot(planeNormal), rotorFacing.Dot(rotorFacing));
            float mult = (float)angle * 1.2f;
            //Echo($"rotorHead matrix: {rotorHeadMatrix}\n");
            //Echo($"mult: {mult}");
            //Echo($"angle: {angle / Math.PI * 180}");
            if (Math.Abs(angle) > rotorToll)
            {
                return RotorSpeed * mult;
            }
            else { return 0; }
        }
        public void RotorSpeedingUp()
        {
            newRotorSpeed = DynamicSpeed;
            IGC.SendBroadcastMessage(BroadcastTag, newRotorSpeed);
        }

        public void ActionTime(IMyShipController Cockpit, List<IMyThrust> ThrusterGroup)
        {
            PrintingBlocksListCreation();

            if (imMoving)
            {
                Wait = ImWait;
                firstRotation = false;
            }
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            Wait -= Runtime.TimeSinceLastRun.TotalSeconds;
            
            if (updateTB)
            {
                GridTerminalSystem.GetBlocksOfType(ignoringList, x => !x.CubeGrid.GetCubeBlock(x.Min).IsFullIntegrity);
                PrintingBlocksListCreation();
                refreshBlocks = ignoringList.Count;
                //Echo($"refreshed Blocks = {refreshBlocks}");
                updateTB = false;
            }
            int remainingTB = integrityListT0.Count;
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

                if (remainingTB <= 0)
                {
                    start = Me.GetPosition();
                    PrintingResults(totRemaining, remainingTB, safetyDistanceStop);
                    Movement(Cockpit, ThrusterGroup, totRemaining, remainingTB);
                }

                //check for stopping status (finished printing)
                if (totRemaining == 0)
                {
                    Runtime.UpdateFrequency = UpdateFrequency.None;
                    Wait = 0;
                    Stop(ThrusterGroup);
                    checkDistance = false;
                    firstRotation = false;
                    activation = false;
                    IGC.SendBroadcastMessage(BroadcastTag,new MyTuple<string, bool> ("activation", activation));
                    if (toggleAfterFinish)
                    { ToggleOn(toggleList.Clear()); }
                    foreach (var gyro in imGyroList) { gyro.GyroOverride = false; }
                    Echo($"Ship Printed!\n{toggleAfterFinish}");
                    IGC.SendBroadcastMessage(BroadcastTag, $"\nShip Printed!\nToggle: {toggleAfterFinish}");
                    return;
                }
                if (safetyDistanceStop >= maxDistanceStop)
                {
                    Runtime.UpdateFrequency = UpdateFrequency.None;
                    Wait = 0;
                    checkDistance = false;
                    firstRotation = false;
                    Stop(ThrusterGroup);
                    foreach (var gyro in imGyroList) { gyro.GyroOverride = false; }
                    IGC.SendBroadcastMessage(BroadcastTag, "\nSafety Distance Reached. Tug stopped.");
                    activation = false;
                    IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool>("activation", activation));
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
        void ImListening(IMyShipController Cockpit, IMyProjector Projector, List<IMyThrust> ThrusterGroup)
        {
            while (_myBroadcastListener.HasPendingMessage)
            {
                var myIGCMessage = _myBroadcastListener.AcceptMessage();
                if (myIGCMessage.Tag == BroadcastTag && myIGCMessage.Data is string)
                {
                    switch (myIGCMessage.Data.ToString().ToLower())
                    {
                        case "start":
                            firstRotation = true;
                            setupCommand = false;
                            aligningBool = true;
                            time = 0;
                            PrintingBlocksListCreation();
                            totBlocks = TotalBlocks();
                            Wait = ImWait;
                            Runtime.UpdateFrequency = UpdateFrequency.Update10;
                            activation = true;
                            IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool>("activation", activation));
                            mass = Cockpit.CalculateShipMass().PhysicalMass;
                            int remainingTB = integrityListT0.Count;
                            totRemaining = Projector.RemainingBlocks - refreshBlocks;
                            safetyDistanceStop = Math.Round(Vector3D.Distance(rotorPosition, Cockpit.GetPosition()), 2);
                            imMoving = false;
                            PrintingResults(totRemaining, remainingTB, safetyDistanceStop);
                            Stop(ThrusterGroup); 

                            break;

                        case "projector":
                            Projecting(Projector);
                            break;

                        case "stop":
                            Runtime.UpdateFrequency = UpdateFrequency.None;
                            Wait = ImWait;
                            firstRotation=false;
                            checkDistance = false;
                            Stop(ThrusterGroup);
                            activation = false;
                            IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool>("activation", activation));
                            IGC.SendBroadcastMessage(BroadcastTag, "Stopping command processed.");
                            IGC.SendBroadcastMessage(BroadcastTag, newRotorSpeed = 0);
                            foreach (var gyro in imGyroList) { gyro.GyroOverride = false; }
                            IGC.DisableBroadcastListener(_myBroadcastListener);
                            _myBroadcastListener = IGC.RegisterBroadcastListener(BroadcastTag);
                            _myBroadcastListener.SetMessageCallback(BroadcastTag);
                            break;

                        case "skip":
                            start = Me.GetPosition();
                            remainingTB = integrityListT0.Count
                                          ;
                            totRemaining = Projector.RemainingBlocks - refreshBlocks;
                            Movement(Cockpit, ThrusterGroup, totRemaining, remainingTB);
                            IGC.SendBroadcastMessage(BroadcastTag, "Backward movement processed"); 
                            break;

                        case "update":
                            updateTB = true;
                            break;

                            //case "untag_d":
                            //    Runtime.UpdateFrequency = UpdateFrequency.None;
                            //    Wait = 0;
                            //    checkDistance = false;
                            //    Stop(ThrusterGroup);
                            //    try
                            //    {
                            //        UntagDrone();
                            //        IGC.SendBroadcastMessage(BroadcastTag, "Drone untagged");
                            //        activation = false;
                            //        IGC.SendBroadcastMessage(BroadcastTag, activation);
                            //    }
                            //    catch
                            //    {
                            //        Echo("No tagged blocks found!");
                            //        IGC.SendBroadcastMessage(BroadcastTag, "No tagged blocks found!");
                            //    }
                            //    break;
                    }
                }
                //immutable list from toggle and start
                if(myIGCMessage.Data is ImmutableList<string>)
                {
                    var immutable = (ImmutableList<string>)myIGCMessage.Data;
                    //Echo($"{immutable.Count}");
                    //\n{toggleList[0]}\n{toggleList[1]}
                    string command = immutable[0].ToLower();
                    if(command == "toggle")
                    {
                        //Echo("yes toggle");
                        if (immutable.Count>1)
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
                        checkDistance = false;
                        Stop(ThrusterGroup);
                        activation = false;
                        ToggleOn(toggleList);
                        IGC.SendBroadcastMessage(BroadcastTag, "Toggle On blocks, except for Epsteins and Tools");
                        IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool>("activation", activation));
                        IGC.DisableBroadcastListener(_myBroadcastListener);
                        _myBroadcastListener = IGC.RegisterBroadcastListener(BroadcastTag);
                        _myBroadcastListener.SetMessageCallback(BroadcastTag);
                    }
                    else if(command=="start")
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
                if(myIGCMessage.Data is MyTuple<string, bool>)
                {
                    var tuple = (MyTuple<string, bool>)myIGCMessage.Data;
                    string command = tuple.Item1;
                    bool toggleYes = tuple.Item2;
                    if(command == "toggleAfterFinish")
                    {
                        toggleAfterFinish = toggleYes;
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
                if (myIGCMessage.Data is MyTuple<string, bool, bool, MatrixD>)
                {
                    var tuple = (MyTuple<string, bool, bool, MatrixD>)myIGCMessage.Data;
                    string command = tuple.Item1;
                    if (command == "rotorHead")
                    {
                        welder_right = tuple.Item2;
                        welder_forward = tuple.Item3;
                        rotorHeadMatrix = tuple.Item4;
                    }
                }

                //setup commands
                if (myIGCMessage.Data is MyTuple<double, float, double, float, double, MyTuple<MatrixD, int>>)
                {
                    IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, string>("droneVersion", droneVersion));
                    CustomData();
                    SetupBlocks();
                    Runtime.UpdateFrequency = UpdateFrequency.None;
                    setupCommand = true;
                    Stop(ThrusterGroup);
                    checkDistance = false;
                    firstRotation = false;
                    var tuple = (MyTuple<double, float, double, float, double, MyTuple<MatrixD, int>>)myIGCMessage.Data;
                    ImWait = tuple.Item1;
                    DynamicSpeed = tuple.Item2;
                    DroneMovDistance = tuple.Item3;
                    RotorSpeed = tuple.Item4;
                    maxDistanceStop = tuple.Item5;
                    rotorMatrix = tuple.Item6.Item1;
                    welderSign = tuple.Item6.Item2;
                    rotorPosition = rotorMatrix.Translation;
                    rotorOrientation = rotorMatrix.GetOrientation();
                    string output_tuple = $"       SETUP COMPLETED       \n{lcd_divider}\n" +
                        $"{"|Wait",-16} {"= " + ImWait + " seconds;",-17}\n" +
                        $"{"|DroneMovement",-16} {"= " + DroneMovDistance + " meters;",5}\n" +
                        $"{"|Rotor Speed",-16} {"= " + RotorSpeed + " RPM;",-19}\n" +
                        $"{"|Dynamic Speed",-16} {"= " + DynamicSpeed + " RPM;",8}\n" +
                        $"{"|Safety Distance",-16} {"= " + maxDistanceStop + " meters",-15}";
                    Echo(output_tuple);
                    IGC.SendBroadcastMessage(BroadcastTag, output_tuple);
                    IGC.DisableBroadcastListener(_myBroadcastListener);
                    _myBroadcastListener = IGC.RegisterBroadcastListener(BroadcastTag);
                    _myBroadcastListener.SetMessageCallback(BroadcastTag);
                }
                //alignment drone
                if(myIGCMessage.Data is MatrixD)
                {
                    Stop(ThrusterGroup);
                    setupCommand = true;
                    Runtime.UpdateFrequency = UpdateFrequency.Update10;
                    checkDistance = false;
                    firstRotation = false;
                    Wait = ImWait;
                    aligningBool = true;
                    activation = false;
                    rotorOrientation = (MatrixD)myIGCMessage.Data;
                    IGC.SendBroadcastMessage(BroadcastTag, "Drone is aligning.");
                    IGC.SendBroadcastMessage(BroadcastTag, activation);
                    IGC.DisableBroadcastListener(_myBroadcastListener);
                    _myBroadcastListener = IGC.RegisterBroadcastListener(BroadcastTag);
                    _myBroadcastListener.SetMessageCallback(BroadcastTag);
                }
            }
        }
        public void ImAligning(List<IMyThrust> ThrusterGroup)
        {
            if(rotorMatrix == MatrixD.Zero)
            {
                IGC.SendBroadcastMessage(BroadcastTag, "Run setup first!");
            }
            else
            {
                string myString = "weldersToggle";
                weldersToggleOn = false;
                IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool>(myString, weldersToggleOn));
                double pitch, yaw, roll;
                //the desiredForward and UP are the rotor's: the cockpit of the tug will be aligned with the downward direction of the rotor!!
                var desiredForward = -rotorOrientation.Up;
                var desiredUp = Vector3D.Zero; // IF the ROLL angle is important use " -rotorOrientation.Forward";
                GetRotationAnglesSimultaneous(desiredForward, desiredUp, Cockpit.WorldMatrix, out yaw, out pitch, out roll);
                ApplyGyroOverride(pitch, yaw, roll, imGyroList, Cockpit.WorldMatrix, ThrusterGroup);
            }
        }

        //turn on all blocks
        public void ToggleOn(ImmutableList<string> toggleList)
        {
            //foreach(var toggle in toggleList)
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
            
            if (toggleList!=null && toggleList.Count>0)
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
                GridTerminalSystem.GetBlocksOfType(allProj, x=>!x.CustomName.Contains(TagCustom));
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
                if(allProj!=null && allProj.Count > 0)
                {
                    foreach(var proj in allProj)
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
            string printingOutput;
            string spinningSymbols;
            double tankLevel = 0;

            if (Projector.IsProjecting && totBlocks>0)
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

            string[] waitingSpinnerSymbols = new string[] {Math.Round(Wait).ToString(), " " };
            spinningWaitingStatus += Runtime.TimeSinceLastRun.TotalSeconds;
            lcd_spinner_status += Runtime.TimeSinceLastRun.TotalSeconds;
            lcd_moving_spinner_status += Runtime.TimeSinceLastRun.TotalSeconds;
            lcd_printing_spinner_status += Runtime.TimeSinceLastRun.TotalSeconds;
            if (lcd_spinner_status > lcd_spinners.Length) lcd_spinner_status = 0;
            
            spinningSymbols = lcd_spinners[(int)lcd_spinner_status];
            var remainingDist = maxDistanceStop - safetyDisanceStop;
            StringBuilder printingStatus = new StringBuilder(lcd_divider + "\n" + spinningSymbols +
                spinningSymbols + lcd_status_title + spinningSymbols + spinningSymbols + "\n" + lcd_divider + "\n");
            //retrieve a list of unfinished blocks and their integrity
            List<MyTuple<string, float>> statusList = StatusLCD();
            //Echo($"{statusList.Count}");
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
            printingOutput = "              " + spinner + "          \n" + lcd_divider + "\n" +
            $"{"Projection", -27}" + $"{imProjecting, 5}\n" +
            $"{lcd_divider}\n" +
            $"{"|Total Blocks", -27}" + $"{"= " + totBlocks}\n" +
            $"{"|Total remaining blocks",-27}" + $"{"= " + totRemaining + " (" + refreshBlocks + ")"};\n" +
            $"{"|Missing TB for section", -27}" + $"{"= " + remainingTB + " (" + refreshBlocks + ")"};\n" +
            $"{"|Ignoring TB from Start",-27}" + $"{"= " + ignoredBlocks};\n" +
            $"{"|Seconds till next check", -27}" + $"{"= " + waitingSpinner};\n" +
            $"{"|Distance", -27}" + $"{ "= " + instsPos + " meters;\n"}" +
            $"{"|Safety Dist Remaining", -27}" + $"{"= " + remainingDist + " meters"};\n" +
            $"{"|N° Active Gyros", -27}" + $"{"= " + imGyroList.Count};\n" +
            $"{"|Grid Total Mass",-27}" + $"{"= " + Math.Round(mass / 1000f, 0) + " tons;\n"}" +
            $"{"|Rotor Speed", -27}" + $"{"= " +Math.Round(newRotorSpeed, 2) + " RPM;\n"}" +
            $"{lcd_divider}\n" +
            $"Toggle After Print: {toggleAfterFinish}\n"+
            $"{lcd_divider}\n" + $"{lcd_h2_level+": " + Math.Round(tankLevel * 100, 2) + "%      \n"}" +
            //tank multiplier and totBlockMultiplier are multiplicated by 3 cause the string is long 32 digit
            $"{"0% " + string.Concat(Enumerable.Repeat("=", tankMultiplier * 3)), -33}" + $"{" 100%", 4}\n" +
            $"{lcd_proj_level + ": " + totBlockPercentage + "%      "}\n" +
            $"{"0% " + string.Concat(Enumerable.Repeat("=", totBlockMultiplier * 3)), -33}" + $"{" 100%", 4}\n" +
            $"{lcd_divider}"
            ;
            //PRINT ON STATUS LCD
            foreach (var tuple in statusList)
            {
                string name = tuple.Item1;
                float integrity = tuple.Item2;
                //numbers in string format is the padding
                printingStatus.Append($"{name, -26}{integrity, 5}%\r\n");
                //Echo(printingOutput);
            }
          
            IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, string>(printingOutput, printingStatus.ToString()));
            statusList.Clear();
        }
        public void PrintingOnActiveBlock()
        {
            int totTimeETA = 0;
            if(sectionsBuilt>2)
            {
                //extimated time with some semi viable constants, like 60=number of average sections (150 meter lonf ship)
                //totBlocks/20 ratio between functional and armor blocks
                //divided by 60 to convert in minutes
                totTimeETA = (int)Math.Ceiling((60*ImWait + averageTime*totBlocks/20)/60);
            }
            //PRINT ON ACTIVE LCD
            string activeOutput = $"{lcd_divider}\n          ACTIVE WELDING\n{lcd_divider}\n"
                + $"{activeWeldedBlockName.CustomName,-26}{Math.Round(newIntegrity * 100, 2),5}%\n" +
                $"{"Active Welding time",-27}{"= " +(int)Math.Round(time,0) + " s",5}\n" +
                $"{lcd_divider}\n" +
                $"             STATS\n{lcd_divider}\n" +
                $"{"Section N°", -25}{"= " + sectionsBuilt, 7}\n" +
                $"{"Total printing time", -25}{"= " + totTime, 7}\n" +
                $"{"Average time",-25}{"= " + averageTime + " s",7}\n" +
                $"{"Max time",-25}{"= " + maxTime + " s",7}\n" +
                $"{"Min time",-25}{"= " + minTime + " s",7}\n" +
                $"{"Average Blocks/section", -24}{"= " + averageBlocks, 8}\n" +
                $"{lcd_divider}\n" +
                $"{"ETA", -19}{"= " +totTimeETA + " minutes", 13}"
                ;
            IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, string>("ActiveWelding", activeOutput));
        }

        public List<MyTuple<string, float>> StatusLCD()
        {
            MyTuple <string, float> MyTuple = new MyTuple<string, float>();
            List<MyTuple<string, float>> MyList = new List<MyTuple<string, float>>();
            
            for (int i = 0; i < integrityListT0.Count; i++)
            {
                var block = integrityListT0[i];
                string name = block.CustomName;
                float integrity = block.CubeGrid.GetCubeBlock(block.Min).BuildLevelRatio*100;
                MyTuple.Item1 = name;
                MyTuple.Item2 = (float)Math.Round(integrity, 2);
                MyList.Add(MyTuple);
            }
            return MyList;
        }
        public void Projecting(IMyProjector Projector)
        {
            Projector.Enabled = !Projector.Enabled;
            IGC.SendBroadcastMessage(BroadcastTag, "Projector On/Off \n");
        }

        public void Movement(IMyShipController Cockpit, List<IMyThrust> ThrusterGroup, int totRemaining, int remainingTB)
        {
            imMoving = true;
            checkDistance = true;
            firstRotation = false;
            Wait = ImWait;
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            aligningBool = true;
            mass = Cockpit.CalculateShipMass().PhysicalMass;
            thrust = (mass * acceleration) / (ThrustersInGroup);
            safetyDistanceStop = Math.Round(Vector3D.Distance(rotorPosition, Me.GetPosition()), 2);
            PrintingResults(totRemaining, remainingTB, safetyDistanceStop);
            //stats counting at the end of the section
            sectionsBuilt++;
            endingBlocks = totBlocks - totRemaining;
            averageBlocks = endingBlocks / sectionsBuilt;

            foreach (var thrusters in ThrusterGroup)
            {
                thrusters.ThrustOverride = thrust;
            }
        }

        public void DistanceCheck(List<IMyThrust> ThrusterGroup)
        {
            //check if distance has been covered
            checkDistance = false;
            imMoving = false;
            Stop(ThrusterGroup);
            Wait = ImWait;
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            firstRotation = true;
            aligningBool = false;
        }

        public void Stop(List<IMyThrust> ThrusterGroup)
        {
            foreach (var thrusters in ThrusterGroup)
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

        void ApplyGyroOverride(double pitchSpeed, double yawSpeed, double rollSpeed, List<IMyGyro> gyroList, MatrixD worldMatrix, List<IMyThrust> ThrusterGroup)
        {
            //Echo("applygyro");
            GridTerminalSystem.GetBlocksOfType(imGyroList);
            foreach (var gyro in imGyroList) { gyro.Enabled = true; }
            var rotationVec = new Vector3D(pitchSpeed, yawSpeed, rollSpeed);
            var relativeRotationVec = Vector3D.TransformNormal(rotationVec, worldMatrix);
            
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
                        checkDistance = false;
                        firstRotation = false;
                        weldersToggleOn = false;
                        IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool>("weldersToggle", weldersToggleOn));
                        IGC.SendBroadcastMessage(BroadcastTag, "Drone is aligned.");
                        foreach (var gyro in imGyroList) { gyro.GyroOverride = false; }
                        timeStep = 0;
                    }
                    else if (!setupCommand)
                    {
                        Runtime.UpdateFrequency = UpdateFrequency.Update10;
                        weldersToggleOn = true;
                        IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool>("weldersToggle", weldersToggleOn));
                        IGC.SendBroadcastMessage(BroadcastTag, "Drone is aligned.");
                        foreach (var gyro in imGyroList) { gyro.GyroOverride = false; }
                        timeStep = 0;
                        Wait = ImWait;
                        aligningBool = false;
                        firstRotation = true;
                    }
                }
            }
            if (timeStep > 5)
            {
                IGC.SendBroadcastMessage(BroadcastTag, "Drone cannot align. Security stop imminent");
                Runtime.UpdateFrequency = UpdateFrequency.None;
                Wait = 0;
                checkDistance = false;
                Stop(ThrusterGroup);
                activation = false;
                IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool>("activation", activation));
                foreach (var gyro in imGyroList) { gyro.GyroOverride = false; }
                timeStep = 0;
                IGC.DisableBroadcastListener(_myBroadcastListener);
                _myBroadcastListener = IGC.RegisterBroadcastListener(BroadcastTag);
                _myBroadcastListener.SetMessageCallback(BroadcastTag);
            }
            if (Math.Abs(pitchSpeed) > maxGyroRotation || Math.Abs(yawSpeed) > maxGyroRotation)
            {
                IGC.SendBroadcastMessage(BroadcastTag, "Align the drone manually below\n  25 degrees, before auto alignment!");
                Runtime.UpdateFrequency = UpdateFrequency.None;
                Wait = 0;
                checkDistance = false;
                Stop(ThrusterGroup);
                activation = false;
                IGC.SendBroadcastMessage(BroadcastTag, new MyTuple<string, bool>("activation", activation));
                foreach (var gyro in imGyroList) { gyro.GyroOverride = false; }
                timeStep = 0;
                IGC.DisableBroadcastListener(_myBroadcastListener);
                _myBroadcastListener = IGC.RegisterBroadcastListener(BroadcastTag);
                _myBroadcastListener.SetMessageCallback(BroadcastTag);
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


    }
}