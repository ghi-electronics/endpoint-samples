// billiards_game_cpp - exercises the Endpoint display + touch + UI
// stack on the EPN256 (Nuvoton MA35D1) 1024x600 panel. Everything draws
// through the Endpoint UI library (LVGL under the hood).
//
// What this does, in order:
//   1. Writes /config/display.json via DisplayController::SetSettings:
//      type = Parallel, backlight on PK7 (active-high),
//      1024x600 with the panel's timings (ma35-drm actually reads them
//      from the device tree; the fields here just describe the visible
//      area that LVGL uses for the canvas).
//   2. Calls DisplayController::Enable(), which modprobes ma35-drm and
//      raises the backlight.
//   3. UI::Initialize() wires LVGL to the framebuffer. TouchBridge::Attach()
//      sets up the input layer; Touch::GetDefault() opens the panel's
//      resistive touchscreen (ma35d1-ts kernel driver, always on).
//   4. Builds a screen: the billiards table wallpaper, a full set of 16
//      balls animated by a UiTimer (solids 1-8 and stripes 9-15 racked in
//      a triangle on the left, and a white cue ball on the right that
//      you shoot by sliding your finger off it; friction stops every
//      ball after a few seconds, then you touch the cue ball again for the
//      next shot) and an FPS readout.
//   5. Blinks the board's user LED in time with the cue ball: solid when the
//      table is at rest, blinking faster the faster the ball rolls.

#include <limits.h>
#include <unistd.h>

#include <algorithm>
#include <chrono>
#include <cmath>
#include <csignal>
#include <cstdint>
#include <cstdio>
#include <deque>
#include <fstream>
#include <iterator>
#include <memory>
#include <mutex>
#include <optional>
#include <string>
#include <vector>

#include <endpoint.h>

using namespace GHIElectronics::Endpoint::Core;
using namespace GHIElectronics::Endpoint::Devices::Display;
using namespace GHIElectronics::Endpoint::Devices::Gpio;
using namespace GHIElectronics::Endpoint::UI;

namespace Devices = GHIElectronics::Endpoint::Devices;
namespace Drawing = GHIElectronics::Endpoint::Drawing;
namespace UI = GHIElectronics::Endpoint::UI;

