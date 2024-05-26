//draws graphical sprite-based status for EMS core
//not even close to finished, but functional
//by eboyar

bool usingLaunchController = false;

//ignore, used for unit testing
readonly int uFrequency = 2;

public class MissileBay
{
    public List<IMyShipMergeBlock> MergeBlocks { get; set; }
    public bool IsSalvo { get; set; }

    public MissileBay()
    {
        MergeBlocks = new List<IMyShipMergeBlock>();
    }
}
public class CustomSprite
{
    public SpriteType Type { get; set; }
    public string Name { get; set; }
    public Vector2 Position { get; set; }
    public Vector2 Size { get; set; }
    public Color Color { get; set; }
    public float Rotation { get; set; }
}
public class StatusDisplay
{
    public IMyTextPanel Surface { get; set; }
    public CustomSprite Sprite { get; set; }
    public float Scale { get; set; }
    public int rowMaximum { get; set; }
    public string Name { get; set; }
    public Color TitleColor { get; set; }
    public List<CustomSprite> MissileSprite { get; set; }
}

public List<string> MissileTypes;

List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
Dictionary<string, List<MissileBay>> missiles = new Dictionary<string, List<MissileBay>>();
Dictionary<string, int> missileAvailability = new Dictionary<string, int>();

MyCommandLine _cL = new MyCommandLine();
MyIni ini = new MyIni();

int availible;
int uFreqCounter = 0;

Dictionary<string, List<StatusDisplay>> siloStatusDisplays = new Dictionary<string, List<StatusDisplay>>();
Dictionary<string, List<StatusDisplay>> salvoStatusDisplays = new Dictionary<string, List<StatusDisplay>>();
IMyTextPanel currentLcd;
IMyProgrammableBlock EMScore;

Vector2 titlePos;
Vector2 countPos;
TextAlignment titleAlignemnt;
TextAlignment countAlignment;

public Program()
{
    if (!usingLaunchController)
    {
        Runtime.UpdateFrequency = UpdateFrequency.Update100;
    }
    Setup();
    updateAllMissileAvailability();
}
public void Main(string argument, UpdateType updateSource)
{
    if (_cL.TryParse(argument))
    {
        string[] args = argument.Split(' ');
        string firstArg = args[0];
        if (usingLaunchController)
        {
            if (args.Length > 3)
            {
                if (firstArg == "launch")
                {
                    string type = args[2];
                    string missileType = args[1];
                    if (MissileTypes.Contains(missileType))
                    {
                        int baysLaunched;
                        if (int.TryParse(args[3], out baysLaunched))
                        {
                            if (missileAvailability.ContainsKey(missileType))
                            {
                                missileAvailability[missileType] = Math.Max(0, missileAvailability[missileType] - baysLaunched);
                            }
                        }
                        if (type == "silo")
                        {
                            DrawSiloMissiles(missileType);
                        }
                        else if (type == "salvo")
                        {
                            DrawSalvoMissiles(missileType);
                        }
                    }
                }
            }
        }
        if (firstArg == "reload")
        {
            updateAllMissileAvailability();
        }
    }
    if ((updateSource & UpdateType.Update100) != 0)
    {
        if (!usingLaunchController && uFreqCounter == uFrequency)
        {
            updateAllMissileAvailability();
            uFreqCounter = 0;
        }
        else uFreqCounter++;
    }

    Echo("Last Run:" + Runtime.LastRunTimeMs.ToString() + "ms");
}
public void Setup()
{
    EMScore = GridTerminalSystem.GetBlockWithName("EMS-core") as IMyProgrammableBlock;
    MyIniParseResult result;
    if (!ini.TryParse(EMScore.CustomData, out result))
    {
        Echo("Failed to parse INI from EMS-core custom data: " + result.ToString());
    }
    ini.GetSections(MissileTypes);

    GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(blocks);

    foreach (var block in blocks)
    {
        string[] nameParts = block.CustomName.Split(' ');
        if (nameParts.Length > 2)
        {
            string missileType = nameParts[0];
            if (MissileTypes.Contains(missileType))
            {
                var mergeBlock = block as IMyShipMergeBlock;
                if (mergeBlock != null && (nameParts[1].Equals("silo", StringComparison.OrdinalIgnoreCase) || nameParts[1].Equals("salvo", StringComparison.OrdinalIgnoreCase)))
                {
                    if (!missiles.ContainsKey(missileType))
                    {
                        missiles[missileType] = new List<MissileBay>();
                    }

                    var bay = missiles[missileType].FirstOrDefault(b => b.MergeBlocks.Any(m => m.CustomName == mergeBlock.CustomName));
                    if (bay == null)
                    {
                        bay = new MissileBay { IsSalvo = nameParts[1].Equals("salvo", StringComparison.OrdinalIgnoreCase) };
                        missiles[missileType].Add(bay);
                    }

                    bay.MergeBlocks.Add(mergeBlock);
                }

                var lcd = block as IMyTextSurface;
                if (lcd != null && nameParts[2].Equals("status", StringComparison.OrdinalIgnoreCase))
                {
                    if (nameParts[1].Equals("silo", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!siloStatusDisplays.ContainsKey(missileType))
                        {
                            siloStatusDisplays[missileType] = new List<StatusDisplay>();
                        }
                        siloStatusDisplays[missileType].Add(CreateStatusDisplay((IMyTextPanel)lcd));
                    }
                    else if (nameParts[1].Equals("salvo", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!salvoStatusDisplays.ContainsKey(missileType))
                        {
                            salvoStatusDisplays[missileType] = new List<StatusDisplay>();
                        }
                        salvoStatusDisplays[missileType].Add(CreateStatusDisplay((IMyTextPanel)lcd));
                    }
                }
            }
        }
    }

    foreach (var missileType in MissileTypes)
    {
        if (!missiles.ContainsKey(missileType))
        {
            missiles[missileType] = new List<MissileBay>();
        }
        missileAvailability[missileType] = missiles[missileType].Count;
    }
    blocks.Clear();
}

