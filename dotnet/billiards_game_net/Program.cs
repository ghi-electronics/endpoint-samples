// billiards_game_net - exercises the Endpoint display + touch + UI
// stack on the EPN256 (Nuvoton MA35D1) 1024x600 panel. No SkiaSharp -
// everything draws through the GHIElectronics.Endpoint.UI library (LVGL
// under the hood).
//
// What this does, in order:
//   1. Writes /config/display.json via DisplayController.SetSettings:
//      type = Parallel, backlight on PK7 (active-high),
//      1024x600 with the panel's timings (ma35-drm actually reads them
//      from the device tree; the fields here just describe the visible
//      area that LVGL uses for the canvas).
//   2. Calls DisplayController.Enable(), which modprobes ma35-drm and
//      raises the backlight.
//   3. UI.Initialize() wires LVGL to the framebuffer. TouchBridge.Attach()
//      sets up the input layer; Touch.GetDefault() opens the panel's
//      resistive touchscreen (ma35d1-ts kernel driver, always on).
//   4. Builds a screen: the billiards table wallpaper, a full set of 16
//      balls animated by a UiTimer (solids 1-8 and stripes 9-15 racked in
//      a triangle on the left, and a white cue ball on the right that
//      you shoot by sliding your finger off it; friction stops every
//      ball after a few seconds, then you touch the cue ball again for the
//      next shot) and an FPS readout.
//   5. Blinks the board's user LED in time with the cue ball: solid when the
//      table is at rest, blinking faster the faster the ball rolls.

using System.Device.Gpio;
using System.Runtime.InteropServices;

using GHIElectronics.Endpoint.Devices.Display;
using GHIElectronics.Endpoint.Devices.Touch;
using GHIElectronics.Endpoint.Pins;
using GHIElectronics.Endpoint.UI;

// GHIElectronics.Endpoint.UI has an Image widget of its own, so the decoder
// from GHIElectronics.Endpoint.Drawing is brought in under its own name.
using Image = GHIElectronics.Endpoint.Drawing.Image;

const int Width = 1024;
const int Height = 600;

using var sigInt  = PosixSignalRegistration.Create(PosixSignal.SIGINT,  c => { c.Cancel = true; UI.Stop(); });
using var sigTerm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, c => { c.Cancel = true; UI.Stop(); });

Console.WriteLine("billiards_game_net starting");

// -- 1. Write settings --------------------------------------------------------

var settings = new DisplaySettings {
    Type = DisplayType.Parallel,
    EnableAtBoot = false,
    Backlight = new BacklightSettings {
        Pin = EPN256.Gpio.Pin.PK7,
        ActivePolarity = SignalPolarity.ActiveHigh,
    },
    Parallel = new ParallelLcdSettings {
        Width = Width,
        Height = Height,
        PixelClockHz = 51_200_000,
        HFrontPorch = 160, HSync = 20, HBackPorch = 160,
        VFrontPorch = 12,  VSync = 3,  VBackPorch = 23,
        BitsPerPixel = 16,
        DataEnablePolarity = SignalPolarity.ActiveHigh,
        PixelClockEdge = ClockEdge.Rising,
    },
};
DisplayController.SetSettings(settings);

// -- 2. Enable display -------------------------------------------------------

using var display = DisplayController.GetDefault()
    ?? throw new InvalidOperationException("GetDefault returned null; display.json type is None?");

Console.WriteLine($"Display: {display.Width}x{display.Height}");
display.Enable();
Console.WriteLine($"IsEnabled={display.IsEnabled}");

// -- 3. Initialize UI + wire up touch ----------------------------------------

UI.Initialize();
TouchBridge.Attach();

// MA35D1 ADC resistive touch reports raw ADC counts via InputTouch,
// not panel pixels. Measured on the EPN256 1024x600 dev board by
// tapping each corner: useful X range is ~210..3950 (inverted - raw
// high = screen left), useful Y range is ~290..3660 (normal). We
// linearly map each axis into panel coordinates so LVGL hit-tests
// land where the finger is.
const int rawXMin = 210, rawXMax = 3950;
const int rawYMin = 290, rawYMax = 3660;