namespace {

constexpr int Width = 1024;
constexpr int Height = 600;

// Milliseconds since the program started. The same monotonic clock for swipe
// speed and the LED blink phase, so neither is disturbed by the board's
// clock being set.
double NowMs() {
    static const auto start = std::chrono::steady_clock::now();
    return std::chrono::duration<double, std::milli>(std::chrono::steady_clock::now() - start).count();
}

// SIGINT / SIGTERM only set this; the animation timer sees it on the UI
// thread and calls UI::Stop() there, so nothing LVGL-related runs in a
// signal handler.
volatile std::sig_atomic_t stopRequested = 0;
void OnSignal(int) { stopRequested = 1; }

// The folder the program was deployed into, so Resources/ is found whatever
// the working folder is. Matches .NET's AppContext.BaseDirectory and
// Python's os.path.dirname(os.path.abspath(__file__)).
std::string BaseDirectory() {
    char buffer[PATH_MAX];
    ssize_t length = readlink("/proc/self/exe", buffer, sizeof buffer - 1);
    if (length <= 0) {
        return ".";
    }
    buffer[length] = '\0';
    std::string path(buffer);
    size_t slash = path.find_last_of('/');
    return slash == std::string::npos ? std::string(".") : path.substr(0, slash);
}

std::vector<uint8_t> ReadAllBytes(const std::string& path) {
    std::ifstream file(path, std::ios::binary);
    if (!file) {
        throw IOException("Cannot open " + path);
    }
    return std::vector<uint8_t>(std::istreambuf_iterator<char>(file), std::istreambuf_iterator<char>());
}

// The playing surface: the felt inside the wooden rails and green cushions,
// measured from the table image (where the cushions' inner edges meet the
// felt). The rails aren't the same width on every side, so each edge has its
// own value. Balls stay inside this box. Re-measure these if the table image
// changes.
constexpr int FeltLeft = 53, FeltRight = 970;
constexpr int FeltTop = 49, FeltBottom = 538;

constexpr int R = 24;
constexpr uint32_t White = 0xFFFFFF;

// Which edges of the felt a ball is touching (see Ball::Step).
enum Wall : int { WallNone = 0, WallLeft = 1, WallRight = 2, WallTop = 4, WallBottom = 8 };

// Limits for a ball's top-left corner that keep the whole ball on the felt
// (see FeltLeft etc. above); balls bounce off the cushions there.
constexpr int MinX = FeltLeft, MinY = FeltTop;
constexpr int MaxX = FeltRight - 2 * R, MaxY = FeltBottom - 2 * R;

// Pockets. The cushions don't run the full length of each side: they stop
// at the six pockets. These are the stretches the cushions do cover,
// measured from the table image (felt coordinates, px). A ball that reaches
// the edge of the felt where there's no cushion drops into the pocket and
// disappears. PocketJaw makes the openings a little more forgiving: a ball
// whose center is within that many px of a cushion's end goes in too, as
// its edge hangs over the pocket.
struct Span {
    int Start;
    int End;
};
constexpr Span TopCushions[] = {{96, 475}, {548, 928}};     // along x
constexpr Span BottomCushions[] = {{95, 475}, {548, 931}};  // along x
constexpr Span SideCushions[] = {{85, 505}};                // along y, left and right alike
constexpr int PocketJaw = 6;

template <size_t N>
bool OnCushion(float center, const Span (&cushions)[N]) {
    for (const Span& cushion : cushions) {
        if (center >= cushion.Start + PocketJaw && center <= cushion.End - PocketJaw) {
            return true;
        }
    }
    return false;
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
constexpr int FrameMs = 16;        // animation timer period
constexpr float SpeedGain = 0.3f;  // ball speed / finger speed
constexpr float MinCueSpeed = 1.5f;
constexpr float MaxCueSpeed = 18.0f;
constexpr float Friction = 0.05f;   // px per frame, per frame
constexpr float TouchSlop = 30.0f;  // px beyond the ball's edge; a fingertip on the ball's rim landed 40 px from center
constexpr float MinSwipe = 20.0f;

// A filled circle on the screen with a position and velocity. Endpoint's UI
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
// across its middle. The UI library can't clip a widget to a circle, so the
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
// 14 thin strips (~145 widgets in all). Ball::WidgetCount tracks the total.
class Ball {
public:
    static inline int WidgetCount = 0;

    Ball(const Widget& parent, int radius, float x, float y, uint32_t color,
         std::optional<std::string> number = std::nullopt,
         std::optional<uint32_t> stripe = std::nullopt)
        : Name(number.value_or("cue")), Radius(radius), X(x), Y(y) {
        this->view = std::make_unique<Border>(parent);
        this->view->Width(radius * 2);
        this->view->Height(radius * 2);
        this->view->CornerRadius(radius);
        this->view->SetBackgroundColor(color);
        WidgetCount++;

        // Created after the ball and before the spot so they draw on top of
        // the ball and underneath the spot and number.
        if (stripe) {
            int h = StripeHalfHeight;
            // Circle's half-width at the band edge.
            int chordHalf = static_cast<int>(std::sqrt(static_cast<float>(radius * radius - h * h)));
            int innerHalf = radius - 1;
            // Smallest corner radius that keeps the narrower rect inside the circle.
            int innerCorner = 1;
            while (std::sqrt(static_cast<float>((innerHalf - innerCorner) * (innerHalf - innerCorner) +
                                                (h - innerCorner) * (h - innerCorner))) +
                       innerCorner >
                   radius) {
                innerCorner++;
            }

            this->stripeParts.push_back(MakeStripePart(parent, *stripe, radius, chordHalf, h, 0));
            this->stripeParts.push_back(MakeStripePart(parent, *stripe, radius, radius, h, h));
            this->stripeParts.push_back(MakeStripePart(parent, *stripe, radius, innerHalf, h, innerCorner));
        }

        if (number) {
            this->spot = std::make_unique<Border>(parent);
            this->spot->Width(SpotRadius * 2);
            this->spot->Height(SpotRadius * 2);
            this->spot->CornerRadius(SpotRadius);
            this->spot->SetBackgroundColor(0xFFFFFF);
            WidgetCount++;

            this->numberLabel = std::make_unique<Label>(parent);
            this->numberLabel->Width(SpotRadius * 2);
            this->numberLabel->Height(NumberLineHeight);
            this->numberLabel->FontSize(NumberFontSize);
            this->numberLabel->Color(0x000000);
            this->numberLabel->TextAlignment(TextAlignment::Center);
            this->numberLabel->Text(*number);
            WidgetCount++;
        }

        this->Draw();
    }

    const std::string Name;  // the number, or "cue" for the cue ball; used in log lines
    const int Radius;
    float X, Y;
    float Vx = 0, Vy = 0;

    // Advance one frame and bounce off the rectangle [minX..maxX] x [minY..maxY]
    // (limits for the top-left corner). Clamping, and forcing the velocity to
    // point back inside, keeps a ball that a collision nudged past the edge
    // from getting stuck flipping direction every frame.
    //
    // Returns the walls the ball is touching after the move (including one it
    // is rolling along), so the caller can check whether that spot is a
    // pocket rather than a cushion.
    int Step(int minX, int minY, int maxX, int maxY) {
        this->X += this->Vx;
        this->Y += this->Vy;
        int touching = WallNone;
        if (this->X <= minX) {
            this->X = static_cast<float>(minX);
            this->Vx = std::fabs(this->Vx);
            touching |= WallLeft;
        }
        else if (this->X >= maxX) {
            this->X = static_cast<float>(maxX);
            this->Vx = -std::fabs(this->Vx);
            touching |= WallRight;
        }
        if (this->Y <= minY) {
            this->Y = static_cast<float>(minY);
            this->Vy = std::fabs(this->Vy);
            touching |= WallTop;
        }
        else if (this->Y >= maxY) {
            this->Y = static_cast<float>(maxY);
            this->Vy = -std::fabs(this->Vy);
            touching |= WallBottom;
        }
        return touching;
    }

    bool IsMoving() const { return this->Vx != 0 || this->Vy != 0; }

    // A pocketed ball is hidden and takes no further part in the game (the
    // caller skips it) until Respot puts it back.
    bool Pocketed() const { return this->pocketed; }

    void Pocket() {
        this->pocketed = true;
        this->Vx = this->Vy = 0;
        this->SetVisible(false);
    }

    void Respot(float x, float y) {
        this->X = x;
        this->Y = y;
        this->Vx = this->Vy = 0;
        this->pocketed = false;
        this->Draw();
        this->SetVisible(true);
    }

    void SetVisible(bool visible) {
        this->view->Visible(visible);
        for (StripePart& part : this->stripeParts) {
            part.View->Visible(visible);
        }
        if (this->spot) {
            this->spot->Visible(visible);
        }
        if (this->numberLabel) {
            this->numberLabel->Visible(visible);
        }
    }

    // Slow the ball by `decel` px/frame without changing its direction, and
    // stop it dead once it's slower than that, so it can't creep forever.
    void ApplyFriction(float decel) {
        float speed = std::sqrt(this->Vx * this->Vx + this->Vy * this->Vy);
        if (speed <= decel) {
            this->Vx = this->Vy = 0;
            return;
        }
        float scale = (speed - decel) / speed;
        this->Vx *= scale;
        this->Vy *= scale;
    }

    void Draw() {
        this->view->X(static_cast<int>(this->X));
        this->view->Y(static_cast<int>(this->Y));

        for (StripePart& part : this->stripeParts) {
            part.View->X(static_cast<int>(this->X) + part.Dx);
            part.View->Y(static_cast<int>(this->Y) + part.Dy);
        }

        if (this->spot && this->numberLabel) {
            int cx = static_cast<int>(this->X) + this->Radius;
            int cy = static_cast<int>(this->Y) + this->Radius;
            this->spot->X(cx - SpotRadius);
            this->spot->Y(cy - SpotRadius);
            this->numberLabel->X(cx - SpotRadius);
            this->numberLabel->Y(cy - NumberLineHeight / 2);
        }
    }

    // Elastic collision between two equal-mass balls: if they overlap and are
    // moving toward each other, swap their velocity components along the line
    // between their centers, then push them apart so they don't stay
    // overlapped. A head-on hit stops the striking ball and sends the struck
    // one off at the same speed, as on a real table.
    static void Collide(Ball& a, Ball& b) {
        float nx = (b.X + b.Radius) - (a.X + a.Radius);
        float ny = (b.Y + b.Radius) - (a.Y + a.Radius);
        float minDist = static_cast<float>(a.Radius + b.Radius);
        float dist2 = nx * nx + ny * ny;
        if (dist2 >= minDist * minDist || dist2 == 0) {
            return;
        }

        float dist = std::sqrt(dist2);
        nx /= dist;
        ny /= dist;

        float approach = (a.Vx - b.Vx) * nx + (a.Vy - b.Vy) * ny;
        if (approach > 0) {
            a.Vx -= approach * nx;
            a.Vy -= approach * ny;
            b.Vx += approach * nx;
            b.Vy += approach * ny;
        }

        float push = (minDist - dist) / 2;
        a.X -= nx * push;
        a.Y -= ny * push;
        b.X += nx * push;
        b.Y += ny * push;
    }

private:
    static constexpr int SpotRadius = 13;
    static constexpr int NumberFontSize = 20;    // one of the fonts libendpoint ships (14/20/32)
    static constexpr int NumberLineHeight = 22;  // LVGL Montserrat 20 line height, for vertical centering
    static constexpr int StripeHalfHeight = 14;  // band is 28 px tall on a 48 px ball; just wider than the spot

    struct StripePart {
        std::unique_ptr<Border> View;
        int Dx;  // offsets from the ball's top-left
        int Dy;
    };

    // One piece of a stripe: a rounded rect halfWidth*2 wide and halfHeight*2
    // tall, centered on the ball. Thickness = 0 because Border's default grey
    // outline would otherwise cover thin pieces and tint the band's edges.
    static StripePart MakeStripePart(const Widget& parent, uint32_t color, int radius,
                                     int halfWidth, int halfHeight, int corner) {
        auto part = std::make_unique<Border>(parent);
        part->Width(halfWidth * 2);
        part->Height(halfHeight * 2);
        part->CornerRadius(corner);
        part->Thickness(0);
        part->SetBackgroundColor(color);
        WidgetCount++;
        return StripePart{std::move(part), radius - halfWidth, radius - halfHeight};
    }

    std::unique_ptr<Border> view;
    std::unique_ptr<Border> spot;
    std::unique_ptr<Label> numberLabel;
    std::vector<StripePart> stripeParts;
    bool pocketed = false;
};

bool FallsInPocket(const Ball& ball, int touching) {
    float cx = ball.X + R, cy = ball.Y + R;
    return ((touching & WallTop) && !OnCushion(cx, TopCushions))
        || ((touching & WallBottom) && !OnCushion(cx, BottomCushions))
        || ((touching & WallLeft) && !OnCushion(cy, SideCushions))
        || ((touching & WallRight) && !OnCushion(cy, SideCushions));
}

// A finished swipe: where the finger first touched, where it last was before
// lifting, how long it took, the finger's peak speed in px per ms (see
// SwipeTracker::PeakSpeed), and how many press / move events made it up
// (more than one press means the panel reported the finger lifting and
// landing again mid-slide).
struct Swipe {
    int FromX, FromY;
    int ToX, ToY;
    double DurationMs;
    float PeakSpeed;
    int Presses, Moves;
};

// Turns the touch driver's press / move / release events into whole swipes.
// OnPoint and OnReleased are called on the touch thread, TakeFinished on the
// UI thread; everything is guarded by one lock.
//
// A swipe only counts as finished once the finger has stayed up for
// joinGapMs. If it lands again sooner, that's treated as the same swipe
// continuing, which papers over the resistive panel's brief false releases.
class SwipeTracker {
public:
    explicit SwipeTracker(double joinGapMs) : joinGapMs(joinGapMs) {}

    // timeMs: when the touch event arrived, for speed.
    void OnPoint(int x, int y, bool pressed, double timeMs) {
        std::lock_guard<std::mutex> lock(this->gate);
        if (this->points.empty()) {
            this->presses = this->moves = 0;
        }
        if (pressed) {
            this->presses++;
        }
        else {
            this->moves++;
        }
        this->points.push_back({timeMs, x, y});
        this->fingerDown = true;
    }

    void OnReleased() {
        std::lock_guard<std::mutex> lock(this->gate);
        this->fingerDown = false;
        this->releasedAtMs = NowMs();
    }

    // The swipe, once the finger has been up for joinGapMs; otherwise empty.
    std::optional<Swipe> TakeFinished() {
        std::lock_guard<std::mutex> lock(this->gate);
        if (this->points.empty() || this->fingerDown || NowMs() - this->releasedAtMs < this->joinGapMs) {
            return std::nullopt;
        }
        const Sample& first = this->points.front();
        const Sample& last = this->points.back();
        Swipe swipe{first.X, first.Y, last.X, last.Y, last.T - first.T,
                    PeakSpeed(this->points), this->presses, this->moves};
        this->points.clear();
        return swipe;
    }

private:
    // Speed is measured over stretches of up to SpeedWindowMs. Stretches
    // shorter than MinSpanMs are skipped: over a few ms the panel's jitter
    // of a few px would read as a burst of speed.
    static constexpr double SpeedWindowMs = 80;
    static constexpr double MinSpanMs = 30;

    struct Sample {
        double T;
        int X, Y;
    };

    static float Distance(const Sample& a, const Sample& b) {
        float dx = static_cast<float>(b.X - a.X), dy = static_cast<float>(b.Y - a.Y);
        return std::sqrt(dx * dx + dy * dy);
    }

    // The fastest the finger moved over any ~SpeedWindowMs stretch of the
    // swipe, in px per ms. For a swipe too quick to have such a stretch, the
    // average over the whole swipe (taking at least one frame).
    static float PeakSpeed(const std::vector<Sample>& points) {
        float best = 0;
        size_t j = 0;
        for (size_t i = 1; i < points.size(); i++) {
            while (points[i].T - points[j].T > SpeedWindowMs) {
                j++;
            }
            double span = points[i].T - points[j].T;
            if (span < MinSpanMs) {
                continue;
            }
            best = std::max(best, Distance(points[j], points[i]) / static_cast<float>(span));
        }
        if (best == 0 && points.size() >= 2) {
            double span = std::max(points.back().T - points.front().T, 16.0);
            best = Distance(points.front(), points.back()) / static_cast<float>(span);
        }
        return best;
    }

    double joinGapMs;
    std::mutex gate;
    std::vector<Sample> points;  // empty when no swipe is in progress
    bool fingerDown = false;
    double releasedAtMs = 0;
    int presses = 0, moves = 0;
};

}  // namespace

int main() {
    // Print each line at once, also when the output goes to VS Code
    std::setvbuf(stdout, nullptr, _IOLBF, 0);

    std::signal(SIGINT, OnSignal);
    std::signal(SIGTERM, OnSignal);

    std::printf("billiards_game_cpp starting\n");

    // -- 1. Write settings ---------------------------------------------------

    DisplaySettings settings;
    settings.Type = DisplayType::Parallel;
    settings.EnableAtBoot = false;

    BacklightSettings backlight;
    backlight.Pin = EPN256::Gpio::Pin::PK7;
    backlight.ActivePolarity = SignalPolarity::ActiveHigh;
    settings.Backlight = backlight;

    ParallelLcdSettings parallel;
    parallel.Width = Width;
    parallel.Height = Height;
    parallel.PixelClockHz = 51200000;
    parallel.HFrontPorch = 160;
    parallel.HSync = 20;
    parallel.HBackPorch = 160;
    parallel.VFrontPorch = 12;
    parallel.VSync = 3;
    parallel.VBackPorch = 23;
    parallel.BitsPerPixel = 16;
    parallel.DataEnablePolarity = SignalPolarity::ActiveHigh;
    parallel.PixelClockEdge = ClockEdge::Rising;
    settings.Parallel = parallel;

    DisplayController::SetSettings(settings);

    // -- 2. Enable display ---------------------------------------------------

    auto display = DisplayController::GetDefault();
    if (!display) {
        throw InvalidOperationException("GetDefault returned null; display.json type is None?");
    }

    std::printf("Display: %dx%d\n", display->Width(), display->Height());
    display->Enable();
    std::printf("IsEnabled=%s\n", display->IsEnabled() ? "true" : "false");

    // -- 3. Initialize UI + wire up touch ------------------------------------

    UI::Initialize();
    TouchBridge::Attach();

    // MA35D1 ADC resistive touch reports raw ADC counts via InputTouch,
    // not panel pixels. Measured on the EPN256 1024x600 dev board by
    // tapping each corner: useful X range is ~210..3950 (inverted - raw
    // high = screen left), useful Y range is ~290..3660 (normal). We
    // linearly map each axis into panel coordinates so LVGL hit-tests
    // land where the finger is.
    constexpr int rawXMin = 210, rawXMax = 3950;
    constexpr int rawYMin = 290, rawYMax = 3660;

    // Swipes, in panel pixels (see the SwipeTracker class above). The handler
    // below feeds it touch events on the touch driver's thread; the animation
    // timer (section 4) collects finished swipes on the UI thread and decides
    // whether each one was a shot.
    //
    // The resistive panel can briefly report the finger lifting mid-slide when
    // the pressure wavers, so a press within SwipeJoinGapMs of a release counts
    // as the same swipe rather than a new one.
    constexpr double SwipeJoinGapMs = 120;
    SwipeTracker swipes(SwipeJoinGapMs);

    auto touch = Devices::Touch::Touch::GetDefault();
    // A Released event carries no points, so the last position seen while the
    // finger was down is what the swipe ends on.
    int lastX = 0, lastY = 0;
    if (!touch) {
        std::printf("No touchscreen found under /dev/input - touch input disabled.\n");
    }
    else {
        std::printf("Touch: %dx%d raw units, connected=%s\n",
                    touch->Width(), touch->Height(), touch->Connected() ? "true" : "false");
        touch->SetOnEvent([&](Devices::Touch::Touch&, const Devices::Touch::TouchEventArgs& e) {
            bool down = e.EventType() != Devices::Touch::TouchEventType::Released;
            if (down) {
                if (e.Points().empty()) {
                    return;
                }
                lastX = std::clamp(static_cast<int>(static_cast<long long>(rawXMax - e.Points()[0].X) * Width /
                                                    (rawXMax - rawXMin)), 0, Width - 1);
                lastY = std::clamp(static_cast<int>(static_cast<long long>(e.Points()[0].Y - rawYMin) * Height /
                                                    (rawYMax - rawYMin)), 0, Height - 1);
                TouchBridge::Feed(lastX, lastY, true);
                swipes.OnPoint(lastX, lastY,
                               e.EventType() == Devices::Touch::TouchEventType::Pressed, NowMs());
            }
            else {
                TouchBridge::Feed(lastX, lastY, false);
                swipes.OnReleased();
            }
        });
    }

    // -- 4. Build the screen -------------------------------------------------

    Screen screen;
    screen.SetBackgroundColor(0x101418);  // near-black fallback behind the wallpaper

    // Billiards table wallpaper: a 1024x600 JPEG under Resources/. The deploy
    // task copies that folder next to the program ("--add Resources"), so the
    // program's own folder is where it finds it at runtime. Image::Load decodes
    // JPEG, PNG, BMP, GIF and TGA; SetBackgroundImage converts the decoded
    // pixels to the RGB565 the panel wants.
    auto background = Drawing::Image::Load(ReadAllBytes(BaseDirectory() + "/Resources/billiards.jpg"));
    screen.SetBackgroundImage(background);

    // Balls on the felt (see the Ball class above). The white cue ball moves
    // when you touch it; the numbered balls sit still until another ball hits
    // them, then the two exchange momentum like billiard balls (equal mass,
    // elastic), and friction brings everything to rest after a few seconds.
    // Every pair of balls can collide. A UiTimer moves them every ~16 ms
    // (~60 fps) on the UI thread, so widget updates are safe (LVGL is
    // single-threaded, LV_USE_OS=LV_OS_NONE).
    //
    // Solids 1-8 use these colors; stripes 9-15 are white with the same color
    // band as the solid eight below them (9 = 1, 10 = 2, ... 15 = 7).
    const uint32_t solidColors[] = {
        0xFFCC00,  // 1 yellow
        0x0033CC,  // 2 blue
        0xCC0000,  // 3 red
        0x4B1478,  // 4 dark purple
        0xFF6600,  // 5 orange
        0x008833,  // 6 green
        0x800000,  // 7 maroon
        0x000000,  // 8 black
    };

    // The rack: a triangle of 15 balls on the left of the table, apex (the
    // 1-ball) pointing right at the cue ball. Laid out like a real 8-ball rack:
    // the 8 in the middle of the third row, and one solid and one stripe in the
    // back corners. Rows are listed apex first.
    const std::vector<std::vector<int>> rack = {
        {1},
        {9, 2},
        {10, 8, 3},
        {4, 11, 5, 12},
        {6, 13, 7, 14, 15},
    };
    constexpr float RackBackX = 200;  // center X of the back (5-ball) row: ~120 px of felt behind it,
                                      // apex lands at x ~370
    constexpr float RackCenterY = (FeltTop + FeltBottom) / 2.0f;  // rack is centered vertically on the felt
    constexpr float Spacing = 2 * R + 1;  // center-to-center distance; the 1 px gap keeps
                                          // neighbors from counting as a collision at rest
    const float rowStep = Spacing * std::sqrt(3.0f) / 2;  // horizontal distance between rows of a tight triangle

    // Cue ball on the right, level with the apex. It sits still until you touch
    // it (see ShootCue below). If it's pocketed, it comes back here (see
    // RespotCue below).
    constexpr float CueStartX = 780 - R, CueStartY = RackCenterY - R;  // top-left corner
    std::vector<std::unique_ptr<Ball>> balls;
    balls.push_back(std::make_unique<Ball>(screen, R, CueStartX, CueStartY, White));
    Ball& cue = *balls.front();
    cue.SetVisible(false);  // the table starts empty; rackTimer shows the cue ball after the rack

    // Where each ball goes (center), in order: rack apex first, row by row, then the cue ball (N = 0).
    struct RackSpot {
        int N;
        float X, Y;
    };
    std::deque<RackSpot> rackSpots;
    for (size_t row = 0; row < rack.size(); row++) {
        float cx = RackBackX + (rack.size() - 1 - row) * rowStep;
        for (size_t i = 0; i < rack[row].size(); i++) {
            rackSpots.push_back({rack[row][i], cx, RackCenterY + (i - row / 2.0f) * Spacing});
        }
    }
    rackSpots.push_back({0, CueStartX + R, CueStartY + R});

    constexpr int RackBuildDelayMs = 150;  // between balls; about 2.5 s for the full table when not debugging
    Ball* lastCreated = nullptr;           // what the previous tick put on the table; null = table still empty
    bool emptyTableDrawn = false, rackDone = false;
    UiTimer rackTimer(RackBuildDelayMs, [&] {
        if (rackDone) {
            return;
        }
        if (!emptyTableDrawn) {  // tick 1: let the empty table paint first
            emptyTableDrawn = true;
            return;
        }

        std::printf(lastCreated == nullptr ? "\n" : "rack: ball%3s\n",
                    lastCreated == nullptr ? "" : lastCreated->Name.c_str());

        if (rackSpots.empty()) {
            rackDone = true;
            std::printf("rack: complete, ready to break\n");
            return;
        }

        RackSpot spot = rackSpots.front();
        rackSpots.pop_front();
        if (spot.N == 0) {  // the cue ball already exists (ShootCue etc. hold on to it), so just show it
            cue.SetVisible(true);
            lastCreated = &cue;
            return;
        }

        bool striped = spot.N > 8;
        balls.push_back(std::make_unique<Ball>(
            screen, R, spot.X - R, spot.Y - R,
            striped ? White : solidColors[spot.N - 1],
            std::to_string(spot.N),
            striped ? std::optional<uint32_t>(solidColors[spot.N - 9]) : std::nullopt));
        lastCreated = balls.back().get();
    });

    Label fpsLabel(screen);
    fpsLabel.X(Width - 160);
    fpsLabel.Y(Height - 40);
    fpsLabel.Width(140);
    fpsLabel.Height(30);
    fpsLabel.FontSize(16);
    fpsLabel.Color(0x888888);
    fpsLabel.TextAlignment(TextAlignment::Right);
    fpsLabel.Text("...");

    long frames = 0;
    double fpsSince = NowMs();

    auto anyMoving = [&] {
        for (auto& ball : balls) {
            if (ball->IsMoving()) {
                return true;
            }
        }
        return false;
    };

    // A pocketed cue ball comes back once the table has stopped moving: on its
    // starting spot, or if a ball is sitting there, the nearest free spot
    // straight above or below it.
    auto RespotCue = [&] {
        float y = CueStartY;
        for (int k = 0; k < 12; k++) {
            float candidate = std::clamp(CueStartY + (k + 1) / 2 * (k % 2 == 1 ? 1 : -1) * (2 * R + 2),
                                         static_cast<float>(MinY), static_cast<float>(MaxY));
            bool free = true;
            for (auto& ball : balls) {
                if (ball.get() == &cue || ball->Pocketed()) {
                    continue;
                }
                float dx = ball->X - CueStartX, dy = ball->Y - candidate;
                if (dx * dx + dy * dy < (2 * R + 1) * (2 * R + 1)) {
                    free = false;
                    break;
                }
            }
            if (free) {
                y = candidate;
                break;
            }
        }
        cue.Respot(CueStartX, y);
        std::printf("cue ball back on the table at (%.0f,%.0f)\n", CueStartX + R, y + R);
    };

    // -- Ready LED -----------------------------------------------------------
    // The board's user LED reports the cue ball: solid while the table is at
    // rest, blinking while the cue ball rolls, and faster the faster it rolls.
    // The blink rate is proportional to the ball's speed, so the half-period is
    // LedFastMs at MaxCueSpeed and grows as the ball slows, capped at LedSlowMs
    // so a nearly stopped ball still reads as moving.
    //
    // EPN256::Gpio::Led is PJ14, wired active-low on this board, so PinValue::Low
    // lights it. The board's own device tree leaves the pin unclaimed, so opening
    // it here costs nothing at boot.
    // The half-period is a whole number of FrameMs ticks, otherwise the toggle
    // lands on alternating frames and the blink visibly jitters.
    constexpr float LedFastMs = 32;
    constexpr float LedSlowMs = 240;
    GpioController gpio;
    gpio.OpenPin(EPN256::Gpio::Led, PinMode::Output);
    auto SetLed = [&](bool on) { gpio.Write(EPN256::Gpio::Led, on ? PinValue::Low : PinValue::High); };

    double ledPhaseSince = NowMs();
    bool ledLit = true;
    SetLed(true);

    auto UpdateReadyLed = [&] {
        float speed = std::sqrt(cue.Vx * cue.Vx + cue.Vy * cue.Vy);
        if (speed <= 0) {
            if (!ledLit) {
                ledLit = true;
                SetLed(true);
            }
            ledPhaseSince = NowMs();
            return;
        }
        float halfPeriod = std::clamp(LedFastMs * MaxCueSpeed / speed, LedFastMs, LedSlowMs);
        if (NowMs() - ledPhaseSince >= halfPeriod) {
            ledLit = !ledLit;
            SetLed(ledLit);
            ledPhaseSince = NowMs();
        }
    };

    // Every swipe is logged with the outcome, so if shots don't fire the output
    // shows why (where the touch landed vs. the ball, how far the finger slid,
    // and how many press/move events the panel sent).
    auto ShootCue = [&](const Swipe& s) {
        float ox = s.FromX - (cue.X + R), oy = s.FromY - (cue.Y + R);
        float startDist = std::sqrt(ox * ox + oy * oy);
        float dx = static_cast<float>(s.ToX - s.FromX), dy = static_cast<float>(s.ToY - s.FromY);
        float len = std::sqrt(dx * dx + dy * dy);

        char outcome[128];
        if (!rackDone) {
            std::snprintf(outcome, sizeof outcome, "ignored: rack still being set up");
        }
        else if (anyMoving()) {
            std::snprintf(outcome, sizeof outcome, "ignored: balls still rolling");
        }
        else if (startDist > R + TouchSlop) {
            std::snprintf(outcome, sizeof outcome,
                          "ignored: started %.0f px from the cue ball's center (limit %.0f)",
                          startDist, R + TouchSlop);
        }
        else if (len < MinSwipe) {
            std::snprintf(outcome, sizeof outcome, "ignored: slide too short (limit %.0f px)", MinSwipe);
        }
        else {
            float power = std::clamp(s.PeakSpeed * FrameMs * SpeedGain, MinCueSpeed, MaxCueSpeed);
            cue.Vx = dx / len * power;
            cue.Vy = dy / len * power;
            std::snprintf(outcome, sizeof outcome, "SHOT at %.1f px/frame", power);
        }
        std::printf("swipe (%d,%d) -> (%d,%d), %.0f px in %.0f ms, peak %.0f px/s, "
                    "%d press / %d move events, cue ball at (%.0f,%.0f): %s\n",
                    s.FromX, s.FromY, s.ToX, s.ToY, len, s.DurationMs, s.PeakSpeed * 1000,
                    s.Presses, s.Moves, cue.X + R, cue.Y + R, outcome);
    };

    UiTimer animTimer(FrameMs, [&] {
        if (stopRequested) {
            UI::Stop();
            return;
        }

        if (cue.Pocketed() && !anyMoving()) {
            RespotCue();
        }

        if (auto swipe = swipes.TakeFinished()) {
            ShootCue(*swipe);
        }

        for (auto& ball : balls) {
            if (ball->Pocketed()) {
                continue;
            }
            int touching = ball->Step(MinX, MinY, MaxX, MaxY);
            if (touching != WallNone && FallsInPocket(*ball, touching)) {
                ball->Pocket();
                int left = 0;
                for (auto& other : balls) {
                    if (other.get() != &cue && !other->Pocketed()) {
                        left++;
                    }
                }
                std::printf("ball %s pocketed at (%.0f,%.0f), %d numbered balls left\n",
                            ball->Name.c_str(), ball->X + R, ball->Y + R, left);
            }
        }
        for (size_t i = 0; i < balls.size(); i++) {
            for (size_t j = i + 1; j < balls.size(); j++) {
                if (!balls[i]->Pocketed() && !balls[j]->Pocketed()) {
                    Ball::Collide(*balls[i], *balls[j]);
                }
            }
        }
        for (auto& ball : balls) {
            if (ball->Pocketed()) {
                continue;
            }
            ball->ApplyFriction(Friction);
            ball->Draw();
        }

        UpdateReadyLed();

        frames++;
        double elapsed = NowMs() - fpsSince;
        if (elapsed >= 1000) {
            char text[32];
            std::snprintf(text, sizeof text, "%.0f fps", frames * 1000.0 / elapsed);
            fpsLabel.Text(text);
            frames = 0;
            fpsSince = NowMs();
        }
    });

    screen.Load();
    std::printf("entering UI.Run\n");
    UI::Run();
    std::printf("Stop requested - shutting down.\n");
    return 0;
}
