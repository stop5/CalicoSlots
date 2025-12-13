namespace Stop50.CalicoSlots;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Minigames;

internal sealed class ModEntry : Mod
{
    Texture2D? Buttonmap;

    public override void Entry(IModHelper helper)
    {
        I18n.Init(helper.Translation);
        helper.Events.GameLoop.UpdateTicked += MinigameOverride;
        Buttonmap = helper.ModContent.Load<Texture2D>("assets/Button.png");
    }

    private void MinigameOverride(object? sender, UpdateTickedEventArgs e)
    {
        if (!Context.IsWorldReady || Game1.currentMinigame == null)
        {
            return;
        }
        var id = Game1.currentMinigame.minigameId();
        switch (id)
        {
            case "SlotsEdited":
                break;
            case "Slots":
                this.override_slots();
                break;
            default:
                return;
        }
    }

    private void override_slots()
    {
        Slots mg = (Slots)Game1.currentMinigame;
#pragma warning disable CS8604
        Game1.currentMinigame = new EditedSlots(Helper, Monitor, Buttonmap, mg.currentBet);
#pragma warning restore CS8604
    }
}

public enum Bet
{
    bet10 = 10,
    bet100 = 100,
    bet1k = 1000,
    bet10k = 10000,
    bet100k = 100000,
    bet1m = 1000000,
}

public class EditedSlots : IMinigame
{
    public const float slotTurnRate = 0.008f;
    public const int numberOfIcons = 8;
    public const int defaultBet = 10;
    private readonly string coinBuffer;
    private readonly List<float> slots;
    private readonly List<float> slotResults;
    private readonly ClickableComponent spinButton10;
    private readonly ClickableComponent spinButton100;
    private readonly ClickableComponent spinButton1000;
    private readonly ClickableComponent spinButton10k;
    private readonly ClickableComponent spinButton100k;
    private readonly ClickableComponent spinButtonmillion;
    private readonly ClickableComponent doneButton;

    public bool spinning;

    public bool showResult;

    public float payoutModifier;

    public int currentBet;

    public int spinsCount;

    public int slotsFinished;

    public int endTimer;

    public ClickableComponent? currentlySnappedComponent;

    private int buttons;
    private readonly IModHelper helper;
    private readonly IMonitor monitor;
    private readonly Texture2D Buttonmap;

    private static readonly int button_shift = 92;
    private static readonly int margin = 10;

    private ClickableComponent button(string text)
    {
        var length = Game1.dialogueFont.MeasureString(text);
        length.X += 2 * margin + 3;

        Vector2 pos = Utility.getTopLeftPositionForCenteringOnScreen(
            Game1.viewport,
            (int)length.X,
            52,
            ((buttons % 2) == 0 ? (int)length.X : -button_shift) - button_shift,
            32 + 64 * (buttons / 2)
        );
        monitor.Log($"Position: {pos.X}, {pos.Y}, {length}, 52", LogLevel.Debug);
        buttons++;
        return new ClickableComponent(
            new Rectangle((int)pos.X, (int)pos.Y, (int)length.X, 52),
            text
        );
    }

    public EditedSlots(IModHelper helper, IMonitor monitor, Texture2D buttonmap, int toBet = -1)
    {
        this.helper = helper;
        this.monitor = monitor;
        Buttonmap = buttonmap;
        buttons = 0;
        coinBuffer =
            (LocalizedContentManager.CurrentLanguageCode == LocalizedContentManager.LanguageCode.ru)
                ? "     "
                : (
                    (
                        LocalizedContentManager.CurrentLanguageCode
                        == LocalizedContentManager.LanguageCode.zh
                    )
                        ? "\u3000\u3000"
                        : "  "
                );
        currentBet = toBet;
        if (currentBet == -1)
        {
            currentBet = 10;
        }
        slots = new List<float> { 0f, 0f, 0f };
        slotResults = new List<float> { 0f, 0f, 0f };
        Game1.playSound("newArtifact");
        SetSlotResults(slots);

        spinButton10 = button(I18n.Bet((int)Bet.bet10));
        spinButton100 = button(I18n.Bet((int)Bet.bet100));
        spinButton1000 = button(I18n.Bet((int)Bet.bet1k));
        spinButton10k = button(I18n.Bet((int)Bet.bet10k));
        spinButton100k = button(I18n.Bet((int)Bet.bet100k));
        spinButtonmillion = button(I18n.Bet((int)Bet.bet1m));
        doneButton = button(I18n.Done());
        if (Game1.isAnyGamePadButtonBeingPressed())
        {
            Game1.setMousePosition(spinButton10.bounds.Center);
            if (Game1.options.SnappyMenus)
            {
                currentlySnappedComponent = spinButton10;
            }
        }
    }