// Swipes, in panel pixels (see the SwipeTracker class at the bottom of the
// file). The handler below feeds it touch events on the touch driver's
// thread; the animation timer (section 4) collects finished swipes on the UI
// thread and decides whether each one was a shot.
//
// The resistive panel can briefly report the finger lifting mid-slide when
// the pressure wavers, so a press within SwipeJoinGapMs of a release counts
// as the same swipe rather than a new one.
const int SwipeJoinGapMs = 120;
var swipes = new SwipeTracker(SwipeJoinGapMs);

using var touch = Touch.GetDefault();
if (touch == null) {
    Console.WriteLine("No touchscreen found under /dev/input - touch input disabled.");
}
else {
    Console.WriteLine($"Touch: {touch.Width}x{touch.Height} raw units, connected={touch.Connected}");
    // A Released event carries no points, so the last position seen while the
    // finger was down is what the swipe ends on.
    int lastX = 0, lastY = 0;
    touch.OnChangedEvent += (_, e) => {
        bool down = e.EventType != TouchEventType.Released;
        if (down) {
            if (e.Points.Length == 0) return;
            lastX = Math.Clamp((int)((long)(rawXMax - e.Points[0].X) * Width  / (rawXMax - rawXMin)), 0, Width  - 1);
            lastY = Math.Clamp((int)((long)(e.Points[0].Y - rawYMin) * Height / (rawYMax - rawYMin)), 0, Height - 1);
            TouchBridge.Feed(lastX, lastY, true);
            swipes.OnPoint(lastX, lastY, pressed: e.EventType == TouchEventType.Pressed, Environment.TickCount64);
        }
        else {
            TouchBridge.Feed(lastX, lastY, false);
            swipes.OnReleased();
        }
    };
}

// -- 4. Build the screen ------------------------------------------------------

var screen = new Screen();
screen.BackgroundColor = 0x101418;  // near-black fallback behind the wallpaper

// Billiards table wallpaper: a 1024x600 JPEG under Resources/. The csproj
// copies that folder to the output, F5 deploys it next to the program, and
// AppContext.BaseDirectory finds it at runtime. Image.Load decodes JPEG, PNG,
// BMP, GIF and TGA; SetBackgroundImage converts the decoded pixels to the
// RGB565 the panel wants.
//
// The playing surface: the felt inside the wooden rails and green cushions,
// measured from the table image (where the cushions' inner edges meet the
// felt). The rails aren't the same width on every side, so each edge has its
// own value. Balls stay inside this box. Re-measure these if the table image
// changes.
const int FeltLeft = 53, FeltRight = 970;
const int FeltTop  = 49, FeltBottom = 538;
var bgBytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Resources", "billiards.jpg"));
var background = Image.Load(bgBytes);
screen.SetBackgroundImage(background);

// Balls on the felt (see the Ball class at the bottom of the file). The
// white cue ball moves when you touch it; the numbered balls sit still until
// another ball hits them, then the two exchange momentum like billiard balls
// (equal mass, elastic), and friction brings everything to rest after a few
// seconds. Every pair of balls can collide. A UiTimer
// moves them every ~16 ms (~60 fps) on the UI thread, so widget updates are
// safe (LVGL is single-threaded, LV_USE_OS=LV_OS_NONE).
//
// Solids 1-8 use these colors; stripes 9-15 are white with the same color
// band as the solid eight below them (9 = 1, 10 = 2, ... 15 = 7).
const int R = 24;
const uint White = 0xFFFFFF;
uint[] solidColors = [
    0xFFCC00,  // 1 yellow
    0x0033CC,  // 2 blue
    0xCC0000,  // 3 red
    0x4B1478,  // 4 dark purple
    0xFF6600,  // 5 orange
    0x008833,  // 6 green
    0x800000,  // 7 maroon
    0x000000,  // 8 black
];

// The rack: a triangle of 15 balls on the left of the table, apex (the
// 1-ball) pointing right at the cue ball. Laid out like a real 8-ball rack:
// the 8 in the middle of the third row, and one solid and one stripe in the
// back corners. Rows are listed apex first.
int[][] rack = [
    [1],
    [9, 2],
    [10, 8, 3],
    [4, 11, 5, 12],
    [6, 13, 7, 14, 15],
];
const float RackBackX   = 200;         // center X of the back (5-ball) row: ~120 px of felt behind it,
                                       // apex lands at x ~370
