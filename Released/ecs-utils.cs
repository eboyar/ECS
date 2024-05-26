//General purpose utilities for APCK(and other) drones, single file
//by eboyar
#region FIELDS

const string version = "1.1.7";

List<string> DDs = new List<string>();

List<AssemblyBay> assemblyBays = new List<AssemblyBay>();
List<Printer> printers = new List<Printer>();
List<IMyShipConnector> dockingPorts = new List<IMyShipConnector>();

IMyProgrammableBlock APCK;
List<Container> containers = new List<Container>();

int runCounter = 0;
int loopCounter = 0;
const int maxIterations = 1000;

string disposableDroneAssemblerTag = "DDA";
string nonDisposableDroneAssemblerTag = "Printer";
string disposableDroneTag = "ASD";
string nonDisposableDroneTag = "ASC";

bool splitQueue = false;
bool creativeMode = false;
bool displayLock = false;

int safetyNet = 4;
int reloadsPerUpdate = 1;
int scansPerUpdate = 50;

float fastExtensionRate = 2f;
float slowExtensionRate = 0.05f;
int stepTimeout = 2;
const float stepDistance = 0.5f;

MyCommandLine cL = new MyCommandLine();
MyIni ini = new MyIni();
Scheduler scheduler = new Scheduler();
Scheduler splitScheduler = new Scheduler();
StatusLogger logger;
StatusDisplay statusDisplay;

int cacheClock = 0;
int cacheInterval = 1800;
Queue<double> runtimes = new Queue<double>();
const int avgRuntimes = 33;

List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>(2000);
List<IMyTerminalBlock> splitBlocks = new List<IMyTerminalBlock>(2000);
List<IMyShipMergeBlock> tempMergeBlocks = new List<IMyShipMergeBlock>();
List<IMyShipConnector> tempConnectors = new List<IMyShipConnector>();
List<IMyProjector> tempProjectors = new List<IMyProjector>();
List<IMyTerminalBlock> tempBlocksToResupply = new List<IMyTerminalBlock>(500);
List<IMyGasTank> tempHydrogenTanks = new List<IMyGasTank>(100);

List<IMyBlockGroup> blockGroups = new List<IMyBlockGroup>(80);
List<IMyBlockGroup> splitBlockGroups = new List<IMyBlockGroup>(80);
List<IMyBlockGroup> selectedBlockGroups = new List<IMyBlockGroup>(15);


//stuff for fast lookups
static readonly MyItemType
    uranium = new MyItemType("MyObjectBuilder_Ingot", "Uranium"),
    scatterAmmo = new MyItemType("MyObjectBuilder_AmmoMagazine", "SemiAutoPistolMagazine"),
    gatlingAmmo = new MyItemType("MyObjectBuilder_AmmoMagazine", "NATO_25x184mm"),
    missileAmmo = new MyItemType("MyObjectBuilder_AmmoMagazine", "Missile200mm"),
    autocannonAmmo = new MyItemType("MyObjectBuilder_AmmoMagazine", "AutocannonClip"),
    assaultAmmo = new MyItemType("MyObjectBuilder_AmmoMagazine", "MediumCalibreAmmo"),
    artilleryAmmo = new MyItemType("MyObjectBuilder_AmmoMagazine", "LargeCalibreAmmo"),
    smallRailgunAmmo = new MyItemType("MyObjectBuilder_AmmoMagazine", "SmallRailgunAmmo"),
    largeRailgunAmmo = new MyItemType("MyObjectBuilder_AmmoMagazine", "LargeRailgunAmmo");

static readonly Dictionary<string, MyItemType> itemTypes = new Dictionary<string, MyItemType>
        {
            { "uranium", uranium },
            { "scatterAmmo", scatterAmmo },
            { "gatlingAmmo", gatlingAmmo },
            { "missileAmmo", missileAmmo },
            { "autocannonAmmo", autocannonAmmo },
            { "assaultAmmo", assaultAmmo },
            { "artilleryAmmo", artilleryAmmo },
            { "smallRailgunAmmo", smallRailgunAmmo },
            { "largeRailgunAmmo", largeRailgunAmmo }
        };

static readonly MyDefinitionId
    smallRocketLauncherReload = MyDefinitionId.Parse("MyObjectBuilder_SmallMissileLauncherReload/SmallRocketLauncherReload"),
    smallGatlingGun = MyDefinitionId.Parse("MyObjectBuilder_SmallGatlingGun/"),
    smallGatlingGunWarfare2 = MyDefinitionId.Parse("MyObjectBuilder_SmallGatlingGun/SmallGatlingGunWarfare2"),
    smallBlockAutocannon = MyDefinitionId.Parse("MyObjectBuilder_SmallGatlingGun/SmallBlockAutocannon"),
    smallBlockMediumCalibreGun = MyDefinitionId.Parse("MyObjectBuilder_SmallMissileLauncherReload/SmallBlockMediumCalibreGun"),
    smallRailgun = MyDefinitionId.Parse("MyObjectBuilder_SmallMissileLauncherReload/SmallRailgun"),
    largeRailgun = MyDefinitionId.Parse("MyObjectBuilder_SmallMissileLauncherReload/LargeRailgun"),
    largeBlockLargeCalibreGun = MyDefinitionId.Parse("MyObjectBuilder_SmallMissileLauncher/LargeBlockLargeCalibreGun"),
    largeMissileLauncher = MyDefinitionId.Parse("MyObjectBuilder_SmallMissileLauncher/LargeMissileLauncher"),
    smallGatlingTurret = MyDefinitionId.Parse("MyObjectBuilder_LargeGatlingTurret/SmallGatlingTurret"),
    largeGatlingTurret = MyDefinitionId.Parse("MyObjectBuilder_LargeGatlingTurret/"),
    autoCannonTurret = MyDefinitionId.Parse("MyObjectBuilder_LargeGatlingTurret/AutoCannonTurret"),
    smallMediumCalibreTurret = MyDefinitionId.Parse("MyObjectBuilder_LargeMissileTurret/SmallBlockMediumCalibreTurret"),
    smallMissileTurret = MyDefinitionId.Parse("MyObjectBuilder_LargeMissileTurret/SmallMissileTurret"),
    largeMissileTurret = MyDefinitionId.Parse("MyObjectBuilder_LargeMissileTurret/"),
    largeBlockMediumCalibreTurret = MyDefinitionId.Parse("MyObjectBuilder_LargeMissileTurret/LargeBlockMediumCalibreTurret"),
    largeCalibreTurret = MyDefinitionId.Parse("MyObjectBuilder_LargeMissileTurret/LargeCalibreTurret");