    public void SetSlotResults(List<float> toSet)
    {
        double d = Game1.random.NextDouble();
        double modifier =
            1.0 + Game1.player.DailyLuck * 2.0 + (double)Game1.player.LuckLevel * 0.08;
        if (d < 0.001 * modifier)
        {
            Set(toSet, 5);
            payoutModifier = 2500f;
            return;
        }
        if (d < 0.0016 * modifier)
        {
            Set(toSet, 6);
            payoutModifier = 1000f;
            return;
        }
        if (d < 0.0025 * modifier)
        {
            Set(toSet, 7);
            payoutModifier = 500f;
            return;
        }
        if (d < 0.005 * modifier)
        {
            Set(toSet, 4);
            payoutModifier = 200f;
            return;
        }
        if (d < 0.007 * modifier)
        {
            Set(toSet, 3);
            payoutModifier = 120f;
            return;
        }
        if (d < 0.01 * modifier)
        {
            Set(toSet, 2);
            payoutModifier = 80f;
            return;
        }
        if (d < 0.02 * modifier)
        {
            Set(toSet, 1);
            payoutModifier = 30f;
            return;
        }
        if (d < 0.12 * modifier)
        {
            int whereToPutNonStar = Game1.random.Next(3);
            for (int k = 0; k < 3; k++)
            {
                toSet[k] = (k == whereToPutNonStar) ? Game1.random.Next(7) : 7;
            }
            payoutModifier = 3f;
            return;
        }
        if (d < 0.2 * modifier)
        {
            Set(toSet, 0);
            payoutModifier = 5f;
            return;
        }
        if (d < 0.4 * modifier)
        {
            int whereToPutStar = Game1.random.Next(3);
            for (int j = 0; j < 3; j++)
            {
                toSet[j] = (j == whereToPutStar) ? 7 : Game1.random.Next(7);
            }
            payoutModifier = 2f;
            return;
        }
        payoutModifier = 0f;
        int[] used = new int[8];
        for (int i = 0; i < 3; i++)
        {
            int next = Game1.random.Next(6);
            while (used[next] > 1)
            {
                next = Game1.random.Next(6);
            }
            toSet[i] = next;
            used[next]++;
        }
    }

    private void Set(List<float> toSet, int number)
    {
        toSet[0] = number;
        toSet[1] = number;
        toSet[2] = number;
    }

    private float Scale(ClickableComponent b)
    {
        if (!spinning && b.bounds.Contains(Game1.getOldMouseX(), Game1.getOldMouseY()))
        {
            return 1.05f;
        }
        return 1.0f;
    }