const float RackCenterY = (FeltTop + FeltBottom) / 2f; // rack is centered vertically on the felt
const float Spacing     = 2 * R + 1;   // center-to-center distance; the 1 px gap keeps
                                       // neighbors from counting as a collision at rest
float rowStep = Spacing * MathF.Sqrt(3) / 2;  // horizontal distance between rows of a tight triangle

// Cue ball on the right, level with the apex. It sits still until you touch
// it (see ShootCue below). If it's pocketed, it comes back here (see
// RespotCue below).
const float CueStartX = 780 - R, CueStartY = RackCenterY - R;  // top-left corner
var cue = new Ball(screen, R, x: CueStartX, y: CueStartY, color: White);
cue.SetVisible(false);  // the table starts empty; rackTimer shows the cue ball after the rack
var balls = new List<Ball> { cue };  // the rack balls join as rackTimer creates them

// Where each ball goes (center), in order: rack apex first, row by row, then the cue ball (N = 0).
var rackSpots = new Queue<(int N, float X, float Y)>();
for (int row = 0; row < rack.Length; row++) {
    float cx = RackBackX + (rack.Length - 1 - row) * rowStep;
    for (int i = 0; i < rack[row].Length; i++)
        rackSpots.Enqueue((rack[row][i], cx, RackCenterY + (i - row / 2f) * Spacing));
}
rackSpots.Enqueue((0, CueStartX + R, CueStartY + R));

const int RackBuildDelayMs = 150;  // between balls; about 2.5 s for the full table when not debugging
Ball? lastCreated = null;          // what the previous tick put on the table; null = table still empty
bool emptyTableDrawn = false, rackDone = false;
using var rackTimer = new UiTimer(RackBuildDelayMs, () => {
    if (rackDone) return;
    if (!emptyTableDrawn) { emptyTableDrawn = true; return; }  // tick 1: 
 
    
    
    Console.WriteLine(lastCreated == null ? "" : $"rack: ball{lastCreated.Name,3}");



    if (!rackSpots.TryDequeue(out var spot)) {
        rackDone = true;
        Console.WriteLine("rack: complete, ready to break");
        return;
    }
    if (spot.N == 0) {  // the cue ball already exists (ShootCue etc. hold on to it), so just show it
        cue.SetVisible(true);
        lastCreated = cue;
        return;
    }
    bool striped = spot.N > 8;
    lastCreated = new Ball(screen, R, x: spot.X - R, y: spot.Y - R,
        color:  striped ? White : solidColors[spot.N - 1],
        number: spot.N.ToString(),
        stripe: striped ? solidColors[spot.N - 9] : null);
    balls.Add(lastCreated);
});

var fpsLabel = new Label(screen) {
    X = Width - 160, Y = Height - 40,
    Width = 140, Height = 30,
    FontSize = 16,
    Color = 0x888888,
    TextAlignment = TextAlignment.Right,
    Text = "...",
};

long frames = 0;
var sw = System.Diagnostics.Stopwatch.StartNew();

// Limits for a ball's top-left corner that keep the whole ball on the felt
// (see FeltLeft etc. above); balls bounce off the cushions there.
const int MinX = FeltLeft,           MinY = FeltTop;
const int MaxX = FeltRight - 2 * R,  MaxY = FeltBottom - 2 * R;

// Pockets. The cushions don't run the full length of each side: they stop
// at the six pockets. These are the stretches the cushions do cover,
// measured from the table image (felt coordinates, px). A ball that reaches
// the edge of the felt where there's no cushion drops into the pocket and
// disappears. PocketJaw makes the openings a little more forgiving: a ball
// whose center is within that many px of a cushion's end goes in too, as
// its edge hangs over the pocket.
(int Start, int End)[] topCushions    = [(96, 475), (548, 928)];  // along x
(int Start, int End)[] bottomCushions = [(95, 475), (548, 931)];  // along x
(int Start, int End)[] sideCushions   = [(85, 505)];              // along y, left and right alike
const int PocketJaw = 6;