#endregion FIELDS


#region MAIN

Program()
        {
    if (!string.IsNullOrEmpty(Me.CustomData)) ParseConfig();
    else WriteConfig();

    logger = new StatusLogger(splitQueue);
    Runtime.UpdateFrequency |= UpdateFrequency.Update10;
    scheduler.AddRoutine(Setup());
}

void Main(string argument, UpdateType updateSource)
{

    if (cL.TryParse(argument))
    {
        Command(argument);
    }
    if ((updateSource & UpdateType.Update10) != 0)
    {
        scheduler.ExecuteRoutine();
        if (splitQueue)
        {
            splitScheduler.ExecuteRoutine();
        }

        cacheClock++;
        if (cacheClock >= cacheInterval)
        {
            cacheClock = 0;
            if (splitQueue)
            {
                splitScheduler.AddRoutine(UpdateInventories());
            }
            else
            {
                scheduler.AddRoutine(UpdateInventories());
            }
        }

        WriteStatus();

        if (scheduler.IsActive())
        {
            displayLock = false;
        }
        else if (!displayLock)
        {
            DisplayStatus(null, null);
            displayLock = true;
        }
    }
}

#endregion MAIN


#region ROUTINES

IEnumerator<bool> Setup()
{
    logger.RReport("Running setup");
    yield return true;

    GridTerminalSystem.GetBlocksOfType(blocks, null);
    yield return true;

    runCounter = 0;

    //first pass, create printer abstraction
    logger.RReport("Getting printers");
    foreach (var b in blocks)
    {
        if (!b.IsSameConstructAs(Me))
        {
            runCounter++;
            continue;
        }

        if (b is IMyShipWelder && b.CustomName.Contains(nonDisposableDroneAssemblerTag))
        {
            var printerNumber = int.Parse(b.CustomName.Split(' ')[1]);
            var printer = printers.FirstOrDefault(p => p.Number == printerNumber);
            var welder = b as IMyShipWelder;

            if (printer != null)
            {
                printer.Welders.Add(welder);
            }
            else
            {
                var newPrinter = new Printer(printerNumber);
                newPrinter.Welders.Add(welder);
                printers.Add(newPrinter);
            }
            runCounter++;
        }
        else if (b is IMyProjector && b.CustomName.Contains(nonDisposableDroneAssemblerTag))
        {
            var printerNumber = int.Parse(b.CustomName.Split(' ')[2]);
            var printer = printers.FirstOrDefault(p => p.Number == printerNumber);
            var projector = b as IMyProjector;

            if (printer != null)
            {
                printer.Projectors.Add(projector);
            }
            else
            {
                var newPrinter = new Printer(printerNumber);
                newPrinter.Projectors.Add(projector);
                printers.Add(newPrinter);
            }
            runCounter++;
        }
        else if (b is IMyPistonBase && b.CustomName.Contains(nonDisposableDroneAssemblerTag))
        {
            var printerNumber = int.Parse(b.CustomName.Split(' ')[1]);
            var printer = printers.FirstOrDefault(p => p.Number == printerNumber);
            var piston = b as IMyPistonBase;

            if (printer != null)
            {
                printer.Pistons.Add(piston);
            }
            else
            {
                var newPrinter = new Printer(printerNumber);
                newPrinter.Pistons.Add(piston);
                printers.Add(newPrinter);
                newPrinter.ExtensionLimit = (piston).MaxLimit;
            }

            runCounter++;
        }
        else if (b is IMyShipMergeBlock && b.CustomName.Contains(nonDisposableDroneAssemblerTag))
        {
            var printerNumber = int.Parse(b.CustomName.Split(' ')[1]);
            var printer = printers.FirstOrDefault(p => p.Number == printerNumber);
            var merge = b as IMyShipMergeBlock;

            if (printer != null)
            {
                printer.Merge = merge;
            }
            else
            {
                var newPrinter = new Printer(printerNumber);
                newPrinter.Merge = merge;
                printers.Add(newPrinter);
            }
            runCounter++;
        }
        else if (b is IMyTextPanel && b.CustomName.Contains("ecs-util"))
        {
            var disp = b as IMyTextSurfaceProvider;
            var surf = disp.GetSurface(0);
            statusDisplay = new StatusDisplay(surf);
            DisplayStatus("MAIN", "Setup Initiated");
        }
        if (runCounter >= 25)
        {
            runCounter = 0;
            yield return true;
        }
    }

    //second pass, prep for bay abstraction
    logger.RReport("Getting assembly bays");
    foreach (var b in blocks)
    {
        if (!b.IsSameConstructAs(Me))
        {
            runCounter++;
            continue;
        }

        if (b.CustomName.Contains(disposableDroneAssemblerTag))
        {
            string tag = b.CustomName.Split(' ')[0];
            if (!DDs.Contains(tag))
            {
                DDs.Add(tag);
            }
            runCounter++;
        }

        if (b is IMyShipMergeBlock && b.CustomName.Contains(disposableDroneAssemblerTag))
        {
            tempMergeBlocks.Add(b as IMyShipMergeBlock);
            runCounter++;
        }
        else if (b is IMyShipConnector)
        {
            if (b.CustomName.Contains(disposableDroneAssemblerTag))
            {
                tempConnectors.Add(b as IMyShipConnector);
            }
            else if (b.CustomName.Contains("dock-host"))
            {
                dockingPorts.Add(b as IMyShipConnector);
            }
            runCounter++;
        }
        else if (b is IMyProjector && b.CustomName.Contains(disposableDroneAssemblerTag))
        {
            tempProjectors.Add(b as IMyProjector);
            runCounter++;
        }
        else if (b is IMyCargoContainer && b.CustomName.Contains("resupply"))
        {
            containers.Add(new Container(b as IMyCargoContainer, itemTypes));
            runCounter += 10;
        }
        else if (b is IMyShipWelder && b.CustomName.Contains(disposableDroneAssemblerTag))
        {
            var tag = b.CustomName.Split(' ')[0];
            var welderNumber = int.Parse(b.CustomName.Split(' ')[2]);
            var mBay = assemblyBays.FirstOrDefault(bay => bay.Type == tag && bay.Number == welderNumber);

            if (mBay != null)
            {
                mBay.Welders.Add(b as IMyShipWelder);
            }
            else
            {
                var newBay = new AssemblyBay(tag, welderNumber);
                newBay.Welders.Add(b as IMyShipWelder);
                assemblyBays.Add(newBay);
            }
            runCounter++;
        }
        else if (b is IMyTimerBlock && b.CustomName.Contains(disposableDroneAssemblerTag))
        {
            var tag = b.CustomName.Split(' ')[0];
            var timerNumber = int.Parse(b.CustomName.Split(' ')[2]);
            var mBay = assemblyBays.FirstOrDefault(bay => bay.Type == tag && bay.Number == timerNumber);

            if (mBay != null)
            {
                mBay.Timer = b as IMyTimerBlock;
            }
            else
            {
                var newBay = new AssemblyBay(tag, timerNumber);
                newBay.Timer = b as IMyTimerBlock;
                assemblyBays.Add(newBay);
            }
            runCounter++;
        }
        else if (b is IMyProgrammableBlock && b.CustomName.Contains("a-core"))
        {
            APCK = b as IMyProgrammableBlock;
            runCounter++;
        }
        if (runCounter >= 25)
        {
            runCounter = 0;
            yield return true;
        }
    }

    yield return true;

    //create bay abstraction
    logger.RReport("Pairing assembly bays");
    while (tempMergeBlocks.Count > 0 && tempConnectors.Count > 0 && tempProjectors.Count > 0)
    {
        var mergeBlock = tempMergeBlocks[0];
        var mergePosition = mergeBlock.GetPosition();
        var connector = FindClosestBlock(tempConnectors, mergePosition);

        string[] parts = mergeBlock.CustomName.Split(' ');
        if (parts.Length >= 3 && DDs.Any(dd => dd == parts[0]) && parts[1] == disposableDroneAssemblerTag)
        {
            string bayTag = parts[0];
            int bayNumber = int.Parse(parts[2]);
            AssemblyBay bay = assemblyBays.FirstOrDefault(ab => ab.Number == bayNumber && ab.Type == bayTag);
            if (bay == null)
            {
                bay = new AssemblyBay(bayTag, bayNumber);
                assemblyBays.Add(bay);
                runCounter += 2;
            }

            var bayProjectors = tempProjectors.Where(p => p.CustomName.StartsWith($"{bayTag} {disposableDroneAssemblerTag} {bayNumber}")).ToList();
            var bayMergeBlocks = tempMergeBlocks.Where(m => m.CustomName.StartsWith($"{bayTag} {disposableDroneAssemblerTag} {bayNumber}")).ToList();
            var bayConnectors = tempConnectors.Where(c => c.CustomName.StartsWith($"{bayTag} {disposableDroneAssemblerTag} {bayNumber}")).ToList();

            if (bayProjectors.Count == 1)
            {
                bay.Projector = bayProjectors[0];
                tempProjectors.Remove(bay.Projector);

                foreach (var c in bayConnectors)
                {
                    var closestMergeBlock = FindClosestBlock(bayMergeBlocks, c.GetPosition());
                    if (closestMergeBlock != null)
                    {
                        Hardpoint hardpoint = new Hardpoint { Merge = closestMergeBlock, Connector = c };
                        bay.Hardpoints.Add(hardpoint);
                        bayMergeBlocks.Remove(closestMergeBlock);
                    }
                }
            }
            else
            {
                foreach (var projector in bayProjectors)
                {
                    var closestMergeBlock = FindClosestBlock(bayMergeBlocks, projector.GetPosition());
                    var closestConnector = FindClosestBlock(bayConnectors, projector.GetPosition());

                    if (closestMergeBlock != null && closestConnector != null)
                    {
                        Hardpoint hardpoint = new Hardpoint { Merge = closestMergeBlock, Connector = closestConnector, Projector = projector };
                        bay.Hardpoints.Add(hardpoint);
                        bayMergeBlocks.Remove(closestMergeBlock);
                        bayConnectors.Remove(closestConnector);
                    }

                    tempProjectors.Remove(projector);
                }
            }

            tempMergeBlocks = tempMergeBlocks.Except(bayMergeBlocks).ToList();
            tempConnectors = tempConnectors.Except(bayConnectors).ToList();
        }

        tempMergeBlocks.Remove(mergeBlock);

        loopCounter++;
        if (loopCounter >= maxIterations)
        {
            tempMergeBlocks.Clear();
            tempConnectors.Clear();
            tempProjectors.Clear();
            break;
        }

        runCounter++;
        if (runCounter >= 5)
        {
            runCounter = 0;
            yield return true;
        }
    }
    yield return true;
    loopCounter = 0;
    DisplayStatus("MAIN", "Setup Completed");
}
IEnumerator<bool> UpdateInventories()
{
    yield return true;
    DisplayStatus("MAIN", "Updating inventory caches");

    foreach (var container in containers)
    {
        logger.RReport($"Updating invcache for {container.CargoContainer.CustomName}");
        container.UpdateInventory();
        yield return true;
    }
}

