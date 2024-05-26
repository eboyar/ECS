//Core for EMS; handles commands, launching, loading, reloading, and status display
//by eboyar
const string version = "1.2.0";

List<MType> mTypes = new List<MType>();
Dictionary<MType, List<Bay>> MBays = new Dictionary<MType, List<Bay>>();
SortedSet<GuidanceCommand> guidanceCommands = new SortedSet<GuidanceCommand>(new GuidanceCommand.Comparer());
Queue<Bay> toLaunch = new Queue<Bay>();
Queue<Bay> toClose = new Queue<Bay>();
Queue<GuidanceCommand> toGuidance = new Queue<GuidanceCommand>();

MyCommandLine cL = new MyCommandLine();
MyIni ini = new MyIni();
Scheduler scheduler = new Scheduler();
StatusLogger logger;
StatusDisplay statusDisplay;

IMyUnicastListener blLaunch;
IMyBroadcastListener blRequest;

IMyProgrammableBlock APCK;
IMyProgrammableBlock STATUS;

Dictionary<IMyCargoContainer, int> containers = new Dictionary<IMyCargoContainer, int>();

bool displayLock = false;

string mGroupTag = "Group";

int runCounter = 0;
int loopCounter = 0;
const int maxIterations = 1000;

int reloadsPerUpdate = 2;
int scansPerUpdate = 80;
int hatchDelayTime = 12;

List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>(2000);
List<IMyShipMergeBlock> tempMergeBlocks = new List<IMyShipMergeBlock>(100);
List<IMyShipConnector> tempConnectors = new List<IMyShipConnector>(100);
List<IMyProjector> tempProjectors = new List<IMyProjector>(100);

Dictionary<MType, List<IMyTerminalBlock>> tempMCaches = new Dictionary<MType, List<IMyTerminalBlock>>();
List<double> distances = new List<double>(100);
List<IMyTerminalBlock> closestBlocks = new List<IMyTerminalBlock>(100);

int cacheClock = 0;
int cacheInterval = 1800;
Queue<double> runtimes = new Queue<double>();
const int avgRuntimes = 33;

static readonly MyItemType scatterAmmo = new MyItemType("MyObjectBuilder_AmmoMagazine", "SemiAutoPistolMagazine");

Program()
{
    if (!string.IsNullOrEmpty(Me.CustomData)) ParseConfig();
    else WriteConfig();

    logger = new StatusLogger(true);
    blRequest = IGC.RegisterBroadcastListener("Launch Request");
    blLaunch = IGC.UnicastListener;
    blRequest.SetMessageCallback("LRequest");
    blLaunch.SetMessageCallback("LLaunch");

    Runtime.UpdateFrequency |= UpdateFrequency.Update10;
    scheduler.AddRoutine(Setup());
    scheduler.AddRoutine(Cache());
}