public void DrawSiloMissiles(string missileType)
{
    if (!siloStatusDisplays.ContainsKey(missileType))
    {
        return;
    }

    List<StatusDisplay> statusDisplaysForType = siloStatusDisplays[missileType];

    availible = missileAvailability[missileType];

    foreach (StatusDisplay statusDisplay in statusDisplaysForType)
    {
        currentLcd = statusDisplay.Surface;

        currentLcd.ContentType = ContentType.SCRIPT;
        currentLcd.Script = "";
        float aspectRatio = currentLcd.SurfaceSize.X / currentLcd.SurfaceSize.Y;
        float scale;
        float xOffset;
        float yOffset;
        float yStart;

        float rowCount = 12f;
        float gapScale = 4f;

        if (Math.Abs(aspectRatio - 1f) < 0.01f) // 1x1 & 3x3 & 5x5 LCD
        {
            rowCount = 6f;
            gapScale = 1f;
        }

        int rowMax = (int)(availible > (rowCount * 4) ? ((availible - 1) / 8 + 1) * 2 : rowCount);
        float gap = rowCount / rowMax * gapScale;

        int numberOfRows = (int)Math.Ceiling((double)availible / rowMax);

        if (Math.Abs(aspectRatio - 2f) < 0.01f) // 2x1 LCD
        {
            scale = 1f / (float)Math.Log(numberOfRows + 1, 2) * statusDisplay.Scale;
            xOffset = currentLcd.SurfaceSize.X / ((availible < rowMax ? availible : rowMax) + gap);
            yOffset = currentLcd.SurfaceSize.Y / (numberOfRows + 1f);
            yStart = currentLcd.SurfaceSize.Y / 2 - yOffset * (numberOfRows - 1) / 2;
        }
        else // 1x1 & 3x3 & 5x5 LCD
        {
            scale = 0.8f / (float)Math.Log(numberOfRows + 1, 2) * statusDisplay.Scale;
            xOffset = currentLcd.SurfaceSize.X / ((availible < rowMax ? availible : rowMax) + gap + 0.5f);
            yOffset = currentLcd.SurfaceSize.Y / (numberOfRows + 1.5f);
            yStart = currentLcd.SurfaceSize.Y / 2 - yOffset * (numberOfRows - 1) / 2;
        }
        //5x3 LCD support removed for now
        /*else if (Math.Abs(aspectRatio - (5f / 3f)) < 0.01f) // 5x3 LCD
        {
            scale = 0.6f / (float)Math.Log(numberOfRows + 1, 2);
            xOffset = lcd.SurfaceSize.X / ((availible < rowMax ? availible : rowMax) + gap);

            if (numberOfRows % 2 == 0) yOffset = lcd.SurfaceSize.Y / (numberOfRows + 1f);
            else if (numberOfRows == 1) yOffset = lcd.TextureSize.Y / 2 * 0.4f;
            else yOffset = lcd.SurfaceSize.Y / (numberOfRows);

            yStart = lcd.SurfaceSize.Y / 2 - yOffset * (numberOfRows - 1) / 2 + yOffset;
        }*/

        using (var frame = currentLcd.DrawFrame())
        {

            if (Math.Abs(aspectRatio - 2f) < 0.01f) // 2x1 LCD
            {
                titlePos = new Vector2(currentLcd.SurfaceSize.X * 1 / 5, currentLcd.SurfaceSize.Y * 0.015f);
                countPos = new Vector2(currentLcd.SurfaceSize.X * 32 / 40, currentLcd.SurfaceSize.Y * 0.015f);
                titleAlignemnt = TextAlignment.LEFT;
                countAlignment = TextAlignment.RIGHT;
                yStart += currentLcd.SurfaceSize.Y * 0.05f;
            }
            else
            {
                titlePos = new Vector2(currentLcd.SurfaceSize.X / 2, currentLcd.SurfaceSize.Y * 0.015f);
                countPos = new Vector2(currentLcd.SurfaceSize.X / 2, currentLcd.SurfaceSize.Y * 0.015f + 52);
                titleAlignemnt = TextAlignment.CENTER;
                countAlignment = TextAlignment.CENTER;
                yStart += currentLcd.SurfaceSize.Y * 0.10f;
            }
            string missileName = statusDisplay.Name;
            Color titleColor = statusDisplay.TitleColor;

            var titleName = new MySprite()
            {
                Type = SpriteType.TEXT,
                Data = $"{missileName}",
                Position = titlePos,
                RotationOrScale = 2.15f,
                Color = titleColor,
                Alignment = titleAlignemnt,
                FontId = "White"
            };
            frame.Add(titleName);

            var titleCount = new MySprite()
            {
                Type = SpriteType.TEXT,
                Data = $"x{availible}",
                Position = countPos,
                RotationOrScale = 2.15f,
                Color = Color.White,
                Alignment = countAlignment,
                FontId = "White"
            };
            frame.Add(titleCount);

            for (int i = 0; i < availible; i++)
            {
                int xPosition = i % rowMax;
                int yPosition = i / rowMax;

                int spritesInRow = Math.Min(rowMax, availible - yPosition * rowMax);
                float xStart = (currentLcd.SurfaceSize.X - xOffset * (spritesInRow - 1)) / 2;

                DrawSprites(frame, new Vector2(xStart + xOffset * xPosition, yStart + yOffset * yPosition), statusDisplay.Scale, statusDisplay.MissileSprite);
            }
        }
    }
}
public void DrawSalvoMissiles(string missileType)
{
    if (!salvoStatusDisplays.ContainsKey(missileType))
    {
        return;
    }

    List<StatusDisplay> statusDisplaysForType = salvoStatusDisplays[missileType];

    availible = missileAvailability[missileType];

    int salvoSize = missiles[missileType].First().MergeBlocks.Count;


    foreach (StatusDisplay statusDisplay in statusDisplaysForType)
    {
        currentLcd = statusDisplay.Surface;

        currentLcd.ContentType = ContentType.SCRIPT;
        currentLcd.Script = "";
        float aspectRatio = currentLcd.SurfaceSize.X / currentLcd.SurfaceSize.Y;
        float scale;
        float xOffset;
        float yOffset;
        float yStart;

        int rowMax = statusDisplay.rowMaximum == 0 ? 2 : Math.Min(statusDisplay.rowMaximum, 4);
        float rowCount = 6f * rowMax;
        float gapScale = 2f;

        if (Math.Abs(aspectRatio - 1f) < 0.01f) // 1x1 & 3x3 & 5x5 LCD
        {
            rowCount = 6f;
            gapScale = 1f;
            rowMax = 1;
        }
        float gap = rowCount / rowMax * gapScale;

        int numberOfRows = (int)Math.Ceiling((double)availible / rowMax);

        if (Math.Abs(aspectRatio - 2f) < 0.01f) // 2x1 LCD
        {
            scale = 1f / (float)Math.Log(numberOfRows + 1, 2) * statusDisplay.Scale;
            xOffset = currentLcd.SurfaceSize.X / ((availible < rowMax ? availible : rowMax) + gap + 1f);
            yOffset = currentLcd.SurfaceSize.Y / (numberOfRows + 1f);
            yStart = currentLcd.SurfaceSize.Y / 2 - yOffset * (numberOfRows - 1) / 2;
        }
        else // 1x1 & 3x3 & 5x5 LCD
        {
            scale = 1f / (float)Math.Log(numberOfRows + 1, 2) * statusDisplay.Scale;
            xOffset = currentLcd.SurfaceSize.X / ((availible < rowMax ? availible : rowMax) + gap + 0.5f);
            yOffset = currentLcd.SurfaceSize.Y / (numberOfRows + 2f);
            yStart = currentLcd.SurfaceSize.Y / 2 - yOffset * (numberOfRows - 1) / 2;
        }

        using (var frame = currentLcd.DrawFrame())
        {
            titlePos = new Vector2(currentLcd.SurfaceSize.X / 2, currentLcd.SurfaceSize.Y * 0.015f);
            countPos = new Vector2(currentLcd.SurfaceSize.X / 2, currentLcd.SurfaceSize.Y * 0.015f + 52);
            titleAlignemnt = TextAlignment.CENTER;
            countAlignment = TextAlignment.CENTER;
            yStart += currentLcd.SurfaceSize.Y * 0.10f;

            string missileName = statusDisplay.Name;
            Color titleColor = statusDisplay.TitleColor;

            var titleName = new MySprite()
            {
                Type = SpriteType.TEXT,
                Data = $"{missileName}",
                Position = titlePos,
                RotationOrScale = 2.15f,
                Color = titleColor,
                Alignment = titleAlignemnt,
                FontId = "White"
            };
            frame.Add(titleName);

            var titleCount = new MySprite()
            {
                Type = SpriteType.TEXT,
                Data = $"x{availible} SALVO",
                Position = countPos,
                RotationOrScale = 2.15f,
                Color = Color.White,
                Alignment = countAlignment,
                FontId = "White"
            };
            frame.Add(titleCount);

            float bundleXOffset = xOffset * 0.55f;

            if (Math.Abs(aspectRatio - 2f) < 0.01f)
            {
                int totalBundlesDrawn = 0;

                for (int i = 0; i < availible; i++)
                {
                    int xPosition = totalBundlesDrawn % rowMax;
                    int yPosition = totalBundlesDrawn / rowMax;

                    float bundleWidth = salvoSize * bundleXOffset;
                    float totalWidth = rowMax * bundleWidth + (rowMax - 1) * xOffset / 2;
                    float xStart = currentLcd.SurfaceSize.X / 2 - totalWidth / 2;

                    for (int j = 0; j < salvoSize; j++)
                    {
                        float xPositionBundle = xStart + xPosition * (bundleWidth + xOffset / 2) + bundleXOffset * j;
                        DrawSprites(frame, new Vector2(xPositionBundle, yStart + yOffset * yPosition), scale, statusDisplay.MissileSprite);
                    }

                    totalBundlesDrawn++;

                    if (totalBundlesDrawn >= availible)
                    {
                        break;
                    }
                }
            }
            else
            {
                for (int i = 0; i < availible; i++)
                {
                    int yPosition = i;

                    float bundleWidth = salvoSize * bundleXOffset;
                    float xStart = (currentLcd.SurfaceSize.X - bundleWidth) / 2;

                    for (int j = 0; j < salvoSize; j++)
                    {
                        DrawSprites(frame, new Vector2(xStart + bundleXOffset * j, yStart + yOffset * yPosition), scale, statusDisplay.MissileSprite);
                    }
                }
            }
        }
    }
}
public void DrawSprites(MySpriteDrawFrame frame, Vector2 position, float scale, List<CustomSprite> sprites)
{
    foreach (CustomSprite sprite in sprites)
    {
        MySprite mySprite = new MySprite()
        {
            Type = sprite.Type,
            Alignment = TextAlignment.CENTER,
            Data = sprite.Name,
            Position = position + sprite.Position * scale,
            Size = sprite.Size * scale,
            Color = sprite.Color,
            RotationOrScale = sprite.Rotation
        };

        frame.Add(mySprite);
    }
}