bool OnCushion(float center, (int Start, int End)[] cushions) =>
    cushions.Any(c => center >= c.Start + PocketJaw && center <= c.End - PocketJaw);

bool FallsInPocket(Ball b, Wall touching) {
    float cx = b.X + R, cy = b.Y + R;
    return (touching.HasFlag(Wall.Top)    && !OnCushion(cx, topCushions))
        || (touching.HasFlag(Wall.Bottom) && !OnCushion(cx, bottomCushions))
        || (touching.HasFlag(Wall.Left)   && !OnCushion(cy, sideCushions))
        || (touching.HasFlag(Wall.Right)  && !OnCushion(cy, sideCushions));
}

// A pocketed cue ball comes back once the table has stopped moving: on its
// starting spot, or if a ball is sitting there, the nearest free spot
// straight above or below it.
void RespotCue() {
    float y = CueStartY;
    for (int k = 0; k < 12; k++) {
        float candidate = Math.Clamp(CueStartY + (k + 1) / 2 * (k % 2 == 1 ? 1 : -1) * (2 * R + 2), MinY, MaxY);
        bool free = !balls.Any(b => b != cue && !b.Pocketed &&
            MathF.Pow(b.X - CueStartX, 2) + MathF.Pow(b.Y - candidate, 2) < (2 * R + 1) * (2 * R + 1));
        if (free) { y = candidate; break; }
    }
    cue.Respot(CueStartX, y);
    Console.WriteLine($"cue ball back on the table at ({CueStartX + R:F0},{y + R:F0})");
}

// To shoot, put your finger on the cue ball, slide it the way you want the
// ball to go, and lift. The ball rolls in the direction of the slide (from
// where you pressed to where you lifted). Ignored:
//   - swipes that don't start on the cue ball (within TouchSlop px of it,
//     since the resistive panel's calibration is only approximate);
//   - slides shorter than MinSwipe px, e.g. a plain tap, which has no clear
//     direction;
//   - anything while a ball is still rolling: wait for the table to settle,
//     then take the next shot.
//
// Shot strength follows how fast you slide. SwipeTracker reports the
// finger's peak speed, measured over ~80 ms stretches of the slide (so a
// pause before the flick, or slowing down just before lifting, doesn't
// weaken the shot). The cue ball leaves at that speed times SpeedGain, then
// clamped to MinCueSpeed..MaxCueSpeed. A deliberate slide on this panel peaks
// around 1500 px/s and a hard flick around 3600, which is 24 to 58 px/frame
// raw, so the gain has to scale that down or every shot pins at the maximum
// and sliding faster stops making any difference.
//
// Every frame, friction takes Friction px/frame off each ball's speed
// (constant deceleration, like a ball rolling on felt). A ball at speed v
// rolls for v / Friction frames and v^2 / (2 * Friction) px: a 6 px/frame
// tap stops after ~2 s and 360 px, a full 18 px/frame flick after ~6 s and
// ~3200 px (bouncing off the rails).
const int   FrameMs     = 16;     // animation timer period
const float SpeedGain   = 0.3f;   // ball speed / finger speed
const float MinCueSpeed = 1.5f;   // px per frame
const float MaxCueSpeed = 18f;    // px per frame
const float Friction    = 0.05f;  // px per frame, per frame
const float TouchSlop   = 30f;    // px beyond the ball's edge; a fingertip on the ball's rim landed 40 px from center
const float MinSwipe    = 20f;    // px

// -- Ready LED ----------------------------------------------------------------
// The board's user LED reports the cue ball: solid while the table is at rest,
// blinking while the cue ball rolls, and faster the faster it rolls. The blink
// rate is proportional to the ball's speed, so the half-period is LedFastMs at
// MaxCueSpeed and grows as the ball slows, capped at LedSlowMs so a nearly
// stopped ball still reads as moving.
//
// EPN256.Gpio.Led is PJ14, wired active-low on this board, so PinValue.Low
// lights it. The board's own device tree leaves the pin unclaimed, so opening
// it here costs nothing at boot.
// The half-period is a whole number of FrameMs ticks, otherwise the toggle
// lands on alternating frames and the blink visibly jitters.
const int LedFastMs = 32;
const int LedSlowMs = 240;
using var gpio = new GpioController(new EPN256.Gpio.Driver());
gpio.OpenPin(EPN256.Gpio.Led, PinMode.Output);
void SetLed(bool on) => gpio.Write(EPN256.Gpio.Led, on ? PinValue.Low : PinValue.High);