IEnumerator<bool> AssembleDDs(string type)
{
    yield return true;

    foreach (var bay in assemblyBays)
    {
        if (string.IsNullOrEmpty(type) || bay.Type == type)
        {
            if (bay.Welders.All(IsValidBlock))
            {
                logger.RReport($"Assembling {type} bay {bay.Number}");
                DisplayStatus("MAIN", $"Assembling {type} bay {bay.Number}");
                yield return true;

                foreach (var welder in bay.Welders)
                {
                    welder.Enabled = true;
                }

                if (bay.Projector != null && IsValidBlock(bay.Projector))
                {
                    foreach (var hardpoint in bay.Hardpoints)
                    {
                        if (IsValidBlock(hardpoint.Merge) && IsValidBlock(hardpoint.Connector))
                        {
                            hardpoint.Merge.Enabled = true;
                            hardpoint.Connector.Enabled = true;
                        }
                    }

                    bay.Projector.Enabled = true;

                    while (!bay.Projector.IsProjecting)
                    {
                        yield return true;
                    }

                    while (bay.Projector.RemainingBlocks > 0)
                    {
                        yield return true;
                    }
                    yield return true;

                    foreach (var hardpoint in bay.Hardpoints)
                    {
                        if (!creativeMode)
                        {
                            hardpoint.Connector.Connect();
                        }
                    }
                    bay.Projector.Enabled = false;
                }
                else
                {
                    foreach (var hardpoint in bay.Hardpoints)
                    {
                        if (IsValidBlock(hardpoint.Merge) && IsValidBlock(hardpoint.Connector) && IsValidBlock(hardpoint.Projector))
                        {

                            hardpoint.Merge.Enabled = true;
                            hardpoint.Connector.Enabled = true;
                            hardpoint.Projector.Enabled = true;

                            while (!hardpoint.Projector.IsProjecting)
                            {
                                yield return true;
                            }

                            while (hardpoint.Projector.RemainingBlocks > 0)
                            {
                                yield return true;
                            }
                            yield return true;
                            if (!creativeMode)
                            {
                                hardpoint.Connector.Connect();
                            }
                            hardpoint.Projector.Enabled = false;
                        }
                    }
                }
            }
            foreach (var welder in bay.Welders)
            {
                welder.Enabled = false;
            }
            yield return true;
        }
    }

    if (IsValidBlock(APCK)) APCK.TryRun("command:refresh-su");
    for (int i = 0; i < 10; i++)
    {
        yield return true;
    }
}
IEnumerator<bool> ReloadDDs(string type)
{
    logger.RReport("Preparing supplies to load bays");
    DisplayStatus("MAIN", "Preparing supplies to load bays");
    yield return true;
    tempBlocksToResupply.Clear();
    tempHydrogenTanks.Clear();
    runCounter = 0;

    blockGroups.Clear();
    selectedBlockGroups.Clear();
    GridTerminalSystem.GetBlockGroups(blockGroups);

    if (string.IsNullOrEmpty(type))
    {
        foreach (var group in blockGroups)
        {
            if (group.Name.Contains(disposableDroneTag))
            {
                selectedBlockGroups.Add(group);
            }
        }
    }
    else
    {
        foreach (var group in blockGroups)
        {
            if (group.Name.Equals($"{type} {disposableDroneTag}"))
            {
                selectedBlockGroups.Add(group);
            }
        }
    }


    yield return true;
    runCounter = 0;
    foreach (var bay in assemblyBays)
    {
        if (string.IsNullOrEmpty(type) || bay.Type == type)
        {
            foreach (var hardpoint in bay.Hardpoints)
            {
                var merge = hardpoint.Merge;
                if (IsValidBlock(merge))
                {
                    runCounter++;
                    merge.Enabled = false;
                    if (runCounter >= 10)
                    {
                        runCounter = 0;
                        yield return true;
                    }
                }
            }
        }
    }
    yield return true;
    yield return true;
    yield return true;
    yield return true;
    yield return true;
    yield return true;
    //this is necessary to prevent race issues

    DisplayStatus("MAIN", "Scanning bays");
    foreach (var bay in assemblyBays)
    {
        if (string.IsNullOrEmpty(type) || bay.Type == type)
        {
            var group = selectedBlockGroups.FirstOrDefault(g => g.Name.Equals($"{bay.Type} {disposableDroneTag}"));
            if (group == null)
            {
                continue;
            }

            logger.RReport($"Scanning {type} bay {bay.Number}");
            blocks.Clear();
            group.GetBlocks(blocks);

            foreach (var hardpoint in bay.Hardpoints)
            {
                var connector = hardpoint.Connector;
                if (!IsValidBlock(connector))
                {
                    continue;
                }
                if (!connector.IsConnected)
                {
                    continue;
                }
                var otherConnector = connector.OtherConnector;
                if (!IsValidBlock(otherConnector))
                {
                    continue;
                }

                runCounter = 0;
                tempBlocksToResupply.Add(otherConnector);
                foreach (var block in blocks)
                {
                    runCounter++;
                    if (runCounter >= scansPerUpdate)
                    {
                        runCounter = 0;
                        yield return true;
                    }
                    if (!block.IsSameConstructAs(otherConnector))
                    {
                        continue;
                    }

                    if (block is IMyUserControllableGun)
                    {
                        tempBlocksToResupply.Add(block);
                    }
                    else if (block is IMyCargoContainer)
                    {
                        tempBlocksToResupply.Add(block);
                    }
                    else if (block is IMyReactor)
                    {
                        tempBlocksToResupply.Add(block);
                    }
                    else if (block is IMyGasTank)
                    {
                        tempHydrogenTanks.Add(block as IMyGasTank);
                    }
                }
            }
        }
    }

    logger.RReport($"Reloading {type} bays");
    DisplayStatus("MAIN", $"Reloading {type} bays");

    runCounter = 0;
    foreach (var block in tempBlocksToResupply)
    {
        Resupply(block);
        runCounter++;
        if (runCounter >= reloadsPerUpdate)
        {
            runCounter = 0;
            yield return true;
        }
    }

    logger.RReport("Refueling hydrogen tanks");
    DisplayStatus("MAIN", "Refueling hydrogen tanks");
    while (tempHydrogenTanks.Count > 0)
    {
        for (int i = tempHydrogenTanks.Count - 1; i >= 0; i--)
        {
            if (!IsValidBlock(tempHydrogenTanks[i]) || tempHydrogenTanks[i].FilledRatio > 0.95)
            {
                tempHydrogenTanks[i] = tempHydrogenTanks[tempHydrogenTanks.Count - 1];
                tempHydrogenTanks.RemoveAt(tempHydrogenTanks.Count - 1);
            }
        }
        yield return true;
    }
}
IEnumerator<bool> DeployDDs(string type)
{
    yield return true;
    foreach (var bay in assemblyBays)
    {
        if (string.IsNullOrEmpty(type) || bay.Type == type)
        {
            logger.RReport($"Deploying {type} bay {bay.Number}");
            DisplayStatus("MAIN", $"Deploying {type} bay {bay.Number}");
            if (bay.Timer == null)
            {
                foreach (var hardpoint in bay.Hardpoints)
                {
                    if (!IsValidBlock(hardpoint.Merge) || !IsValidBlock(hardpoint.Connector))
                    {
                        continue;
                    }
                }
                yield return true;

                foreach (var hardpoint in bay.Hardpoints)
                {
                    hardpoint.Connector.Disconnect();
                }
                yield return true;

                APCK.TryRun($"command:start-su:{type}-{bay.Number}");
            }
            else
            {
                bay.Timer.Trigger();
            }

            for (int i = 0; i < safetyNet; i++)
            {
                yield return true;
            }
        }
    }
}