public StatusDisplay CreateStatusDisplay(IMyTextPanel lcd)
{
    MyIniParseResult result;
    if (!ini.TryParse(lcd.CustomData, out result))
    {
        Echo("Failed to parse INI from LCD custom data: " + result.ToString());
    }

    string[] spriteList = ini.Get("Sprite:0", "Sprite list").ToString().Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

    List<CustomSprite> sprites = new List<CustomSprite>();

    foreach (string spriteName in spriteList)
    {
        string textureSection = spriteName.Trim();
        CustomSprite sprite = new CustomSprite
        {
            Type = SpriteType.TEXTURE,
            Name = ini.Get(textureSection, "Type").ToString(),
            Position = ParseVector2(ini.Get(textureSection, "Position").ToString()),
            Size = ParseVector2(ini.Get(textureSection, "Size").ToString()),
            Color = ParseColor(ini.Get(textureSection, "Color").ToString()),
            Rotation = ini.Get(textureSection, "Rotation").ToSingle()
        };

        sprites.Add(sprite);
    }

    StatusDisplay display = new StatusDisplay
    {
        Surface = lcd,
        MissileSprite = sprites,
        Scale = ini.Get("Sprite:0", "Sprite scale").ToSingle(),
        Name = ini.Get("Sprite:0", "Sprite name").ToString(),
        rowMaximum = ini.Get("Sprite:0", "Row maximum").ToInt32(0),
        TitleColor = ParseColor(ini.Get("Sprite:0", "Title color").ToString())
    };

    return display;
}
public void updateAllMissileAvailability()
{
    foreach (var missileType in MissileTypes)
    {
        missileAvailability[missileType] = 0;
        foreach (var bay in missiles[missileType])
        {
            if (bay.MergeBlocks.All(m => m.IsConnected))
            {
                missileAvailability[missileType]++;
            }
        }
    }
    foreach (var missileType in MissileTypes)
    {
        DrawSiloMissiles(missileType);
        DrawSalvoMissiles(missileType);
    }
}
private Vector2 ParseVector2(string vectorString)
{
    string[] parts = vectorString.Trim('{', '}').Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length < 2)
    {
        throw new Exception($"Expected to find 'X:' and 'Y:' in '{vectorString}', but did not");
    }
    float x = float.Parse(parts[0].Split(':')[1]);
    float y = float.Parse(parts[1].Split(':')[1]);
    return new Vector2(x, y);
}
private Color ParseColor(string colorString)
{
    string[] parts = colorString.Replace(" ", "").Split(',');
    if (parts.Length != 4)
    {
        throw new Exception($"Expected 4 parts in color string, got {parts.Length}");
    }
    return new Color(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]), int.Parse(parts[3]));
}