var ledPhase = System.Diagnostics.Stopwatch.StartNew();
bool ledLit = true;
SetLed(true);

void UpdateReadyLed() {
    float speed = MathF.Sqrt(cue.Vx * cue.Vx + cue.Vy * cue.Vy);
    if (speed <= 0f) {
        if (!ledLit) { ledLit = true; SetLed(true); }
        ledPhase.Restart();
        return;
    }
    double halfPeriod = Math.Clamp(LedFastMs * MaxCueSpeed / speed, LedFastMs, LedSlowMs);
    if (ledPhase.Elapsed.TotalMilliseconds >= halfPeriod) {
        ledLit = !ledLit;
        SetLed(ledLit);
        ledPhase.Restart();
    }
}

// Every swipe is logged with the outcome, so if shots don't fire the Debug
// output shows why (where the touch landed vs. the ball, how far the finger
// slid, and how many press/move events the panel sent).
void ShootCue(Swipe s) {
    float ox = s.From.X - (cue.X + R), oy = s.From.Y - (cue.Y + R);
    float startDist = MathF.Sqrt(ox * ox + oy * oy);
    float dx = s.To.X - s.From.X, dy = s.To.Y - s.From.Y;
    float len = MathF.Sqrt(dx * dx + dy * dy);

    string outcome;
    if (!rackDone)
        outcome = "ignored: rack still being set up";
    else if (balls.Any(b => b.IsMoving))
        outcome = "ignored: balls still rolling";
    else if (startDist > R + TouchSlop)
        outcome = $"ignored: started {startDist:F0} px from the cue ball's center (limit {R + TouchSlop:F0})";
    else if (len < MinSwipe)
        outcome = $"ignored: slide too short (limit {MinSwipe:F0} px)";
    else {
        float power = Math.Clamp(s.PeakSpeed * FrameMs * SpeedGain, MinCueSpeed, MaxCueSpeed);
        cue.Vx = dx / len * power;
        cue.Vy = dy / len * power;
        outcome = $"SHOT at {power:F1} px/frame";
    }
    Console.WriteLine($"swipe ({s.From.X},{s.From.Y}) -> ({s.To.X},{s.To.Y}), {len:F0} px in {s.DurationMs:F0} ms, " +
                      $"peak {s.PeakSpeed * 1000:F0} px/s, {s.Presses} press / {s.Moves} move events, " +
                      $"cue ball at ({cue.X + R:F0},{cue.Y + R:F0}): {outcome}");
}

using var animTimer = new UiTimer(FrameMs, () => {
    if (cue.Pocketed && !balls.Any(b => b.IsMoving))
        RespotCue();

    if (swipes.TakeFinished() is { } swipe)
        ShootCue(swipe);

    foreach (var ball in balls) {
        if (ball.Pocketed) continue;
        var touching = ball.Step(MinX, MinY, MaxX, MaxY);
        if (touching != Wall.None && FallsInPocket(ball, touching)) {
            ball.Pocket();
            int left = balls.Count(b => b != cue && !b.Pocketed);
            Console.WriteLine($"ball {ball.Name} pocketed at ({ball.X + R:F0},{ball.Y + R:F0}), {left} numbered balls left");
        }
    }
    for (int i = 0; i < balls.Count; i++)
        for (int j = i + 1; j < balls.Count; j++)
            if (!balls[i].Pocketed && !balls[j].Pocketed)
                Ball.Collide(balls[i], balls[j]);
    foreach (var ball in balls) {
        if (ball.Pocketed) continue;
        ball.ApplyFriction(Friction);
        ball.Draw();
    }

    UpdateReadyLed();

    frames++;
    if (sw.ElapsedMilliseconds >= 1000) {
        fpsLabel.Text = $"{frames * 1000.0 / sw.ElapsedMilliseconds:F0} fps";
        frames = 0;
        sw.Restart();
    }
});

