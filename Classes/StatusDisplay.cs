/// <summary>
/// Represents a status display that displays a log of actions on a textSurfaceProvider.
/// </summary>
class StatusDisplay
{
    private IMyTextSurface surface;
    private List<string> logEntries = new List<string>();
    private int logSlots;
    private int actionCount = 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="StatusDisplay"/> class.
    /// </summary>
    /// <param name="surface">The text surface to display the status on.</param>
    public StatusDisplay(IMyTextSurface surface)
    {
        this.surface = surface;
        this.surface.ContentType = ContentType.SCRIPT;
        this.surface.Script = "";
        this.surface.BackgroundColor = Color.Black;
        this.surface.ScriptBackgroundColor = Color.Black;

        logSlots = (int)(surface.SurfaceSize.Y - 160) / 20;
    }

    /// <summary>
    /// Logs an entry to the status display.
    /// </summary>
    /// <param name="entry">The entry to log.</param>
    /// <param name="isRunning">A flag indicating whether the system is running.</param>
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