    public bool tick(GameTime time)
    {
        if (spinning && endTimer <= 0)
        {
            for (int i = slotsFinished; i < slots.Count; i++)
            {
                float old = slots[i];
                slots[i] +=
                    (float)time.ElapsedGameTime.Milliseconds * 0.008f * (1f - (float)i * 0.05f);
                slots[i] %= 8f;
                if (i == 2)
                {
                    if (
                        old % (0.25f + (float)slotsFinished * 0.5f)
                        > slots[i] % (0.25f + (float)slotsFinished * 0.5f)
                    )
                    {
                        Game1.playSound("shiny4");
                    }
                    if (old > slots[i])
                    {
                        spinsCount++;
                    }
                }
                if (
                    spinsCount > 0
                    && i == slotsFinished
                    && Math.Abs(slots[i] - slotResults[i])
                        <= (float)time.ElapsedGameTime.Milliseconds * 0.008f
                )
                {
                    slots[i] = slotResults[i];
                    slotsFinished++;
                    spinsCount--;
                    Game1.playSound("Cowboy_gunshot");
                }
            }
            if (slotsFinished >= 3)
            {
                endTimer = (payoutModifier == 0f) ? 600 : 1000;
            }
        }
        if (endTimer > 0)
        {
            endTimer -= time.ElapsedGameTime.Milliseconds;
            if (endTimer <= 0)
            {
                spinning = false;
                spinsCount = 0;
                slotsFinished = 0;
                if (payoutModifier > 0f)
                {
                    showResult = true;
                    Game1.playSound(
                        (!(payoutModifier >= 5f))
                            ? "newArtifact"
                            : ((payoutModifier >= 10f) ? "reward" : "money")
                    );
                }
                else
                {
                    Game1.playSound("breathout");
                }
                var earnings = (int)((float)currentBet * payoutModifier);
                if (Game1.player.clubCoins >= (int.MaxValue - earnings))
                {
                    Game1.player.clubCoins = int.MaxValue;
                    Game1.Multiplayer.globalChatInfoMessage(helper.Translation.Get("brokethebank"));
                }
                else
                {
                    Game1.player.clubCoins += earnings;
                    if (payoutModifier == 2500f)
                    {
                        Game1.Multiplayer.globalChatInfoMessage("Jackpot", Game1.player.Name);
                    }
                }
            }
        }
        spinButton10.scale = Scale(spinButton10);
        spinButton100.scale = Scale(spinButton100);
        spinButton1000.scale = Scale(spinButton1000);
        spinButton10k.scale = Scale(spinButton10k);
        spinButton100k.scale = Scale(spinButton100k);
        spinButtonmillion.scale = Scale(spinButtonmillion);
        doneButton.scale = Scale(doneButton);
        return false;
    }