screen.Load();
Console.WriteLine("entering UI.Run");
UI.Run();
Console.WriteLine("Stop requested - shutting down.");

// A filled circle on the screen with a position and velocity. Endpoint.UI
// has no primitive "DrawCircle"; a Border with CornerRadius = half its side
// renders a true filled circle in LVGL. X/Y are the top-left corner in panel
// pixels, matching the widget's coordinates.
//
// A numbered ball (e.g. the 8-ball) also gets a white spot with the number
// in black on top, like a real pool ball. The spot and number are siblings
// on the same parent (not children of the ball) and are moved along with it
// in Draw, so they center exactly without depending on how LVGL offsets
// children inside a Border (padding / border width).
//
// A striped ball (9-15) is a ball of the base color with a colored band
// across its middle. Endpoint.UI can't clip a widget to a circle, so the
// band is the overlap of three centered shapes, each sized to stay inside
// the circle:
//   - a plain rectangle as wide as the circle is at the band's top and
//     bottom edges, giving the band its straight top and bottom;
//   - a full-width "pill" (rounded rect, radius = half its height) whose
//     round ends touch the ball's edge at its widest point;
//   - a slightly narrower rounded rect with smaller corners that fills in
//     where the circle's edge sits between the other two.
// Together they leave under 1 px of white along the band's curved sides and
// never spill outside the ball.
//
// Widget count matters: LVGL's memory pool inside libendpoint is fixed, and
// the program died partway through building the rack when each stripe was
// 14 thin strips (~145 widgets in all). Ball.WidgetCount tracks the total.
sealed class Ball {
    const int SpotRadius = 13;
    const int NumberFontSize = 20;    // one of the fonts libendpoint ships (14/20/32)
    const int NumberLineHeight = 22;  // LVGL Montserrat 20 line height, for vertical centering
    const int StripeHalfHeight = 14;  // band is 28 px tall on a 48 px ball; just wider than the spot

    public static int WidgetCount { get; private set; }

    public string Name { get; }  // the number, or "cue" for the cue ball; used in log lines

    readonly Border view;
    readonly Border? spot;
    readonly Label? numberLabel;
    readonly (Border View, int Dx, int Dy)[] stripeParts = [];  // offsets from the ball's top-left
    public readonly int Radius;
    public float X, Y;
    public float Vx, Vy;

    public Ball(Widget parent, int radius, float x, float y, uint color, string? number = null, uint? stripe = null) {
        //Thread.Sleep(500);
        Radius = radius;
        Name = number ?? "cue";
        X = x; Y = y;
        view = new Border(parent) {
            Width = radius * 2, Height = radius * 2,
            CornerRadius = radius,
        };
        view.BackgroundColor = color;
        WidgetCount++;

        // Created after the ball and before the spot so they draw on top of
        // the ball and underneath the spot and number.
        if (stripe is uint stripeColor) {
            int h = StripeHalfHeight;
            int chordHalf = (int)MathF.Sqrt(radius * radius - h * h);  // circle's half-width at the band edge
            int innerHalf = radius - 1;
            // Smallest corner radius that keeps the narrower rect inside the circle.
            int innerCorner = 1;
            while (MathF.Sqrt((innerHalf - innerCorner) * (innerHalf - innerCorner) + (h - innerCorner) * (h - innerCorner)) + innerCorner > radius)
                innerCorner++;

            stripeParts = [
                MakeStripePart(parent, stripeColor, radius, chordHalf, h, corner: 0),
                MakeStripePart(parent, stripeColor, radius, radius,    h, corner: h),
                MakeStripePart(parent, stripeColor, radius, innerHalf, h, corner: innerCorner),
            ];
        }

        if (number != null) {
            spot = new Border(parent) {
                Width = SpotRadius * 2, Height = SpotRadius * 2,
                CornerRadius = SpotRadius,
            };
            spot.BackgroundColor = 0xFFFFFF;
            WidgetCount++;
            numberLabel = new Label(parent) {
                Width = SpotRadius * 2, Height = NumberLineHeight,
                FontSize = NumberFontSize,
                Color = 0x000000,
                TextAlignment = TextAlignment.Center,
                Text = number,
            };
            WidgetCount++;
        }

        Draw();
    }