IEnumerator<bool> AssembleNDDs(string type, int num)
{
    yield return true;
    Printer printer = printers.FirstOrDefault(p => p.Number == num);
    if (printer == null)
    {
        yield break;
    }
    yield return true;

    IMyProjector projector = printer.Projectors.FirstOrDefault(p => p.CustomName.Split(' ')[0] == type);
    if (projector == null || !IsValidBlock(projector))
    {
        yield break;
    }
    yield return true;

    if (splitQueue)
    {
        logger.SRReport($"Preparing to assemble {type} in printer {num}");
        DisplayStatus("SPLT", $"Preparing to assemble {type} in printer {num}");
    }
    else
    {
        logger.RReport($"Preparing to assemble {type} in printer {num}");
        DisplayStatus("MAIN", $"Preparing to assemble {type} in printer {num}");
    }



    float fastRate = (float)Math.Round(fastExtensionRate / printer.Pistons.Count, 4);
    float slowRate = (float)Math.Round(slowExtensionRate / printer.Pistons.Count, 4);
    float step = (float)Math.Ceiling((stepDistance / printer.Pistons.Count) * 100) / 100;

    printer.Merge.Enabled = true;
    yield return true;

    foreach (var piston in printer.Pistons)
    {
        if (!IsValidBlock(piston))
        {
            yield break;
        }

    }
    foreach (var welder in printer.Welders)
    {
        if (!IsValidBlock(welder))
        {
            yield break;
        }

    }
    yield return true;

    foreach (var piston in printer.Pistons)
    {
        piston.Velocity = fastRate;
    }

    while (printer.Pistons.Any(p => p.CurrentPosition < printer.ExtensionLimit))
    {
        yield return true;
    }

    foreach (var piston in printer.Pistons)
    {
        piston.MinLimit = piston.MaxLimit;
    }

    projector.Enabled = true;
    foreach (var welder in printer.Welders)
    {
        welder.Enabled = true;
    }

    foreach (var piston in printer.Pistons)
    {
        piston.Velocity = -slowRate;
    }
    yield return true;

    if (splitQueue)
    {
        logger.SRReport($"Assembling {type} in printer {num}");
        DisplayStatus("SPLT", $"Assembling {type} in printer {num}");
    }
    else
    {
        logger.RReport($"Assembling {type} in printer {num}");
        DisplayStatus("MAIN", $"Assembling {type} in printer {num}");
    }

    int unchangedCount = 0;
    int emptySteps = 0;
    int previousRemainingBlocks = projector.RemainingBlocks;

    while (true)
    {
        if (projector.RemainingBlocks == previousRemainingBlocks)
        {
            unchangedCount++;
        }
        else
        {
            unchangedCount = 0;
            emptySteps = 0;
        }

        if (unchangedCount >= stepTimeout)
        {
            foreach (var piston in printer.Pistons)
            {
                piston.MinLimit -= step;
                piston.Velocity = -fastRate;
            }
            unchangedCount = 0;
            emptySteps++;
        }

        if (emptySteps >= stepTimeout)
        {
            break;
        }

        previousRemainingBlocks = projector.RemainingBlocks;

        yield return true;
    }

    if (splitQueue)
    {
        logger.SRReport($"Finishing assembly of {type} in printer {num}");
        DisplayStatus("SPLT", $"Finishing assembly of {type} in printer {num}");

    }
    else
    {
        logger.RReport($"Finishing assembly of {type} in printer {num}");
        DisplayStatus("MAIN", $"Finishing assembly of {type} in printer {num}");
    }

    foreach (var piston in printer.Pistons)
    {
        piston.MinLimit = 0;
    }

    while (printer.Pistons.Any(p => p.CurrentPosition != 0))
    {
        yield return true;
    }

    foreach (var piston in printer.Pistons)
    {
        piston.Velocity = 0;
    }
    projector.Enabled = false;
    foreach (var welder in printer.Welders)
    {
        welder.Enabled = false;
    }
}
IEnumerator<bool> ReloadNDDs()
{
    yield return true;
    tempBlocksToResupply.Clear();
    splitBlockGroups.Clear();
    selectedBlockGroups.Clear();

    GridTerminalSystem.GetBlockGroups(splitBlockGroups);

    if (splitQueue)
    {
        logger.SRReport("Scanning docking ports");
        DisplayStatus("SPLT", "Scanning docking ports");
    }
    else
    {
        logger.RReport("Scanning docking ports");
        DisplayStatus("MAIN", "Scanning docking ports");
    }
    runCounter = 0;
    yield return true;

    foreach (var connector in dockingPorts)
    {
        runCounter++;
        if (runCounter >= 28)
        {
            runCounter = 0;
            yield return true;
        }

        if (!IsValidBlock(connector) || !connector.IsConnected)
        {
            continue;
        }

        var otherConnector = connector.OtherConnector;
        if (!IsValidBlock(otherConnector))
        {
            continue;
        }

        var tag = otherConnector.CustomName.Split(' ')[0];

        var group = splitBlockGroups.Find(g => g.Name.Equals($"{tag} {nonDisposableDroneTag}"));
        if (group == null)
        {
            continue;
        }

        splitBlocks.Clear();
        group.GetBlocks(splitBlocks);
        runCounter = 0;
        tempBlocksToResupply.Add(otherConnector);
        yield return true;

        foreach (var block in splitBlocks)
        {
            runCounter++;
            if (runCounter >= 40)
            {
                runCounter = 0;
                yield return true;
            }

            if (!block.IsSameConstructAs(otherConnector))
            {
                continue;
            }

            if (block is IMyUserControllableGun)
            {
                tempBlocksToResupply.Add(block);
            }
            else if (block is IMyCargoContainer)
            {
                tempBlocksToResupply.Add(block);
            }
            else if (block is IMyReactor)
            {
                tempBlocksToResupply.Add(block);
            }
        }

    }

    foreach (var block in tempBlocksToResupply)
    {
        Resupply(block);
        if (splitQueue)
        {
            logger.SRReport($"Resupplying {block.CustomName}");
            DisplayStatus("MAIN", $"Resupplying {block.CustomName}");
        }
        else
        {
            logger.RReport($"Resupplying {block.CustomName}");
            DisplayStatus("SPLT", $"Resupplying {block.CustomName}");
        }
        yield return true;
    }
}