void Main(string argument, UpdateType updateSource)
{
    if (cL.TryParse(argument))
        Command(argument);

    if ((updateSource & UpdateType.Update10) != 0)
    {
        scheduler.ExecuteRoutine();
        PassGuidance();

        cacheClock++;
        if (cacheClock >= cacheInterval)
        {
            cacheClock = 0;
            scheduler.AddRoutine(UpdateContainers());
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


IEnumerator<bool> Setup()
{
    logger.Report("Running setup");
    foreach (var mType in mTypes)
    {
        MBays[mType] = new List<Bay>();
        tempMCaches[mType] = new List<IMyTerminalBlock>(2000);
    }
    yield return true;

    blocks.Clear();
    GridTerminalSystem.GetBlocksOfType(blocks, null);
    yield return true;

    runCounter = 0;

    logger.Report("Getting bays");
    foreach (var b in blocks)
    {

        if (!b.IsSameConstructAs(Me))
        {
            runCounter++;
            continue;
        }

        string[] tag = b.CustomName.Split(' ');

        int t;
        if (tag.Length >= 3 && mTypes.Any(mType => mType.Name == tag[0]) && int.TryParse(tag[2], out t))
        {
            string MTypeName = tag[0];
            MType m = mTypes.Find(mType => mType.Name == MTypeName);
            if (m == null || !MBays.ContainsKey(m))
            {
                continue;
            }
            runCounter += 4;

            if (b is IMyShipMergeBlock)
            {
                tempMergeBlocks.Add(b as IMyShipMergeBlock);
                runCounter++;
            }
            else if (b is IMyShipConnector)
            {
                tempConnectors.Add(b as IMyShipConnector);
                runCounter++;
            }
            else if (b is IMyProjector)
            {
                tempProjectors.Add(b as IMyProjector);
                runCounter++;
            }
            else if (b is IMyDoor || b is IMyMotorAdvancedStator || b is IMyShipWelder)
            {
                bool bayType = tag[1] == "Salvo";
                int bayNumber = t;

                Bay bay = MBays[m].FirstOrDefault(by => by.Number == bayNumber);

                if (bay == null)
                {
                    bay = new Bay(m, bayNumber, bayType);
                    MBays[m].Add(bay);
                }
                runCounter++;

                if (b is IMyDoor)
                {
                    bay.Hatch.Doors.Add(b as IMyDoor);
                    runCounter++;
                }
                else if (b is IMyMotorAdvancedStator)
                {
                    bay.Hatch.Hinges.Add(b as IMyMotorAdvancedStator);
                    runCounter++;
                }
                else if (b is IMyShipWelder)
                {
                    bay.Welders.Add(b as IMyShipWelder);
                    runCounter++;
                }
            }
        }
        else
        {
            if (b is IMyCargoContainer && b.CustomName.Contains("resupply"))
            {
                IMyCargoContainer container = b as IMyCargoContainer;
                int ammoCount = GetAmmoCount(container);
                containers[container] = ammoCount;
                runCounter++;
            }
            else if (b is IMyProgrammableBlock)
            {
                if (b.CustomName.Contains("a-core"))
                {
                    APCK = b as IMyProgrammableBlock;
                    runCounter++;
                }
                else if (b.CustomName.Contains("ems-status"))
                {
                    STATUS = b as IMyProgrammableBlock;
                    runCounter++;
                }
            }
            else if (b is IMyTextPanel && b.CustomName.Contains("ems-core"))
            {
                var disp = b as IMyTextSurfaceProvider;
                var surf = disp.GetSurface(0);
                statusDisplay = new StatusDisplay(surf);
                DisplayStatus("LOCAL", "Setup Initiated");
            }
        }
        if (runCounter >= 28)
        {
            runCounter = 0;
            yield return true;
        }

    }

    logger.Report("Setting up bays");
    while (tempMergeBlocks.Count > 0 && tempConnectors.Count > 0 && tempProjectors.Count > 0)
    {
        var mergeBlock = tempMergeBlocks[0];
        var mergePosition = mergeBlock.GetPosition();
        var connector = FindClosestBlock(tempConnectors, mergePosition);

        string[] parts = mergeBlock.CustomName.Split(' ');

        if (parts.Length >= 3)
        {
            string MTypeName = parts[0];
            MType m = mTypes.Find(mType => mType.Name == MTypeName);
            string bayType = parts[1];
            int bayNumber = int.Parse(parts[2]);

            if (!MBays.ContainsKey(m))
            {
                MBays[m] = new List<Bay>();
            }

            Bay bay = MBays[m].FirstOrDefault(b => b.Number == bayNumber);

            if (bay == null)
            {
                bay = new Bay(m, bayNumber, bayType == "Salvo");
                MBays[m].Add(bay);
            }
            runCounter++;

            var bayProjectors = tempProjectors.Where(proj => proj.CustomName.StartsWith($"{MTypeName} {bayType} {bayNumber}")).ToList();
            var bayMergeBlocks = tempMergeBlocks.Where(merge => merge.CustomName.StartsWith($"{MTypeName} {bayType} {bayNumber}")).ToList();
            var bayConnectors = tempConnectors.Where(conn => conn.CustomName.StartsWith($"{MTypeName} {bayType} {bayNumber}")).ToList();
            runCounter++;

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
        if (runCounter >= 4)
        {
            runCounter = 0;
            yield return true;
        }
    }
    DisplayStatus("LOCAL", "Setup Completed");
}
IEnumerator<bool> UpdateContainers()
{
    DisplayStatus("LOCAL", "Updating inventory caches");
    foreach (var container in containers.Keys.ToList())
    {
        if (container == null || container.Closed)
        {
            containers.Remove(container);
        }
        else
        {
            containers[container] = GetAmmoCount(container);
        }
        yield return true;
    }
}

IEnumerator<bool> Assemble(string type = null)
{
    runCounter = 0;
    foreach (var mType in mTypes)
    {
        if (type != null && mType.Name != type)
        {
            continue;
        }
        foreach (var bayList in MBays)
        {
            if (bayList.Key.Name != mType.Name)
            {
                continue;
            }
            foreach (var bay in bayList.Value)
            {
                if (bay.isAvailable)
                {
                    continue;
                }

                if (!bay.Welders.All(IsValidBlock))
                {
                    continue;
                }
                yield return true;

                logger.Report($"Assembling {mType.Name} bay {bay.Number}");
                DisplayStatus("LOCAL", $"Assembling {mType.Name} bay {bay.Number}");

                foreach (var welder in bay.Welders)
                {
                    if (runCounter >= 10)
                    {
                        runCounter = 0;
                        yield return true;
                    }
                    welder.Enabled = true;
                    runCounter++;
                }
                yield return true;

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
                    yield return true;

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

                    foreach (Hardpoint hardpoint in bay.Hardpoints)
                    {
                        hardpoint.Connector.Connect();
                    }
                    bay.Projector.Enabled = false;
                    yield return true;
                }
                else
                {
                    foreach (Hardpoint hardpoint in bay.Hardpoints)
                    {
                        if (IsValidBlock(hardpoint.Merge) && IsValidBlock(hardpoint.Connector) && IsValidBlock(hardpoint.Projector))
                        {
                            yield return true;

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

                            hardpoint.Connector.Connect();
                            hardpoint.Projector.Enabled = false;
                            yield return true;
                        }
                    }
                    yield return true;
                }

                foreach (var welder in bay.Welders)
                {
                    welder.Enabled = false;
                }
                yield return true;
            }
        }
    }
    if (IsValidBlock(APCK)) APCK.TryRun("command:refresh-su");
    if (IsValidBlock(STATUS)) STATUS.TryRun("reload");
    yield return true;
}
IEnumerator<bool> Cache(string type = null)
{
    logger.Report("Preparing missile bays");
    DisplayStatus("LOCAL", "Preparing missile bays");
    runCounter = 0;

    foreach (var mType in mTypes)
    {
        if (type != null && mType.Name != type)
        {
            continue;
        }

        var group = GridTerminalSystem.GetBlockGroupWithName($"{mType.Name} Group");
        if (group == null)
        {
            continue;
        }

        tempMCaches[mType].Clear();
        group.GetBlocks(tempMCaches[mType]);
        yield return true;
    }

    foreach (var mType in mTypes)
    {
        if (type != null && mType.Name != type)
        {
            continue;
        }

        if (!tempMCaches.ContainsKey(mType))
        {
            continue;
        }

        foreach (var bay in MBays[mType])
        {

            bool isBayFull = true;
            bool hasEnoughScatterAmmo = false;

            if (mType.ScatterAmmo == 0)
            {
                hasEnoughScatterAmmo = true;
            }

            foreach (var hardpoint in bay.Hardpoints)
            {
                var connector = hardpoint.Connector.OtherConnector;
                if (connector == null)
                {
                    isBayFull = false;
                    continue;
                }

                hardpoint.Merge.Enabled = false;
                hardpoint.Missile.Thrusters.Clear();
                hardpoint.Missile.scatterCons.Clear();
                hardpoint.Missile.launchTimer = null;

                foreach (var block in tempMCaches[mType])
                {
                    runCounter++;
                    if (runCounter >= scansPerUpdate)
                    {
                        runCounter = 0;
                        yield return true;
                    }
                    if (!block.IsSameConstructAs(connector))
                    {
                        continue;
                    }
                    if (block is IMyThrust)
                    {
                        hardpoint.Missile.Thrusters.Enqueue(block as IMyThrust);
                    }
                    else if (block is IMyTimerBlock && block.CustomName.IndexOf("stockpile", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        hardpoint.Missile.launchTimer = block as IMyTimerBlock;
                    }
                    else if (block.HasInventory && block != connector && block.CustomName.IndexOf("scatter", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (block is IMyCargoContainer)
                        {
                            var scn = block as IMyCargoContainer;
                            var scnInv = scn.GetInventory();
                            if (!hasEnoughScatterAmmo && scn.GetInventory().ItemCount > 0)
                            {
                                hasEnoughScatterAmmo = true;
                            }
                            hardpoint.Missile.scatterCons.Enqueue(scnInv);

                        }
                        else if (block is IMyShipConnector)
                        {
                            var scn = block as IMyShipConnector;
                            var scnInv = scn.GetInventory();
                            if (!hasEnoughScatterAmmo && scn.GetInventory().ItemCount > 0)
                            {
                                hasEnoughScatterAmmo = true;
                            }
                            hardpoint.Missile.scatterCons.Enqueue(scnInv);
                        }
                    }
                }
                //hardpoint.Merge.Enabled = true;
            }

            bay.isAvailable = isBayFull && hasEnoughScatterAmmo;
        }
    }
}
IEnumerator<bool> Load(string type = null)
{
    runCounter = 0;
    foreach (var mType in mTypes)
    {
        if (mType.ScatterAmmo == 0)
        {
            continue;
        }

        if (type != null && mType.Name != type)
        {
            continue;
        }

        var amountToLoad = mType.ScatterAmmo;

        foreach (var bayList in MBays)
        {
            if (bayList.Key.Name != mType.Name)
            {
                continue;
            }
            foreach (var bay in bayList.Value)
            {
                logger.Report($"Reloading {mType.Name} bay {bay.Number}");
                DisplayStatus("LOCAL", $"Reloading {mType.Name} bay {bay.Number}");
                yield return true;

                foreach (var hardpoint in bay.Hardpoints)
                {
                    if (hardpoint.Missile == null)
                    {
                        continue;
                    }

                    var scatterCons = hardpoint.Missile.scatterCons;

                    foreach (var con in scatterCons)
                    {
                        IMyCargoContainer provider = null;
                        foreach (var container in containers)
                        {
                            if (container.Value >= amountToLoad)
                            {
                                provider = container.Key;
                                break;
                            }
                        }

                        if (provider == null)
                        {
                            continue;
                        }

                        var providerInv = provider.GetInventory();
                        if (providerInv == null)
                        {
                            continue;
                        }

                        var ammo = providerInv.FindItem(scatterAmmo).Value;

                        if (con == null)
                        {
                            continue;
                        }

                        con.TransferItemFrom(providerInv, ammo, (MyFixedPoint)amountToLoad);
                        containers[provider] -= amountToLoad;
                        runCounter++;

                        if (runCounter >= reloadsPerUpdate)
                        {
                            runCounter = 0;
                            yield return true;
                        }
                    }
                }
                bay.isAvailable = true;
                yield return true;
            }
        }
    }
}

IEnumerator<bool> Launch(string type, int num)
{
    logger.Report($"Preparing to launch {num} {type} missiles");
    DisplayStatus("LOCAL", $"Preparing to launch {num} {type} missiles");
    runCounter = 0;
    MType mType = null;
    bool isSalvo = false;
    foreach (var m in mTypes)
    {
        if (m.Name == type)
        {
            mType = m;
            break;
        }
    }
    if (mType == null)
    {
        yield break;
    }

    int baysAdded = 0;
    foreach (var bayList in MBays)
    {
        if (bayList.Key.Name != type)
        {
            continue;
        }
        foreach (var bay in bayList.Value)
        {
            if (!bay.isAvailable)
            {
                continue;
            }
            bool isValidBay = true;
            if (bay.Hatch.Doors != null && bay.Hatch.Doors.Count > 0)
            {
                foreach (var door in bay.Hatch.Doors)
                {
                    if (!IsValidBlock(door))
                    {
                        isValidBay = false;
                        break;
                    }
                }
            }
            if (isValidBay && bay.Hatch.Hinges != null && bay.Hatch.Hinges.Count > 0)
            {
                foreach (var hinge in bay.Hatch.Hinges)
                {
                    if (!IsValidBlock(hinge))
                    {
                        isValidBay = false;
                        break;
                    }
                }
            }
            if (!isValidBay)
            {
                continue;
            }

            toLaunch.Enqueue(bay);
            bay.isAvailable = false;
            baysAdded++;
            if (baysAdded >= num)
            {
                break;
            }
        }
    }
    yield return true;

    logger.Report($"Opening hatches for {num} {type} bays");
    DisplayStatus("LOCAL", $"Opening hatches for {num} {type} bays");
    foreach (var bay in toLaunch)
    {
        if (runCounter >= 10)
        {
            yield return true;
            runCounter = 0;
        }

        if (bay.IsSalvo)
        {
            isSalvo = true;
        }

        if (bay.Hatch.Doors != null)
        {
            foreach (var door in bay.Hatch.Doors)
            {
                door.OpenDoor();
                runCounter++;
                if (runCounter >= 10)
                {
                    yield return true;
                    runCounter = 0;
                }
            }
        }
        if (bay.Hatch.Hinges != null)
        {
            foreach (var hinge in bay.Hatch.Hinges)
            {
                hinge.TargetVelocityRPM = -hinge.TargetVelocityRPM;
                runCounter++;
                if (runCounter >= 10)
                {
                    yield return true;
                    runCounter = 0;
                }
            }
        }
    }

    for (int i = 0; i < hatchDelayTime; i++)
    {
        yield return true;
    }

    if (STATUS != null)
    {
        if (isSalvo)
        {
            STATUS.TryRun($"launch {type} salvo {num}");
        }
        else
        {
            STATUS.TryRun($"launch {type} silo {num}");
        }
    }

    while (toLaunch.Count > 0)
    {
        Bay bay = toLaunch.Dequeue();
        toClose.Enqueue(bay);

        logger.Report($"Launching {type} missile from bay {bay.Number}");
        DisplayStatus("LOCAL", $"Launching {type} missile from bay {bay.Number}");
        foreach (var hardpoint in bay.Hardpoints)
        {
            LaunchMissile(hardpoint);
            yield return true;
        }
        bay.isReserved = false;
        guidanceCommands.Add(new GuidanceCommand { Type = type, Number = bay.Number, DelayCounter = mType.Delay });
    }

    for (int i = 0; i < hatchDelayTime + 4; i++)
    {
        yield return true;
    }

    runCounter = 0;

    foreach (var bay in toClose)
    {
        if (runCounter >= 10)
        {
            yield return true;
            runCounter = 0;
        }

        if (bay.Hatch.Doors != null)
        {
            foreach (var door in bay.Hatch.Doors)
            {
                door.CloseDoor();
                runCounter++;
                if (runCounter >= 10)
                {
                    yield return true;
                    runCounter = 0;
                }
            }
        }
        if (bay.Hatch.Hinges != null)
        {
            foreach (var hinge in bay.Hatch.Hinges)
            {
                hinge.TargetVelocityRPM = -hinge.TargetVelocityRPM;
                runCounter++;
                if (runCounter >= 10)
                {
                    yield return true;
                    runCounter = 0;
                }
            }
        }
    }
    toClose.Clear();
}

void Reload(string type = null)
{
    bool typeExists = false;
    if (type == null)
    {
        typeExists = true;
    }
    else
    {
        foreach (MType mType in mTypes)
        {
            if (mType.Name == type)
            {
                typeExists = true;
                break;
            }
        }
    }
    if (typeExists)
    {
        scheduler.AddRoutine(Assemble(type));
        scheduler.AddRoutine(Cache(type));
        scheduler.AddRoutine(Load(type));
    }
}
void Command(string arg)
{
    var args = arg.Split(' ');
    var baseCommand = args[0].ToLower();

    switch (baseCommand)
    {
        case "reload":
            if (args.Length == 1)
            {
                Reload();
            }
            else if (args.Length == 2)
            {
                Reload(args[1]);
            }
            break;

        case "launch":
            if (args.Length == 3)
            {
                var type = args[1];
                var num = int.Parse(args[2]);
                scheduler.AddRoutine(Launch(type, num));
            }
            break;

        case "lrequest":
            RequestCommand();
            break;

        case "llaunch":
            LaunchCommand();
            break;

        case "update":
            scheduler.AddRoutine(UpdateContainers());
            break;
    }
}

void LaunchMissile(Hardpoint hardpoint)
{
    if (hardpoint == null || hardpoint.Missile.isLaunched)
    {
        return;
    }

    var merge = hardpoint.Merge;
    var conn = hardpoint.Connector;
    var thrusters = hardpoint.Missile.Thrusters;
    var timer = hardpoint.Missile.launchTimer;

    if (!IsValidBlock(merge) || !IsValidBlock(conn) || !IsValidBlock(timer))
    {
        return;
    }

    foreach (var thruster in thrusters)
    {
        if (!IsValidBlock(thruster))
        {
            return;
        }
    }

    merge.Enabled = false;
    timer.Trigger();
    foreach (var thruster in thrusters)
    {
        thruster.ThrustOverride = thruster.MaxThrust;
    }
    conn.Disconnect();
}
void PassGuidance()
{
    foreach (GuidanceCommand command in guidanceCommands)
    {
        command.DelayCounter--;

        if (command.DelayCounter <= 0)
        {
            toGuidance.Enqueue(command);
        }
    }

    while (toGuidance.Count > 0)
    {
        GuidanceCommand command = toGuidance.Dequeue();
        guidanceCommands.Remove(command);
        if (IsValidBlock(APCK))
        {
            APCK.TryRun($"command:start-su:{command.Type}-{command.Number}");
        }
    }
}

void LaunchCommand()
{
    while (blLaunch.HasPendingMessage)
    {
        var message = blLaunch.AcceptMessage();
        if (message.Data is string && message.Tag == "Launch Command")
        {
            var command = message.Data.ToString().Split(' ');
            if (command.Length == 3 && String.Equals(command[0], "launch", StringComparison.OrdinalIgnoreCase))
            {
                var type = command[1];
                var num = int.Parse(command[2]);
                scheduler.AddRoutine(Launch(type, num));
                DisplayStatus("REMOTE", $"Got request for {num} {type} missile(s)");


                foreach (var bayList in MBays)
                {
                    if (bayList.Key.Name != type)
                    {
                        continue;
                    }
                    foreach (var bay in bayList.Value)
                    {
                        if (!bay.isReserved)
                        {
                            bay.isReserved = true;
                            num--;
                        }
                        if (num == 0)
                        {
                            break;
                        }
                    }
                }
            }
        }
    }
}
void RequestCommand()
{
    while (blRequest.HasPendingMessage)
    {
        var message = blRequest.AcceptMessage();
        if (message.Data is string)
        {
            var request = message.Data.ToString().Split('|');
            if (request.Length == 3)
            {
                var requestID = long.Parse(request[0]);
                var requestCommand = request[1].Split(' ');
                var requestPosition = request[2];

                if (requestCommand.Length == 3 && String.Equals(requestCommand[0], "launch", StringComparison.OrdinalIgnoreCase))
                {
                    var type = mTypes.FirstOrDefault(mType => mType.Name == requestCommand[1]);
                    var num = int.Parse(requestCommand[2]);

                    if (type != null)
                    {
                        int baysAvailable = 0;
                        foreach (var bayList in MBays)
                        {
                            if (bayList.Key.Name != type.Name)
                            {
                                continue;
                            }
                            foreach (var bay in bayList.Value)
                            {
                                if (bay.isAvailable && !bay.isReserved)
                                {
                                    baysAvailable++;
                                }
                            }
                        }
                        if (baysAvailable >= num)
                        {
                            var senderPosition = new Vector3D();
                            Vector3D.TryParse(requestPosition, out senderPosition);

                            var recieverPosition = Me.GetPosition();
                            var distance = Vector3D.Distance(senderPosition, recieverPosition);

                            IGC.SendUnicastMessage(message.Source, "Launch Response", $"{requestID}|{distance}");
                        }
                    }
                }
            }
        }
    }
}

void ParseConfig()
{
    MyIniParseResult result;
    if (!ini.TryParse(Me.CustomData, out result))
        throw new Exception(result.ToString());

    mTypes.Clear();
    List<string> sections = new List<string>();
    ini.GetSections(sections);

    foreach (var section in sections)
    {
        if (section == "General Settings")
        {
            mGroupTag = ini.Get(section, "Missile Group Tag").ToString(mGroupTag);
            reloadsPerUpdate = ini.Get(section, "Reloads Per Update").ToInt32(reloadsPerUpdate);
            scansPerUpdate = ini.Get(section, "Scans Per Update").ToInt32(scansPerUpdate);
            hatchDelayTime = ini.Get(section, "Hatch Delay Time").ToInt32(hatchDelayTime);
            cacheInterval = ini.Get(section, "Inventory Update Interval").ToInt32(cacheInterval);
        }
        else
        {
            MType mType = new MType(section);
            mType.Delay = ini.Get(section, "Delay").ToInt32();
            mType.ScatterAmmo = ini.Get(section, "Scatter Ammo").ToInt32();
            mTypes.Add(mType);
        }
    }
}
void WriteConfig()
{
    ini.Clear();

    ini.AddSection("General Settings");
    ini.AddSection("Hellfire");

    ini.Set("General Settings", "Missile Group Tag", mGroupTag);
    ini.Set("General Settings", "Reloads Per Update", reloadsPerUpdate.ToString());
    ini.Set("General Settings", "Scans Per Update", scansPerUpdate.ToString());
    ini.Set("General Settings", "Hatch Delay Time", hatchDelayTime.ToString());
    ini.Set("General Settings", "Inventory Update Interval", cacheInterval.ToString());

    ini.SetSectionComment("Hellfire", "Example missile configuration");
    ini.Set("Hellfire", "Delay", "5");
    ini.Set("Hellfire", "Scatter Ammo", "320");

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


bool IsValidBlock(IMyTerminalBlock b)
{
    return b != null && !b.Closed && b.IsFunctional;
}

int GetAmmoCount(IMyCargoContainer container)
{
    var items = new List<MyInventoryItem>();
    var sAmmo = container.GetInventory().FindItem(scatterAmmo);

    if (sAmmo.HasValue)
    {
        return (int)sAmmo.Value.Amount;
    }
    else return 0;
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

class MType
{
    public string Name { get; set; }
    public int Delay { get; set; }
    public int ScatterAmmo { get; set; }

    public MType(string name)
    {
        Name = name;
    }
}
class M
{
    public Queue<IMyThrust> Thrusters { get; set; }
    public Queue<IMyInventory> scatterCons { get; set; }
    public IMyTimerBlock launchTimer { get; set; }
    public bool isLaunched { get; set; }
    public M()
    {
        Thrusters = new Queue<IMyThrust>();
        scatterCons = new Queue<IMyInventory>();
        isLaunched = false;
    }
}

class Bay
{
    public List<Hardpoint> Hardpoints { get; private set; }
    public List<IMyShipWelder> Welders { get; private set; }
    public Hatch Hatch { get; set; }
    public IMyProjector Projector { get; set; }
    public IMyTimerBlock Timer { get; set; }
    public MType Type { get; set; }
    public int Number { get; set; }
    public bool IsSalvo { get; set; }
    public bool isAvailable { get; set; }
    public bool isReserved { get; set; }

    public Bay(MType type, int number, bool salvo)
    {
        Hardpoints = new List<Hardpoint>();
        Welders = new List<IMyShipWelder>();
        Hatch = new Hatch();
        Type = type;
        Number = number;
        IsSalvo = salvo;
        isAvailable = false;
    }
}
class Hardpoint
{
    public IMyShipMergeBlock Merge { get; set; }
    public IMyShipConnector Connector { get; set; }
    public IMyProjector Projector { get; set; }
    public M Missile { get; set; }

    public Hardpoint()
    {
        Missile = new M();
    }
}
class Hatch
{
    public List<IMyDoor> Doors { get; set; }
    public List<IMyMotorAdvancedStator> Hinges { get; set; }
    public Hatch()
    {
        Doors = new List<IMyDoor>();
        Hinges = new List<IMyMotorAdvancedStator>();
    }
}

class GuidanceCommand
{
    public string Type { get; set; }
    public int Number { get; set; }
    public int DelayCounter { get; set; }

    public class Comparer : IComparer<GuidanceCommand>
    {
        public int Compare(GuidanceCommand x, GuidanceCommand y)
        {
            return y.DelayCounter.CompareTo(x.DelayCounter);
        }
    }
}

class StatusLogger
{
    private string status;
    private bool split;
    private string report;

    public StatusLogger(bool split)
    {
        status = "";
        this.split = split;
    }

    public void Report(string r)
    {
        report = r;
    }
    public string BuildStatus(bool isRunning)
    {
        if (!isRunning)
        {
            Report("");
            return "EMS Core v" + version + "\n|- WAITING -|\n";
        }

        return "EMS Core v" + version + "\n|- RUNNING -|\n" + report;
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
            Data = "EMS Core v" + version,
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
            Color = Color.Cyan,
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
            Color = Color.Cyan,
            RotationOrScale = 0.85f
        });

        frame.Dispose();
    }
}