    public void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (spinning)
        {
            return;
        }
        if (doneButton.bounds.Contains(x, y))
        {
            Game1.playSound("bigDeSelect");
            Game1.currentMinigame = null;
            return;
        }
        if (Game1.player.clubCoins >= 10 && spinButton10.bounds.Contains(x, y))
        {
            Club.timesPlayedSlots++;
            SetSlotResults(slotResults);
            spinning = true;
            Game1.playSound("bigSelect");
            slotsFinished = 0;
            spinsCount = 0;
            showResult = false;
            currentBet = (int)Bet.bet10;
            Game1.player.clubCoins -= (int)Bet.bet10;
        }
        if (Game1.player.clubCoins >= (int)Bet.bet100 && spinButton100.bounds.Contains(x, y))
        {
            Club.timesPlayedSlots++;
            SetSlotResults(slotResults);
            Game1.playSound("bigSelect");
            spinning = true;
            slotsFinished = 0;
            spinsCount = 0;
            showResult = false;
            currentBet = (int)Bet.bet100;
            Game1.player.clubCoins -= (int)Bet.bet100;
        }
        if (Game1.player.clubCoins >= (int)Bet.bet1k && spinButton1000.bounds.Contains(x, y))
        {
            Club.timesPlayedSlots++;
            SetSlotResults(slotResults);
            Game1.playSound("bigSelect");
            spinning = true;
            slotsFinished = 0;
            spinsCount = 0;
            showResult = false;
            currentBet = (int)Bet.bet1k;
            Game1.player.clubCoins -= (int)Bet.bet1k;
        }
        if (Game1.player.clubCoins >= (int)Bet.bet10k && spinButton10k.bounds.Contains(x, y))
        {
            Club.timesPlayedSlots++;
            SetSlotResults(slotResults);
            Game1.playSound("bigSelect");
            spinning = true;
            slotsFinished = 0;
            spinsCount = 0;
            showResult = false;
            currentBet = (int)Bet.bet10k;
            Game1.player.clubCoins -= (int)Bet.bet10k;
        }
        if (Game1.player.clubCoins >= (int)Bet.bet100k && spinButton100k.bounds.Contains(x, y))
        {
            Club.timesPlayedSlots++;
            SetSlotResults(slotResults);
            Game1.playSound("bigSelect");
            spinning = true;
            slotsFinished = 0;
            spinsCount = 0;
            showResult = false;
            currentBet = (int)Bet.bet100k;
            Game1.player.clubCoins -= (int)Bet.bet100k;
        }
        if (Game1.player.clubCoins >= (int)Bet.bet1m && spinButtonmillion.bounds.Contains(x, y))
        {
            Club.timesPlayedSlots++;
            SetSlotResults(slotResults);
            Game1.playSound("bigSelect");
            spinning = true;
            slotsFinished = 0;
            spinsCount = 0;
            showResult = false;
            currentBet = (int)Bet.bet1m;
            Game1.player.clubCoins -= (int)Bet.bet1m;
        }
    }

    public void leftClickHeld(int x, int y) { }

    public void receiveRightClick(int x, int y, bool playSound = true) { }

    public void releaseLeftClick(int x, int y) { }

    public void releaseRightClick(int x, int y) { }

    public bool overrideFreeMouseMovement()
    {
        return Game1.options.SnappyMenus;
    }

    public void receiveKeyPress(Keys k)
    {
        if (
            !spinning
            && (
                k.Equals(Keys.Escape)
                || Game1.options.doesInputListContain(Game1.options.menuButton, k)
            )
        )
        {
            unload();
            Game1.playSound("bigDeSelect");
            Game1.currentMinigame = null;
        }
        else
        {
            if (spinning || currentlySnappedComponent == null)
            {
                return;
            }
            if (Game1.options.doesInputListContain(Game1.options.moveDownButton, k))
            {
                if (currentlySnappedComponent.Equals(spinButton10))
                {
                    currentlySnappedComponent = spinButton100;
                    Game1.setMousePosition(currentlySnappedComponent.bounds.Center);
                }
                else if (currentlySnappedComponent.Equals(spinButton100))
                {
                    currentlySnappedComponent = doneButton;
                    Game1.setMousePosition(currentlySnappedComponent.bounds.Center);
                }
            }
            else if (Game1.options.doesInputListContain(Game1.options.moveUpButton, k))
            {
                if (currentlySnappedComponent.Equals(doneButton))
                {
                    currentlySnappedComponent = spinButton100;
                    Game1.setMousePosition(currentlySnappedComponent.bounds.Center);
                }
                else if (currentlySnappedComponent.Equals(spinButton100))
                {
                    currentlySnappedComponent = spinButton10;
                    Game1.setMousePosition(currentlySnappedComponent.bounds.Center);
                }
            }
        }
    }

    public void receiveKeyRelease(Keys k) { }

    public int getIconIndex(int index)
    {
        return index switch
        {
            0 => 24,
            1 => 186,
            2 => 138,
            3 => 392,
            4 => 254,
            5 => 434,
            6 => 72,
            7 => 638,
            _ => 24,
        };
    }

    private static readonly Rectangle button_border = new(0, 0, 3, 13);

    private void drawbutton(SpriteBatch b, ClickableComponent butt, Color color)
    {
        var coords = new Vector2(butt.bounds.X, butt.bounds.Y);
        b.Draw(
            Buttonmap,
            coords,
            button_border,
            color,
            0f,
            Vector2.Zero,
            4f * butt.scale,
            SpriteEffects.None,
            0.99f
        );
        coords.X += 10;
        int length;
        while (coords.X < butt.bounds.Right - 3)
        {
            length = butt.bounds.Right - (int)coords.X;
            if (length > 4)
            {
                length = 4;
            }
            b.Draw(
                Buttonmap,
                coords,
                new Rectangle(3, 0, length, 13),
                color,
                0f,
                Vector2.Zero,
                4f * butt.scale,
                SpriteEffects.None,
                0.99f
            );
            coords.X += length;
        }
        b.Draw(
            Buttonmap,
            coords,
            button_border,
            color,
            0f,
            Vector2.Zero,
            4f * butt.scale,
            SpriteEffects.FlipHorizontally,
            0.99f
        );
        b.DrawString(
            Game1.dialogueFont,
            butt.name,
            new Vector2(butt.bounds.X + margin, butt.bounds.Y + margin),
            color
        );
    }

    public void draw(SpriteBatch b)
    {
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
        b.Draw(
            Game1.staminaRect,
            new Rectangle(
                0,
                0,
                Game1.graphics.GraphicsDevice.Viewport.Width,
                Game1.graphics.GraphicsDevice.Viewport.Height
            ),
            new Color(38, 0, 7)
        );
        b.Draw(
            Game1.mouseCursors,
            Utility.getTopLeftPositionForCenteringOnScreen(Game1.viewport, 228, 52, 0, -256),
            new Rectangle(441, 424, 57, 13),
            Color.White,
            0f,
            Vector2.Zero,
            4f,
            SpriteEffects.None,
            0.99f
        );
        for (int i = 0; i < 3; i++)
        {
            b.Draw(
                Game1.mouseCursors,
                new Vector2(
                    Game1.graphics.GraphicsDevice.Viewport.Width / 2 - 112 + i * 26 * 4,
                    Game1.graphics.GraphicsDevice.Viewport.Height / 2 - 128
                ),
                new Rectangle(306, 320, 16, 16),
                Color.White,
                0f,
                Vector2.Zero,
                4f,
                SpriteEffects.None,
                0.99f
            );
            float num = (slots[i] + 1f) % 8f;
            int iconIndex = getIconIndex(((int)num + 8 - 1) % 8);
            int iconIndex2 = getIconIndex((iconIndex + 1) % 8);
            b.Draw(
                Game1.objectSpriteSheet,
                new Vector2(
                    Game1.graphics.GraphicsDevice.Viewport.Width / 2 - 112 + i * 26 * 4,
                    Game1.graphics.GraphicsDevice.Viewport.Height / 2 - 128
                ) - new Vector2(0f, -64f * (num % 1f)),
                Game1.getSourceRectForStandardTileSheet(Game1.objectSpriteSheet, iconIndex, 16, 16),
                Color.White,
                0f,
                Vector2.Zero,
                4f,
                SpriteEffects.None,
                0.99f
            );
            b.Draw(
                Game1.objectSpriteSheet,
                new Vector2(
                    Game1.graphics.GraphicsDevice.Viewport.Width / 2 - 112 + i * 26 * 4,
                    Game1.graphics.GraphicsDevice.Viewport.Height / 2 - 128
                ) - new Vector2(0f, 64f - 64f * (num % 1f)),
                Game1.getSourceRectForStandardTileSheet(
                    Game1.objectSpriteSheet,
                    iconIndex2,
                    16,
                    16
                ),
                Color.White,
                0f,
                Vector2.Zero,
                4f,
                SpriteEffects.None,
                0.99f
            );
            b.Draw(
                Game1.mouseCursors,
                new Vector2(
                    Game1.graphics.GraphicsDevice.Viewport.Width / 2 - 132 + i * 26 * 4,
                    Game1.graphics.GraphicsDevice.Viewport.Height / 2 - 192
                ),
                new Rectangle(415, 385, 26, 48),
                Color.White,
                0f,
                Vector2.Zero,
                4f,
                SpriteEffects.None,
                0.99f
            );
        }

        if (showResult)
        {
            SpriteText.drawString(
                b,
                "+" + payoutModifier * (float)currentBet,
                Game1.graphics.GraphicsDevice.Viewport.Width / 2 - 372,
                spinButton10.bounds.Y - 64 + 8,
                9999,
                -1,
                9999,
                1f,
                1f,
                junimoText: false,
                -1,
                "",
                SpriteText.color_White
            );
        }
        drawbutton(
            b,
            spinButton10,
            Color.White * ((!spinning && Game1.player.clubCoins >= (int)Bet.bet10) ? 1f : 0.5f)
        );
        drawbutton(
            b,
            spinButton100,
            Color.White * ((!spinning && Game1.player.clubCoins >= (int)Bet.bet100) ? 1f : 0.5f)
        );
        drawbutton(
            b,
            spinButton1000,
            Color.White * ((!spinning && Game1.player.clubCoins >= (int)Bet.bet1k) ? 1f : 0.5f)
        );
        drawbutton(
            b,
            spinButton10k,
            Color.White * ((!spinning && Game1.player.clubCoins >= (int)Bet.bet10k) ? 1f : 0.5f)
        );
        drawbutton(
            b,
            spinButton100k,
            Color.White * ((!spinning && Game1.player.clubCoins >= (int)Bet.bet100k) ? 1f : 0.5f)
        );
        drawbutton(
            b,
            spinButtonmillion,
            Color.White * ((!spinning && Game1.player.clubCoins >= (int)Bet.bet1m) ? 1f : 0.5f)
        );
        drawbutton(b, doneButton, Color.White * ((!spinning) ? 1f : 0.5f));
        SpriteText.drawStringWithScrollBackground(
            b,
            coinBuffer + Game1.player.clubCoins,
            Game1.graphics.GraphicsDevice.Viewport.Width / 2 - 376,
            Game1.graphics.GraphicsDevice.Viewport.Height / 2 - 120
        );
        Utility.drawWithShadow(
            b,
            Game1.mouseCursors,
            new Vector2(
                Game1.graphics.GraphicsDevice.Viewport.Width / 2 - 376 + 4,
                Game1.graphics.GraphicsDevice.Viewport.Height / 2 - 120 + 4
            ),
            new Rectangle(211, 373, 9, 10),
            Color.White,
            0f,
            Vector2.Zero,
            4f,
            flipped: false,
            1f
        );
        Vector2 vector = new Vector2(
            Game1.graphics.GraphicsDevice.Viewport.Width / 2 + 200,
            Game1.graphics.GraphicsDevice.Viewport.Height / 2 - 352
        );
        IClickableMenu.drawTextureBox(
            b,
            Game1.mouseCursors,
            new Rectangle(375, 357, 3, 3),
            (int)vector.X,
            (int)vector.Y,
            384,
            704,
            Color.White,
            4f
        );
        b.Draw(
            Game1.objectSpriteSheet,
            vector + new Vector2(8f, 8f),
            Game1.getSourceRectForStandardTileSheet(
                Game1.objectSpriteSheet,
                getIconIndex(7),
                16,
                16
            ),
            Color.White,
            0f,
            Vector2.Zero,
            4f,
            SpriteEffects.None,
            0.99f
        );
        SpriteText.drawString(
            b,
            "x2",
            (int)vector.X + 192 + 16,
            (int)vector.Y + 24,
            9999,
            -1,
            99999,
            1f,
            0.88f,
            junimoText: false,
            -1,
            "",
            SpriteText.color_White
        );
        b.Draw(
            Game1.objectSpriteSheet,
            vector + new Vector2(8f, 76f),
            Game1.getSourceRectForStandardTileSheet(
                Game1.objectSpriteSheet,
                getIconIndex(7),
                16,
                16
            ),
            Color.White,
            0f,
            Vector2.Zero,
            4f,
            SpriteEffects.None,
            0.99f
        );
        b.Draw(
            Game1.objectSpriteSheet,
            vector + new Vector2(76f, 76f),
            Game1.getSourceRectForStandardTileSheet(
                Game1.objectSpriteSheet,
                getIconIndex(7),
                16,
                16
            ),
            Color.White,
            0f,
            Vector2.Zero,
            4f,
            SpriteEffects.None,
            0.99f
        );
        SpriteText.drawString(
            b,
            "x3",
            (int)vector.X + 192 + 16,
            (int)vector.Y + 68 + 24,
            9999,
            -1,
            99999,
            1f,
            0.88f,
            junimoText: false,
            -1,
            "",
            SpriteText.color_White
        );
        for (int j = 0; j < 8; j++)
        {
            int index = j;
            switch (j)
            {
                case 5:
                    index = 7;
                    break;
                case 7:
                    index = 5;
                    break;
            }

            b.Draw(
                Game1.objectSpriteSheet,
                vector + new Vector2(8f, 8 + (j + 2) * 68),
                Game1.getSourceRectForStandardTileSheet(
                    Game1.objectSpriteSheet,
                    getIconIndex(index),
                    16,
                    16
                ),
                Color.White,
                0f,
                Vector2.Zero,
                4f,
                SpriteEffects.None,
                0.99f
            );
            b.Draw(
                Game1.objectSpriteSheet,
                vector + new Vector2(76f, 8 + (j + 2) * 68),
                Game1.getSourceRectForStandardTileSheet(
                    Game1.objectSpriteSheet,
                    getIconIndex(index),
                    16,
                    16
                ),
                Color.White,
                0f,
                Vector2.Zero,
                4f,
                SpriteEffects.None,
                0.99f
            );
            b.Draw(
                Game1.objectSpriteSheet,
                vector + new Vector2(144f, 8 + (j + 2) * 68),
                Game1.getSourceRectForStandardTileSheet(
                    Game1.objectSpriteSheet,
                    getIconIndex(index),
                    16,
                    16
                ),
                Color.White,
                0f,
                Vector2.Zero,
                4f,
                SpriteEffects.None,
                0.99f
            );
            int num2 = 0;
            switch (j)
            {
                case 0:
                    num2 = 5;
                    break;
                case 1:
                    num2 = 30;
                    break;
                case 2:
                    num2 = 80;
                    break;
                case 3:
                    num2 = 120;
                    break;
                case 4:
                    num2 = 200;
                    break;
                case 5:
                    num2 = 500;
                    break;
                case 6:
                    num2 = 1000;
                    break;
                case 7:
                    num2 = 2500;
                    break;
            }

            SpriteText.drawString(
                b,
                "x" + num2,
                (int)vector.X + 192 + 16,
                (int)vector.Y + (j + 2) * 68 + 24,
                9999,
                -1,
                99999,
                1f,
                0.88f,
                junimoText: false,
                -1,
                "",
                SpriteText.color_White
            );
        }

        IClickableMenu.drawTextureBox(
            b,
            Game1.mouseCursors,
            new Rectangle(379, 357, 3, 3),
            (int)vector.X - 640,
            (int)vector.Y,
            1024,
            704,
            Color.Red,
            4f,
            drawShadow: false
        );
        for (int k = 1; k < 8; k++)
        {
            IClickableMenu.drawTextureBox(
                b,
                Game1.mouseCursors,
                new Rectangle(379, 357, 3, 3),
                (int)vector.X - 640 - 4 * k,
                (int)vector.Y - 4 * k,
                1024 + 8 * k,
                704 + 8 * k,
                Color.Red * (1f - (float)k * 0.15f),
                4f,
                drawShadow: false
            );
        }

        for (int l = 0; l < 17; l++)
        {
            IClickableMenu.drawTextureBox(
                b,
                Game1.mouseCursors,
                new Rectangle(147, 472, 3, 3),
                (int)vector.X - 640 + 8,
                (int)vector.Y + l * 4 * 3 + 12,
                (int)(608f - (float)(l * 64) * 1.2f + (float)(l * l * 4) * 0.7f),
                4,
                new Color(l * 25, (l > 8) ? (l * 10) : 0, 255 - l * 25),
                4f,
                drawShadow: false
            );
        }

        if (Game1.IsMultiplayer)
        {
            Utility.drawTextWithColoredShadow(
                b,
                Game1.getTimeOfDayString(Game1.timeOfDay),
                Game1.dialogueFont,
                new Vector2(
                    vector.X
                        + 416f
                        - Game1
                            .dialogueFont.MeasureString(Game1.getTimeOfDayString(Game1.timeOfDay))
                            .X,
                    vector.Y - 72f
                ),
                Color.Purple,
                Color.Black * 0.2f
            );
        }

        if (!Game1.options.hardwareCursor)
        {
            b.Draw(
                Game1.mouseCursors,
                new Vector2(Game1.getMouseX(), Game1.getMouseY()),
                Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 0, 16, 16),
                Color.White,
                0f,
                Vector2.Zero,
                4f + Game1.dialogueButtonScale / 150f,
                SpriteEffects.None,
                1f
            );
        }

        b.End();
    }

    public void changeScreenSize() { }

    public void unload() { }

    public void receiveEventPoke(int data) { }

    public string minigameId()
    {
        return "SlotsEdited";
    }

    public bool doMainGameUpdates()
    {
        return false;
    }

    public bool forceQuit()
    {
        if (spinning)
        {
            Game1.player.clubCoins += currentBet;
        }
        unload();
        return true;
    }
}