#endregion ROUTINES


#region HANDLERS

void Auto(string type, int num)
{
    int totalHardpoints = 0;
    foreach (var bay in assemblyBays)
    {
        if (bay.Type == type)
        {
            totalHardpoints += bay.Hardpoints.Count;
        }
    }
    int cyclesNeeded = (int)Math.Ceiling((double)num / totalHardpoints);

    for (int i = 0; i < cyclesNeeded; i++)
    {
        scheduler.AddRoutine(AssembleDDs(type));
        if (!creativeMode)
        {
            scheduler.AddRoutine(ReloadDDs(type));
        }
        scheduler.AddRoutine(DeployDDs(type));
    }
}
void Command(string arg)
{
    var args = arg.Split(' ');
    var baseCommand = args[0];

    switch (baseCommand)
    {
        case "auto":
            if (args.Length > 2)
            {
                Auto(args[1], int.Parse(args[2]));
            }
            break;

        case "reload":
            if (splitQueue)
            {
                splitScheduler.AddRoutine(ReloadNDDs());
            }
            else
            {
                scheduler.AddRoutine(ReloadNDDs());
            }
            break;

        case "assemble":
            if (args.Length > 2)
            {
                if (splitQueue)
                {
                    splitScheduler.AddRoutine(AssembleNDDs(args[1], int.Parse(args[2])));
                }
                else
                {
                    scheduler.AddRoutine(AssembleNDDs(args[1], int.Parse(args[2])));
                }
            }
            break;

        case "update":
            if (splitQueue)
            {
                splitScheduler.AddRoutine(UpdateInventories());
            }
            else
            {
                scheduler.AddRoutine(UpdateInventories());
            }
            break;
    }
}
void Resupply(IMyTerminalBlock b)
{
    if (!IsValidBlock(b))
    {
        return;
    }

    double fillAmount;
    int desiredAmount;

    if (b is IMyCargoContainer || b is IMyShipConnector)
    {
        if (string.IsNullOrEmpty(b.CustomData))
        {
            return;
        }

        var customDataLines = b.CustomData.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in customDataLines)
        {
            if (line.Trim().StartsWith("tags="))
            {
                continue;
            }

            var keyValue = line.Split(new[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
            if (keyValue.Length != 2)
            {
                continue;
            }

            MyItemType itemType;
            if (!itemTypes.TryGetValue(keyValue[0].Trim(), out itemType))
            {
                continue;
            }

            var fillValue = keyValue[1].Trim();

            if (fillValue.EndsWith("%"))
            {
                if (double.TryParse(fillValue.TrimEnd('%'), out fillAmount))
                {
                    FillToPercent(b, itemType, Math.Min(fillAmount, 100));
                }
            }
            else
            {
                if (double.TryParse(fillValue, out fillAmount))
                {
                    FillToAmount(b, itemType, fillAmount);
                }
            }
        }
    }
    else if (b is IMyUserControllableGun)
    {
        var gun = (IMyUserControllableGun)b;
        MyItemType? ammoType = GetAmmoTypeForGun(gun);
        if (ammoType != null)
        {
            FillToFull(b, ammoType.Value);
        }
    }
    else if (b is IMyReactor)
    {
        var reactor = (IMyReactor)b;
        var customData = reactor.CustomData.Trim();
        var equalsIndex = customData.IndexOf('=');
        if (equalsIndex >= 0 && "uranium".Equals(customData.Substring(0, equalsIndex).Trim(), StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(customData.Substring(equalsIndex + 1), out desiredAmount))
            {
                FillToAmount(reactor, uranium, (double)desiredAmount);
                reactor.UseConveyorSystem = false;
            }
        }
    }
}

void ParseConfig()
{
    MyIniParseResult result;
    if (!ini.TryParse(Me.CustomData, out result))
        throw new Exception(result.ToString());

    splitQueue = ini.Get("General", "Split Queue").ToBoolean(splitQueue);
    creativeMode = ini.Get("General", "Creative Mode").ToBoolean(creativeMode);
    cacheInterval = ini.Get("General", "Inventory Update Interval").ToInt32(cacheInterval);

    disposableDroneTag = ini.Get("Tags", "Disposable Drone Tag").ToString(disposableDroneTag);
    nonDisposableDroneTag = ini.Get("Tags", "Non Disposable Drone Tag").ToString(nonDisposableDroneTag);
    disposableDroneAssemblerTag = ini.Get("Tags", "Disposable Drone Assembler Tag").ToString(disposableDroneAssemblerTag);
    nonDisposableDroneAssemblerTag = ini.Get("Tags", "Non Disposable Drone Assembler Tag").ToString(nonDisposableDroneAssemblerTag);

    safetyNet = ini.Get("Disposable Drones", "Safety Net").ToInt32(safetyNet);
    reloadsPerUpdate = ini.Get("Disposable Drones", "Reloads Per Update").ToInt32(reloadsPerUpdate);
    scansPerUpdate = ini.Get("Disposable Drones", "Scans Per Update").ToInt32(scansPerUpdate);

    fastExtensionRate = ini.Get("Printing", "Fast Extension Rate").ToSingle(fastExtensionRate);
    slowExtensionRate = ini.Get("Printing", "Slow Extension Rate").ToSingle(slowExtensionRate);
    stepTimeout = ini.Get("Printing", "Step Timeout").ToInt32(stepTimeout);
}
void WriteConfig()
{
    ini.AddSection("General");
    ini.AddSection("Tags");
    ini.AddSection("Disposable Drones");
    ini.AddSection("Printing");

    ini.Set("General", "Split Queue", splitQueue.ToString());
    ini.Set("General", "Creative Mode", creativeMode.ToString());
    ini.Set("General", "Inventory Update Interval", cacheInterval.ToString());

    ini.Set("Tags", "Disposable Drone Tag", disposableDroneTag);
    ini.Set("Tags", "Non Disposable Drone Tag", nonDisposableDroneTag);
    ini.Set("Tags", "Disposable Drone Assembler Tag", disposableDroneAssemblerTag);
    ini.Set("Tags", "Non Disposable Drone Assembler Tag", nonDisposableDroneAssemblerTag);

    ini.Set("Disposable Drones", "Safety Net", safetyNet.ToString());
    ini.Set("Disposable Drones", "Reloads Per Update", reloadsPerUpdate.ToString());
    ini.Set("Disposable Drones", "Scans Per Update", scansPerUpdate.ToString());

    ini.Set("Printing", "Fast Extension Rate", fastExtensionRate.ToString());
    ini.Set("Printing", "Slow Extension Rate", slowExtensionRate.ToString());
    ini.Set("Printing", "Step Timeout", stepTimeout.ToString());

    Me.CustomData = ini.ToString();

}

void WriteStatus()
{
    double lastRunTime = Runtime.LastRunTimeMs;
    runtimes.Enqueue(lastRunTime);

    if (runtimes.Count > avgRuntimes)
    {
        runtimes.Dequeue();
    }

    double averageRuntime = runtimes.Sum() / runtimes.Count;

    string status = logger.BuildStatus(scheduler.IsActive());
    status += $"\n\nAverage runtime: {averageRuntime:F3}ms";

    Echo(status);
}

void DisplayStatus(string tag, string report)
{
    if (statusDisplay != null)
    {
        string status = $"{tag} - {report}";
        statusDisplay.Log(status, scheduler.IsActive());
    }
}

#endregion HANDLERS


#region UTILITIES

bool IsValidBlock(IMyTerminalBlock b)
{
    return b != null && !b.Closed && b.IsFunctional;
}

void FillToPercent(IMyTerminalBlock b, MyItemType itemType, double fillPercentage)
{
    if (!b.HasInventory)
    {
        return;
    }

    var inventory = b.GetInventory();
    var maxVolume = (double)inventory.MaxVolume;
    var targetVolume = maxVolume * fillPercentage / 100;
    var volumePerItem = itemType.GetItemInfo().Volume;
    var currentVolume = (double)inventory.GetItemAmount(itemType) * volumePerItem;

    if (currentVolume >= targetVolume)
    {
        return;
    }

    var requiredVolume = targetVolume - currentVolume;
    Container container = null;
    foreach (var c in containers)
    {
        var containerVolume = c.Items[itemType] * volumePerItem;
        if (containerVolume >= requiredVolume)
        {
            container = c;
            break;
        }
    }

    if (container == null || container.CargoContainer == null)
    {
        return;
    }

    var transferVolume = Math.Min(container.Items[itemType] * volumePerItem, requiredVolume);
    var transferAmount = Math.Floor(transferVolume / volumePerItem);
    var item = container.CargoContainer.GetInventory().FindItem(itemType).Value;

    inventory.TransferItemFrom(container.CargoContainer.GetInventory(), item, (MyFixedPoint)transferAmount);
    container.RemoveItem(itemType, transferAmount);
}
void FillToAmount(IMyTerminalBlock b, MyItemType itemType, double fillAmount)
{
    if (!b.HasInventory)
    {
        return;
    }

    var inventory = b.GetInventory();
    var currentAmount = (double)inventory.GetItemAmount(itemType);

    if (currentAmount >= fillAmount)
    {
        return;
    }

    var requiredAmount = fillAmount - currentAmount;
    Container container = null;
    foreach (var c in containers)
    {
        if (c.Items[itemType] >= requiredAmount)
        {
            container = c;
            break;
        }
    }

    if (container == null || container.CargoContainer == null)
    {
        return;
    }

    var transferAmount = Math.Min(container.Items[itemType], requiredAmount);
    var item = container.CargoContainer.GetInventory().FindItem(itemType).Value;

    inventory.TransferItemFrom(container.CargoContainer.GetInventory(), item, (MyFixedPoint)transferAmount);
    container.RemoveItem(itemType, transferAmount);
}
void FillToFull(IMyTerminalBlock b, MyItemType itemType)
{
    if (!b.HasInventory)
    {
        return;
    }

    var inventory = b.GetInventory();
    var maxVolume = (double)inventory.MaxVolume;
    var volumePerItem = itemType.GetItemInfo().Volume;
    var currentVolume = (double)inventory.GetItemAmount(itemType) * volumePerItem;

    if (currentVolume >= maxVolume)
    {
        return;
    }

    var requiredVolume = maxVolume - currentVolume;
    Container container = null;
    foreach (var c in containers)
    {
        var containerVolume = c.Items[itemType] * volumePerItem;
        if (containerVolume >= requiredVolume)
        {
            container = c;
            break;
        }
    }

    if (container == null || container.CargoContainer == null)
    {
        return;
    }

    var transferVolume = Math.Min(container.Items[itemType] * volumePerItem, requiredVolume);
    var transferAmount = transferVolume / volumePerItem;
    var item = container.CargoContainer.GetInventory().FindItem(itemType).Value;

    inventory.TransferItemFrom(container.CargoContainer.GetInventory(), item, (MyFixedPoint)transferAmount);
    container.RemoveItem(itemType, transferAmount);
}

MyItemType? GetAmmoTypeForGun(IMyUserControllableGun gun)
{
    var gbd = gun.BlockDefinition;
    if (gbd == smallGatlingGun || gbd == smallGatlingGunWarfare2 || gbd == smallGatlingTurret || gbd == largeGatlingTurret)
    {
        return gatlingAmmo;
    }
    else if (gbd == smallBlockAutocannon || gbd == autoCannonTurret)
    {
        return autocannonAmmo;
    }
    else if (gbd == smallBlockMediumCalibreGun || gbd == smallMediumCalibreTurret || gbd == largeBlockMediumCalibreTurret)
    {
        return assaultAmmo;
    }
    else if (gbd == smallRailgun)
    {
        return smallRailgunAmmo;
    }
    else if (gbd == largeRailgun)
    {
        return largeRailgunAmmo;
    }
    else if (gbd == largeBlockLargeCalibreGun || gbd == largeCalibreTurret)
    {
        return artilleryAmmo;
    }
    else if (gbd == smallRocketLauncherReload || gbd == largeMissileLauncher || gbd == smallMissileTurret || gbd == largeMissileTurret)
    {
        return missileAmmo;
    }
    else
    {
        return null;
    }

}
T FindClosestBlock<T>(List<T> blocks, Vector3D position) where T : IMyTerminalBlock
{
    T closestBlock = default(T);
    double closestDistance = double.MaxValue;

    foreach (var block in blocks)
    {
        double distance = Vector3D.Distance(position, block.GetPosition());
        if (distance < closestDistance)
        {
            closestDistance = distance;
            closestBlock = block;
        }
    }

    return closestBlock;
}

#endregion UTILITIES


#region CLASSES

class Scheduler
{
    private Queue<IEnumerator<bool>> routines = new Queue<IEnumerator<bool>>();
    private IEnumerator<bool> routine = null;
    public void AddRoutine(IEnumerator<bool> routine)
    {
        routines.Enqueue(routine);
    }
    public void ExecuteRoutine()
    {
        if (routine != null)
        {
            bool hasMoreSteps = routine.MoveNext();
            if (!hasMoreSteps)
            {
                routine.Dispose();
                routine = null;
            }
        }
        if (routine == null && routines.Count > 0)
        {
            routine = routines.Dequeue();
            ExecuteRoutine();
        }
    }
    public bool IsEmpty()
    {
        return routines.Count == 0;
    }
    public bool IsActive()
    {
        return routine != null;
    }
}

class AssemblyBay
{
    public List<Hardpoint> Hardpoints { get; private set; }
    public List<IMyShipWelder> Welders { get; private set; }
    public IMyProjector Projector { get; set; }
    public IMyTimerBlock Timer { get; set; }
    public string Type { get; set; }
    public int Number { get; set; }

    public AssemblyBay(string type, int number)
    {
        Hardpoints = new List<Hardpoint>();
        Welders = new List<IMyShipWelder>();
        Type = type;
        Number = number;
    }
}
class Hardpoint
{
    public IMyShipMergeBlock Merge { get; set; }
    public IMyShipConnector Connector { get; set; }
    public IMyProjector Projector { get; set; }
}

class Printer
{
    public List<IMyShipWelder> Welders { get; private set; }
    public List<IMyProjector> Projectors { get; private set; }
    public List<IMyPistonBase> Pistons { get; private set; }
    public IMyShipMergeBlock Merge { get; set; }
    public float ExtensionLimit { get; set; }
    public int Number { get; set; }

    public Printer(int number)
    {
        Welders = new List<IMyShipWelder>();
        Projectors = new List<IMyProjector>();
        Pistons = new List<IMyPistonBase>();
        Number = number;
        ExtensionLimit = 0.0f;
    }
}
class Container
{
    public IMyCargoContainer CargoContainer { get; set; }
    public Dictionary<MyItemType, double> Items { get; set; }
    public Container(IMyCargoContainer cargoContainer, Dictionary<string, MyItemType> itemTypes)
    {
        CargoContainer = cargoContainer;
        Items = new Dictionary<MyItemType, double>(10);
        foreach (var itemType in itemTypes.Values)
        {
            Items[itemType] = 0;
            UpdateInventory();
        }
    }
    public void UpdateInventory()
    {
        if (CargoContainer == null || CargoContainer.Closed || !CargoContainer.IsFunctional)
        {
            return;
        }

        var inventory = CargoContainer.GetInventory();
        foreach (var item in Items.Keys.ToList())
        {
            Items[item] = (double)inventory.GetItemAmount(item);
        }
    }
    public void RemoveItem(MyItemType itemType, double amount)
    {
        if (Items.ContainsKey(itemType))
        {
            Items[itemType] -= amount;
            if (Items[itemType] < 0)
            {
                Items[itemType] = 0;
            }
        }
    }
}
class StatusLogger
{
    private bool split;
    private string routineReport;
    private string splitRoutineReport;

    public StatusLogger(bool split)
    {
        this.split = split;
    }

    public void RReport(string report)
    {
        routineReport = report;
    }

    public void SRReport(string report)
    {
        splitRoutineReport = report;
    }

    public string getRReport()
    {
        return routineReport;
    }
    public string getSRReport()
    {
        return splitRoutineReport;
    }

    public string BuildStatus(bool isRunning)
    {
        string status;
        if (!isRunning)
        {
            RReport("");
            SRReport("");
            status = "ECS Utilities v" + version + "\n|- WAITING -|\n\n";
        }
        else
        {
            status = "ECS Utilities v" + version + "\n|- RUNNING -|\n" + routineReport + "\n" + splitRoutineReport;
        }

        return status;
    }
}

class StatusDisplay
{
    private IMyTextSurface surface;
    private List<string> logEntries = new List<string>();
    private int logSlots;
    private int actionCount = 0;

    public StatusDisplay(IMyTextSurface surface)
    {
        this.surface = surface;
        this.surface.ContentType = ContentType.SCRIPT;
        this.surface.Script = "";
        this.surface.BackgroundColor = Color.Black;
        this.surface.ScriptBackgroundColor = Color.Black;

        logSlots = (int)(surface.SurfaceSize.Y - 160) / 20;
    }

    public void Log(string entry, bool isRunning)
    {

        if (isRunning)
        {
            logEntries.Add(entry);
            actionCount++;

            if (logEntries.Count > logSlots)
            {
                logEntries.RemoveAt(0);
            }
        }

        var frame = surface.DrawFrame();

        frame.Add(new MySprite()
        {
            Type = SpriteType.TEXT,
            Data = "ECS Utilities v" + version,
            Position = new Vector2(surface.SurfaceSize.X / 2, 10),
            Alignment = TextAlignment.CENTER,
            FontId = "White",
            Color = Color.LightBlue,
            RotationOrScale = 1.5f
        });

        frame.Add(new MySprite()
        {
            Type = SpriteType.TEXT,
            Data = isRunning ? "RUNNING" : "WAITING",
            Position = new Vector2(surface.SurfaceSize.X / 2, 50),
            Alignment = TextAlignment.CENTER,
            FontId = "White",
            Color = isRunning ? Color.Green : Color.Orange,
            RotationOrScale = 1.5f
        });

        frame.Add(new MySprite()
        {
            Type = SpriteType.TEXT,
            Data = "Action Log",
            Position = new Vector2(5, 88),
            Alignment = TextAlignment.LEFT,
            FontId = "White",
            Color = Color.Yellow,
            RotationOrScale = 1f
        });

        frame.Add(new MySprite()
        {
            Type = SpriteType.TEXTURE,
            Data = "SquareSimple",
            Position = new Vector2(3, 120),
            Size = new Vector2(surface.SurfaceSize.X - 6, 2f),
            RotationOrScale = 0f,
            Color = Color.White
        });

        frame.Add(new MySprite()
        {
            Type = SpriteType.TEXTURE,
            Data = "SquareSimple",
            Position = new Vector2(3, surface.SurfaceSize.Y - 30),
            Size = new Vector2(surface.SurfaceSize.X - 6, 2f),
            RotationOrScale = 0f,
            Color = Color.White
        });

        for (int i = 0; i < logEntries.Count; i++)
        {
            frame.Add(new MySprite()
            {
                Type = SpriteType.TEXT,
                Data = logEntries[i],
                Position = new Vector2(5, 125 + i * 20),
                Alignment = TextAlignment.LEFT,
                FontId = "White",
                Color = Color.White,
                RotationOrScale = 0.7f
            });
        }

        frame.Add(new MySprite()
        {
            Type = SpriteType.TEXT,
            Data = $"Action Count: {actionCount}",
            Position = new Vector2(5, surface.SurfaceSize.Y - 30),
            Alignment = TextAlignment.LEFT,
            FontId = "White",
            Color = Color.Yellow,
            RotationOrScale = 0.85f
        });

        frame.Dispose();
    }
}

#endregion CLASSES