    // One piece of a stripe: a rounded rect halfWidth*2 wide and halfHeight*2
    // tall, centered on the ball. Thickness = 0 because Border's default grey
    // outline would otherwise cover thin pieces and tint the band's edges.
    static (Border, int, int) MakeStripePart(Widget parent, uint color, int radius, int halfWidth, int halfHeight, int corner) {
        var part = new Border(parent) {
            Width = halfWidth * 2, Height = halfHeight * 2,
            CornerRadius = corner,
            Thickness = 0,
        };
        part.BackgroundColor = color;
        WidgetCount++;
        return (part, radius - halfWidth, radius - halfHeight);
    }

    // Advance one frame and bounce off the rectangle [minX..maxX] x [minY..maxY]
    // (limits for the top-left corner). Clamping, and forcing the velocity to
    // point back inside, keeps a ball that a collision nudged past the edge
    // from getting stuck flipping direction every frame.
    //
    // Returns the walls the ball is touching after the move (including one it
    // is rolling along), so the caller can check whether that spot is a
    // pocket rather than a cushion.
    public Wall Step(int minX, int minY, int maxX, int maxY) {
        X += Vx; Y += Vy;
        var touching = Wall.None;
        if (X <= minX) { X = minX; Vx =  Math.Abs(Vx); touching |= Wall.Left; }
        else if (X >= maxX) { X = maxX; Vx = -Math.Abs(Vx); touching |= Wall.Right; }
        if (Y <= minY) { Y = minY; Vy =  Math.Abs(Vy); touching |= Wall.Top; }
        else if (Y >= maxY) { Y = maxY; Vy = -Math.Abs(Vy); touching |= Wall.Bottom; }
        return touching;
    }

    public bool IsMoving => Vx != 0 || Vy != 0;

    // A pocketed ball is hidden and takes no further part in the game (the
    // caller skips it) until Respot puts it back.
    public bool Pocketed { get; private set; }

    public void Pocket() {
        Pocketed = true;
        Vx = Vy = 0;
        SetVisible(false);
    }

    public void Respot(float x, float y) {
        X = x; Y = y;
        Vx = Vy = 0;
        Pocketed = false;
        Draw();
        SetVisible(true);
    }

    public void SetVisible(bool visible) {
        view.Visible = visible;
        foreach (var (part, _, _) in stripeParts)
            part.Visible = visible;
        if (spot != null) spot.Visible = visible;
        if (numberLabel != null) numberLabel.Visible = visible;
    }

    // Slow the ball by `decel` px/frame without changing its direction, and
    // stop it dead once it's slower than that, so it can't creep forever.
    public void ApplyFriction(float decel) {
        float speed = MathF.Sqrt(Vx * Vx + Vy * Vy);
        if (speed <= decel) { Vx = Vy = 0; return; }
        float scale = (speed - decel) / speed;
        Vx *= scale; Vy *= scale;
    }

    public void Draw() {
        view.X = (int)X;
        view.Y = (int)Y;

        foreach (var (part, dx, dy) in stripeParts) {
            part.X = (int)X + dx;
            part.Y = (int)Y + dy;
        }

        if (spot != null && numberLabel != null) {
            int cx = (int)X + Radius, cy = (int)Y + Radius;
            spot.X = cx - SpotRadius;
            spot.Y = cy - SpotRadius;
            numberLabel.X = cx - SpotRadius;
            numberLabel.Y = cy - NumberLineHeight / 2;
        }
    }

    // Elastic collision between two equal-mass balls: if they overlap and are
    // moving toward each other, swap their velocity components along the line
    // between their centers, then push them apart so they don't stay
    // overlapped. A head-on hit stops the striking ball and sends the struck
    // one off at the same speed, as on a real table.
    public static void Collide(Ball a, Ball b) {
        float nx = (b.X + b.Radius) - (a.X + a.Radius);
        float ny = (b.Y + b.Radius) - (a.Y + a.Radius);
        float minDist = a.Radius + b.Radius;
        float dist2 = nx * nx + ny * ny;
        if (dist2 >= minDist * minDist || dist2 == 0) return;

        float dist = MathF.Sqrt(dist2);
        nx /= dist; ny /= dist;

        float approach = (a.Vx - b.Vx) * nx + (a.Vy - b.Vy) * ny;
        if (approach > 0) {
            a.Vx -= approach * nx; a.Vy -= approach * ny;
            b.Vx += approach * nx; b.Vy += approach * ny;
        }

        float push = (minDist - dist) / 2;
        a.X -= nx * push; a.Y -= ny * push;
        b.X += nx * push; b.Y += ny * push;
    }
}

// Which edges of the felt a ball is touching (see Ball.Step).
[Flags]
enum Wall { None = 0, Left = 1, Right = 2, Top = 4, Bottom = 8 }

// A finished swipe: where the finger first touched, where it last was before
// lifting, how long it took, the finger's peak speed in px per ms (see
// SwipeTracker.PeakSpeed), and how many press / move events made it up
// (more than one press means the panel reported the finger lifting and
// landing again mid-slide).
readonly record struct Swipe((int X, int Y) From, (int X, int Y) To, double DurationMs, float PeakSpeed, int Presses, int Moves);

// Turns the touch driver's press / move / release events into whole swipes.
// OnPoint and OnReleased are called on the touch thread, TakeFinished on the
// UI thread; everything is guarded by one lock.
//
// A swipe only counts as finished once the finger has stayed up for
// joinGapMs. If it lands again sooner, that's treated as the same swipe
// continuing, which papers over the resistive panel's brief false releases.
sealed class SwipeTracker(long joinGapMs) {
    // Speed is measured over stretches of up to SpeedWindowMs. Stretches
    // shorter than MinSpanMs are skipped: over a few ms the panel's jitter
    // of a few px would read as a burst of speed.
    const double SpeedWindowMs = 80;
    const double MinSpanMs = 30;

    readonly object gate = new();
    readonly List<(double T, int X, int Y)> points = [];  // empty when no swipe is in progress
    bool fingerDown;
    long releasedAtMs;
    int presses, moves;

    // timeMs: when the touch event arrived, for speed.
    public void OnPoint(int x, int y, bool pressed, double timeMs) {
        lock (gate) {
            if (points.Count == 0)
                presses = moves = 0;
            if (pressed) presses++; else moves++;
            points.Add((timeMs, x, y));
            fingerDown = true;
        }
    }

    public void OnReleased() {
        lock (gate) {
            fingerDown = false;
            releasedAtMs = Environment.TickCount64;
        }
    }

    // The swipe, once the finger has been up for joinGapMs; otherwise null.
    public Swipe? TakeFinished() {
        lock (gate) {
            if (points.Count == 0 || fingerDown || Environment.TickCount64 - releasedAtMs < joinGapMs)
                return null;
            var first = points[0];
            var last = points[^1];
            var swipe = new Swipe((first.X, first.Y), (last.X, last.Y), last.T - first.T, PeakSpeed(points), presses, moves);
            points.Clear();
            return swipe;
        }
    }

    // The fastest the finger moved over any ~SpeedWindowMs stretch of the
    // swipe, in px per ms. For a swipe too quick to have such a stretch, the
    // average over the whole swipe (taking at least one frame).
    static float PeakSpeed(List<(double T, int X, int Y)> pts) {
        float best = 0;
        int j = 0;
        for (int i = 1; i < pts.Count; i++) {
            while (pts[i].T - pts[j].T > SpeedWindowMs) j++;
            double span = pts[i].T - pts[j].T;
            if (span < MinSpanMs) continue;
            best = MathF.Max(best, Distance(pts[j], pts[i]) / (float)span);
        }
        if (best == 0 && pts.Count >= 2)
            best = Distance(pts[0], pts[^1]) / (float)Math.Max(pts[^1].T - pts[0].T, 16);
        return best;
    }

    static float Distance((double T, int X, int Y) a, (double T, int X, int Y) b) =>
        MathF.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
}
