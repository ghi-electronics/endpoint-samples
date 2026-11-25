# billiards_game_py - exercises the Endpoint display + touch + UI
# stack on the EPN256 (Nuvoton MA35D1) 1024x600 panel. Everything draws
# through the GHIElectronics.Endpoint.UI library (LVGL under the hood).
#
# The Python twin of billiards_game_net. Same game, same numbers, same
# order of operations; the library calls are the PascalCase Python
# mirrors of the .NET ones.
#
# What this does, in order:
#   1. Writes /config/display.json via DisplayController.SetSettings:
#      type = Parallel, backlight on PK7 (active-high),
#      1024x600 with the panel's timings (ma35-drm actually reads them
#      from the device tree; the fields here just describe the visible
#      area that LVGL uses for the canvas).
#   2. Calls DisplayController.Enable(), which modprobes ma35-drm and
#      raises the backlight.
#   3. UI.Initialize() wires LVGL to the framebuffer. TouchBridge.Attach()
#      sets up the input layer; Touch.GetDefault() opens the panel's
#      resistive touchscreen (ma35d1-ts kernel driver, always on).
#   4. Builds a screen: the billiards table wallpaper, a full set of 16
#      balls animated by a UiTimer (solids 1-8 and stripes 9-15 racked in
#      a triangle on the left, and a white cue ball on the right that
#      you shoot by sliding your finger off it; friction stops every
#      ball after a few seconds, then you touch the cue ball again for the
#      next shot) and an FPS readout.
#   5. Blinks the board's user LED in time with the cue ball: solid when the
#      table is at rest, blinking faster the faster the ball rolls.

import math
import os
import signal
import sys
import threading
import time
from collections import deque

from GHIElectronics.Endpoint.Devices.Display import (
    BacklightSettings, ClockEdge, DisplayController, DisplaySettings,
    DisplayType, ParallelLcdSettings, SignalPolarity)
from GHIElectronics.Endpoint.Devices.Gpio import GpioController, PinMode, PinValue
from GHIElectronics.Endpoint.Devices.Touch import Touch, TouchEventType
from GHIElectronics.Endpoint.Drawing import Image
from GHIElectronics.Endpoint.Pins import EPN256
from GHIElectronics.Endpoint.UI import (
    Border, Label, Screen, TextAlignment, TouchBridge, UI, UiTimer)

# print() is block-buffered when stdout is a pipe, which is how
# "endpoint: run" and F5 capture it, so the game's log would arrive in
# bursts minutes late. Line buffering sends each line as it happens.
sys.stdout.reconfigure(line_buffering=True)

WIDTH = 1024
HEIGHT = 600


def clamp(value, low, high):
    return low if value < low else high if value > high else value


# Elapsed milliseconds since the program started. time.monotonic never goes
# backwards, so it is safe for the speed and blink-rate maths below.
def now_ms():
    return time.monotonic() * 1000.0


print("billiards_game_py starting")

# -- 1. Write settings --------------------------------------------------------

backlight = BacklightSettings()
backlight.Pin = EPN256.Gpio.Pin.PK7
backlight.ActivePolarity = SignalPolarity.ActiveHigh

parallel = ParallelLcdSettings()
parallel.Width = WIDTH
parallel.Height = HEIGHT
parallel.PixelClockHz = 51_200_000
parallel.HFrontPorch = 160
parallel.HSync = 20
parallel.HBackPorch = 160
parallel.VFrontPorch = 12
parallel.VSync = 3
parallel.VBackPorch = 23
parallel.BitsPerPixel = 16
parallel.DataEnablePolarity = SignalPolarity.ActiveHigh
parallel.PixelClockEdge = ClockEdge.Rising

settings = DisplaySettings()
settings.Type = DisplayType.Parallel
settings.EnableAtBoot = False
settings.Backlight = backlight
settings.Parallel = parallel

DisplayController.SetSettings(settings)

# -- 2. Enable display -------------------------------------------------------

display = DisplayController.GetDefault()
if display is None:
    raise RuntimeError("GetDefault returned None; display.json type is None?")

print(f"Display: {display.Width}x{display.Height}")
display.Enable()
print(f"IsEnabled={display.IsEnabled}")

# -- 3. Initialize UI + wire up touch ----------------------------------------

UI.Initialize()
TouchBridge.Attach()

# SIGINT / SIGTERM stop the UI loop so the program leaves the display and
# the LED the way it found them. Python only runs a signal handler on the
# main thread, and UI.Run() blocks it inside LVGL, so the handler does not
# fire the instant the signal arrives: it fires on the next UiTimer
# callback, which is Python running on that same thread. With a 16 ms
# animation timer below that is the next frame.
signal.signal(signal.SIGINT, lambda *_: UI.Stop())
signal.signal(signal.SIGTERM, lambda *_: UI.Stop())

# MA35D1 ADC resistive touch reports raw ADC counts via InputTouch,
# not panel pixels. Measured on the EPN256 1024x600 dev board by
# tapping each corner: useful X range is ~210..3950 (inverted - raw
# high = screen left), useful Y range is ~290..3660 (normal). We
# linearly map each axis into panel coordinates so LVGL hit-tests
# land where the finger is.
RAW_X_MIN, RAW_X_MAX = 210, 3950
RAW_Y_MIN, RAW_Y_MAX = 290, 3660


# Turns the touch driver's press / move / release events into whole swipes.
# on_point and on_released are called on the touch thread, take_finished on
# the UI thread; everything is guarded by one lock.
#
# A swipe only counts as finished once the finger has stayed up for
# join_gap_ms. If it lands again sooner, that's treated as the same swipe
# continuing, which papers over the resistive panel's brief false releases.
class SwipeTracker:
    # Speed is measured over stretches of up to SPEED_WINDOW_MS. Stretches
    # shorter than MIN_SPAN_MS are skipped: over a few ms the panel's jitter
    # of a few px would read as a burst of speed.
    SPEED_WINDOW_MS = 80
    MIN_SPAN_MS = 30

    def __init__(self, join_gap_ms):
        self.join_gap_ms = join_gap_ms
        self._gate = threading.Lock()
        self._points = []  # (t, x, y); empty when no swipe is in progress
        self._finger_down = False
        self._released_at_ms = 0.0
        self._presses = 0
        self._moves = 0

    # time_ms: when the touch event arrived, for speed.
    def on_point(self, x, y, pressed, time_ms):
        with self._gate:
            if not self._points:
                self._presses = self._moves = 0
            if pressed:
                self._presses += 1
            else:
                self._moves += 1
            self._points.append((time_ms, x, y))
            self._finger_down = True

    def on_released(self):
        with self._gate:
            self._finger_down = False
            self._released_at_ms = now_ms()

    # The swipe, once the finger has been up for join_gap_ms; otherwise None.
    # A finished swipe is (from_x, from_y, to_x, to_y, duration_ms,
    # peak_speed, presses, moves): where the finger first touched, where it
    # last was before lifting, how long it took, its peak speed in px per ms
    # (see _peak_speed), and how many press / move events made it up (more
    # than one press means the panel reported the finger lifting and landing
    # again mid-slide).
    def take_finished(self):
        with self._gate:
            if (not self._points or self._finger_down
                    or now_ms() - self._released_at_ms < self.join_gap_ms):
                return None
            first, last = self._points[0], self._points[-1]
            swipe = (first[1], first[2], last[1], last[2],
                     last[0] - first[0], self._peak_speed(self._points),
                     self._presses, self._moves)
            self._points = []
            return swipe

    # The fastest the finger moved over any ~SPEED_WINDOW_MS stretch of the
    # swipe, in px per ms. For a swipe too quick to have such a stretch, the
    # average over the whole swipe (taking at least one frame).
    @classmethod
    def _peak_speed(cls, points):
        best = 0.0
        j = 0
        for i in range(1, len(points)):
            while points[i][0] - points[j][0] > cls.SPEED_WINDOW_MS:
                j += 1
            span = points[i][0] - points[j][0]
            if span < cls.MIN_SPAN_MS:
                continue
            best = max(best, cls._distance(points[j], points[i]) / span)
        if best == 0 and len(points) >= 2:
            best = cls._distance(points[0], points[-1]) / max(points[-1][0] - points[0][0], 16)
        return best

    @staticmethod
    def _distance(a, b):
        return math.hypot(b[1] - a[1], b[2] - a[2])


# Swipes, in panel pixels. The handler below feeds the tracker touch events
# on the touch driver's thread; the animation timer (section 4) collects
# finished swipes on the UI thread and decides whether each one was a shot.
#
# The resistive panel can briefly report the finger lifting mid-slide when
# the pressure wavers, so a press within SWIPE_JOIN_GAP_MS of a release
# counts as the same swipe rather than a new one.
SWIPE_JOIN_GAP_MS = 120
swipes = SwipeTracker(SWIPE_JOIN_GAP_MS)

touch = Touch.GetDefault()
if touch is None:
    print("No touchscreen found under /dev/input - touch input disabled.")
else:
    print(f"Touch: {touch.Width}x{touch.Height} raw units, connected={touch.Connected}")

    # A Released event carries no points, so the last position seen while the
    # finger was down is what the swipe ends on.
    last_point = [0, 0]

    def on_touch_changed(_sender, e):
        down = e.EventType != TouchEventType.Released
        if down:
            if not e.Points:
                return
            last_point[0] = clamp((RAW_X_MAX - e.Points[0].X) * WIDTH // (RAW_X_MAX - RAW_X_MIN), 0, WIDTH - 1)
            last_point[1] = clamp((e.Points[0].Y - RAW_Y_MIN) * HEIGHT // (RAW_Y_MAX - RAW_Y_MIN), 0, HEIGHT - 1)
            TouchBridge.Feed(last_point[0], last_point[1], True)
            swipes.on_point(last_point[0], last_point[1],
                            e.EventType == TouchEventType.Pressed, now_ms())
        else:
            TouchBridge.Feed(last_point[0], last_point[1], False)
            swipes.on_released()

    touch.OnChangedEvent += on_touch_changed

# -- 4. Build the screen ------------------------------------------------------

# Which edges of the felt a ball is touching (see Ball.step).
WALL_NONE = 0
WALL_LEFT = 1
WALL_RIGHT = 2
WALL_TOP = 4
WALL_BOTTOM = 8


# A filled circle on the screen with a position and velocity. Endpoint.UI
# has no primitive "DrawCircle"; a Border with CornerRadius = half its side
# renders a true filled circle in LVGL. x/y are the top-left corner in panel
# pixels, matching the widget's coordinates.
#
# A numbered ball (e.g. the 8-ball) also gets a white spot with the number
# in black on top, like a real pool ball. The spot and number are siblings
# on the same parent (not children of the ball) and are moved along with it
# in draw, so they center exactly without depending on how LVGL offsets
# children inside a Border (padding / border width).
#
# A striped ball (9-15) is a ball of the base color with a colored band
# across its middle. Endpoint.UI can't clip a widget to a circle, so the
# band is the overlap of three centered shapes, each sized to stay inside
# the circle:
#   - a plain rectangle as wide as the circle is at the band's top and
#     bottom edges, giving the band its straight top and bottom;
#   - a full-width "pill" (rounded rect, radius = half its height) whose
#     round ends touch the ball's edge at its widest point;
#   - a slightly narrower rounded rect with smaller corners that fills in
#     where the circle's edge sits between the other two.
# Together they leave under 1 px of white along the band's curved sides and
# never spill outside the ball.
#
# Widget count matters: LVGL's memory pool inside libendpoint is fixed, and
# the program died partway through building the rack when each stripe was
# 14 thin strips (~145 widgets in all). Ball.widget_count tracks the total.
class Ball:
    SPOT_RADIUS = 13
    NUMBER_FONT_SIZE = 20    # one of the fonts libendpoint ships (14/20/32)
    NUMBER_LINE_HEIGHT = 22  # LVGL Montserrat 20 line height, for vertical centering
    STRIPE_HALF_HEIGHT = 14  # band is 28 px tall on a 48 px ball; just wider than the spot

    widget_count = 0

    def __init__(self, parent, radius, x, y, color, number=None, stripe=None):
        self.radius = radius
        self.name = number if number is not None else "cue"  # used in log lines
        self.x, self.y = float(x), float(y)
        self.vx = self.vy = 0.0
        self.pocketed = False

        self._view = Border(parent)
        self._view.Width = radius * 2
        self._view.Height = radius * 2
        self._view.CornerRadius = radius
        self._view.BackgroundColor = color
        Ball.widget_count += 1

        # Created after the ball and before the spot so they draw on top of
        # the ball and underneath the spot and number.
        self._stripe_parts = []  # (widget, dx, dy) offsets from the ball's top-left
        if stripe is not None:
            h = Ball.STRIPE_HALF_HEIGHT
            chord_half = int(math.sqrt(radius * radius - h * h))  # circle's half-width at the band edge
            inner_half = radius - 1
            # Smallest corner radius that keeps the narrower rect inside the circle.
            inner_corner = 1
            while math.hypot(inner_half - inner_corner, h - inner_corner) + inner_corner > radius:
                inner_corner += 1

            self._stripe_parts = [
                self._make_stripe_part(parent, stripe, radius, chord_half, h, 0),
                self._make_stripe_part(parent, stripe, radius, radius, h, h),
                self._make_stripe_part(parent, stripe, radius, inner_half, h, inner_corner),
            ]

        self._spot = None
        self._number_label = None
        if number is not None:
            self._spot = Border(parent)
            self._spot.Width = Ball.SPOT_RADIUS * 2
            self._spot.Height = Ball.SPOT_RADIUS * 2
            self._spot.CornerRadius = Ball.SPOT_RADIUS
            self._spot.BackgroundColor = 0xFFFFFF
            Ball.widget_count += 1

            self._number_label = Label(parent)
            self._number_label.Width = Ball.SPOT_RADIUS * 2
            self._number_label.Height = Ball.NUMBER_LINE_HEIGHT
            self._number_label.FontSize = Ball.NUMBER_FONT_SIZE
            self._number_label.Color = 0x000000
            self._number_label.TextAlignment = TextAlignment.Center
            self._number_label.Text = number
            Ball.widget_count += 1

        self.draw()

    # One piece of a stripe: a rounded rect half_width*2 wide and
    # half_height*2 tall, centered on the ball. Thickness = 0 because
    # Border's default grey outline would otherwise cover thin pieces and
    # tint the band's edges.
    @staticmethod
    def _make_stripe_part(parent, color, radius, half_width, half_height, corner):
        part = Border(parent)
        part.Width = half_width * 2
        part.Height = half_height * 2
        part.CornerRadius = corner
        part.Thickness = 0
        part.BackgroundColor = color
        Ball.widget_count += 1
        return (part, radius - half_width, radius - half_height)

    # Advance one frame and bounce off the rectangle [min_x..max_x] x
    # [min_y..max_y] (limits for the top-left corner). Clamping, and forcing
    # the velocity to point back inside, keeps a ball that a collision nudged
    # past the edge from getting stuck flipping direction every frame.
    #
    # Returns the walls the ball is touching after the move (including one it
    # is rolling along), so the caller can check whether that spot is a
    # pocket rather than a cushion.
    def step(self, min_x, min_y, max_x, max_y):
        self.x += self.vx
        self.y += self.vy
        touching = WALL_NONE
        if self.x <= min_x:
            self.x = min_x
            self.vx = abs(self.vx)
            touching |= WALL_LEFT
        elif self.x >= max_x:
            self.x = max_x
            self.vx = -abs(self.vx)
            touching |= WALL_RIGHT
        if self.y <= min_y:
            self.y = min_y
            self.vy = abs(self.vy)
            touching |= WALL_TOP
        elif self.y >= max_y:
            self.y = max_y
            self.vy = -abs(self.vy)
            touching |= WALL_BOTTOM
        return touching

    @property
    def is_moving(self):
        return self.vx != 0 or self.vy != 0

    # A pocketed ball is hidden and takes no further part in the game (the
    # caller skips it) until respot puts it back.
    def pocket(self):
        self.pocketed = True
        self.vx = self.vy = 0.0
        self.set_visible(False)

    def respot(self, x, y):
        self.x, self.y = float(x), float(y)
        self.vx = self.vy = 0.0
        self.pocketed = False
        self.draw()
        self.set_visible(True)

    def set_visible(self, visible):
        self._view.Visible = visible
        for part, _, _ in self._stripe_parts:
            part.Visible = visible
        if self._spot is not None:
            self._spot.Visible = visible
        if self._number_label is not None:
            self._number_label.Visible = visible

    # Slow the ball by decel px/frame without changing its direction, and
    # stop it dead once it's slower than that, so it can't creep forever.
    def apply_friction(self, decel):
        speed = math.hypot(self.vx, self.vy)
        if speed <= decel:
            self.vx = self.vy = 0.0
            return
        scale = (speed - decel) / speed
        self.vx *= scale
        self.vy *= scale

    def draw(self):
        self._view.X = int(self.x)
        self._view.Y = int(self.y)

        for part, dx, dy in self._stripe_parts:
            part.X = int(self.x) + dx
            part.Y = int(self.y) + dy

        if self._spot is not None:
            cx, cy = int(self.x) + self.radius, int(self.y) + self.radius
            self._spot.X = cx - Ball.SPOT_RADIUS
            self._spot.Y = cy - Ball.SPOT_RADIUS
            self._number_label.X = cx - Ball.SPOT_RADIUS
            self._number_label.Y = cy - Ball.NUMBER_LINE_HEIGHT // 2

    # Elastic collision between two equal-mass balls: if they overlap and are
    # moving toward each other, swap their velocity components along the line
    # between their centers, then push them apart so they don't stay
    # overlapped. A head-on hit stops the striking ball and sends the struck
    # one off at the same speed, as on a real table.
    @staticmethod
    def collide(a, b):
        nx = (b.x + b.radius) - (a.x + a.radius)
        ny = (b.y + b.radius) - (a.y + a.radius)
        min_dist = a.radius + b.radius
        dist2 = nx * nx + ny * ny
        if dist2 >= min_dist * min_dist or dist2 == 0:
            return

        dist = math.sqrt(dist2)
        nx /= dist
        ny /= dist

        approach = (a.vx - b.vx) * nx + (a.vy - b.vy) * ny
        if approach > 0:
            a.vx -= approach * nx
            a.vy -= approach * ny
            b.vx += approach * nx
            b.vy += approach * ny

        push = (min_dist - dist) / 2
        a.x -= nx * push
        a.y -= ny * push
        b.x += nx * push
        b.y += ny * push


screen = Screen()
screen.BackgroundColor = 0x101418  # near-black fallback behind the wallpaper

# Billiards table wallpaper: a 1024x600 JPEG under Resources/. The whole
# project folder goes to the board, so the folder of this file is where the
# program finds it at runtime. Image.Load decodes JPEG, PNG, BMP, GIF and
# TGA; SetBackgroundImage converts the decoded pixels to the RGB565 the
# panel wants.
#
# The playing surface: the felt inside the wooden rails and green cushions,
# measured from the table image (where the cushions' inner edges meet the
# felt). The rails aren't the same width on every side, so each edge has its
# own value. Balls stay inside this box. Re-measure these if the table image
# changes.
FELT_LEFT, FELT_RIGHT = 53, 970
FELT_TOP, FELT_BOTTOM = 49, 538
here = os.path.dirname(os.path.abspath(__file__))
with open(os.path.join(here, "Resources", "billiards.jpg"), "rb") as f:
    background = Image.Load(f.read())
screen.SetBackgroundImage(background)

# Balls on the felt. The white cue ball moves when you touch it; the
# numbered balls sit still until another ball hits them, then the two
# exchange momentum like billiard balls (equal mass, elastic), and friction
# brings everything to rest after a few seconds. Every pair of balls can
# collide. A UiTimer moves them every ~16 ms (~60 fps) on the UI thread, so
# widget updates are safe (LVGL is single-threaded, LV_USE_OS=LV_OS_NONE).
#
# Solids 1-8 use these colors; stripes 9-15 are white with the same color
# band as the solid eight below them (9 = 1, 10 = 2, ... 15 = 7).
R = 24
WHITE = 0xFFFFFF
solid_colors = [
    0xFFCC00,  # 1 yellow
    0x0033CC,  # 2 blue
    0xCC0000,  # 3 red
    0x4B1478,  # 4 dark purple
    0xFF6600,  # 5 orange
    0x008833,  # 6 green
    0x800000,  # 7 maroon
    0x000000,  # 8 black
]

# The rack: a triangle of 15 balls on the left of the table, apex (the
# 1-ball) pointing right at the cue ball. Laid out like a real 8-ball rack:
# the 8 in the middle of the third row, and one solid and one stripe in the
# back corners. Rows are listed apex first.
rack = [
    [1],
    [9, 2],
    [10, 8, 3],
    [4, 11, 5, 12],
    [6, 13, 7, 14, 15],
]
RACK_BACK_X = 200                              # center X of the back (5-ball) row: ~120 px of
                                               # felt behind it, apex lands at x ~370
RACK_CENTER_Y = (FELT_TOP + FELT_BOTTOM) / 2   # rack is centered vertically on the felt
SPACING = 2 * R + 1                            # center-to-center distance; the 1 px gap keeps
                                               # neighbors from counting as a collision at rest
row_step = SPACING * math.sqrt(3) / 2          # horizontal distance between rows of a tight triangle

# Cue ball on the right, level with the apex. It sits still until you touch
# it (see shoot_cue below). If it's pocketed, it comes back here (see
# respot_cue below).
CUE_START_X, CUE_START_Y = 780 - R, RACK_CENTER_Y - R  # top-left corner
cue = Ball(screen, R, x=CUE_START_X, y=CUE_START_Y, color=WHITE)
cue.set_visible(False)  # the table starts empty; rack_timer shows the cue ball after the rack
balls = [cue]           # the rack balls join as rack_timer creates them

# Where each ball goes (center), in order: rack apex first, row by row, then
# the cue ball (n = 0).
rack_spots = deque()
for row in range(len(rack)):
    cx = RACK_BACK_X + (len(rack) - 1 - row) * row_step
    for i in range(len(rack[row])):
        rack_spots.append((rack[row][i], cx, RACK_CENTER_Y + (i - row / 2) * SPACING))
rack_spots.append((0, CUE_START_X + R, CUE_START_Y + R))

RACK_BUILD_DELAY_MS = 150  # between balls; about 2.5 s for the full table when not debugging
state = {"last_created": None, "empty_table_drawn": False, "rack_done": False}


def build_rack():
    if state["rack_done"]:
        return
    if not state["empty_table_drawn"]:  # tick 1: let the empty table paint first
        state["empty_table_drawn"] = True
        return

    last = state["last_created"]
    print("" if last is None else f"rack: ball{last.name:>3}")

    if not rack_spots:
        state["rack_done"] = True
        print("rack: complete, ready to break")
        return

    n, spot_x, spot_y = rack_spots.popleft()
    if n == 0:  # the cue ball already exists (shoot_cue etc. hold on to it), so just show it
        cue.set_visible(True)
        state["last_created"] = cue
        return

    striped = n > 8
    state["last_created"] = Ball(
        screen, R, x=spot_x - R, y=spot_y - R,
        color=WHITE if striped else solid_colors[n - 1],
        number=str(n),
        stripe=solid_colors[n - 9] if striped else None)
    balls.append(state["last_created"])


rack_timer = UiTimer(RACK_BUILD_DELAY_MS, build_rack)

fps_label = Label(screen)
fps_label.X = WIDTH - 160
fps_label.Y = HEIGHT - 40
fps_label.Width = 140
fps_label.Height = 30
fps_label.FontSize = 16
fps_label.Color = 0x888888
fps_label.TextAlignment = TextAlignment.Right
fps_label.Text = "..."

fps = {"frames": 0, "since_ms": now_ms()}

# Limits for a ball's top-left corner that keep the whole ball on the felt
# (see FELT_LEFT etc. above); balls bounce off the cushions there.
MIN_X, MIN_Y = FELT_LEFT, FELT_TOP
MAX_X, MAX_Y = FELT_RIGHT - 2 * R, FELT_BOTTOM - 2 * R

# Pockets. The cushions don't run the full length of each side: they stop
# at the six pockets. These are the stretches the cushions do cover,
# measured from the table image (felt coordinates, px). A ball that reaches
# the edge of the felt where there's no cushion drops into the pocket and
# disappears. POCKET_JAW makes the openings a little more forgiving: a ball
# whose center is within that many px of a cushion's end goes in too, as
# its edge hangs over the pocket.
TOP_CUSHIONS = [(96, 475), (548, 928)]     # along x
BOTTOM_CUSHIONS = [(95, 475), (548, 931)]  # along x
SIDE_CUSHIONS = [(85, 505)]                # along y, left and right alike
POCKET_JAW = 6


def on_cushion(center, cushions):
    return any(start + POCKET_JAW <= center <= end - POCKET_JAW for start, end in cushions)


def falls_in_pocket(ball, touching):
    cx, cy = ball.x + R, ball.y + R
    return ((touching & WALL_TOP and not on_cushion(cx, TOP_CUSHIONS))
            or (touching & WALL_BOTTOM and not on_cushion(cx, BOTTOM_CUSHIONS))
            or (touching & WALL_LEFT and not on_cushion(cy, SIDE_CUSHIONS))
            or (touching & WALL_RIGHT and not on_cushion(cy, SIDE_CUSHIONS)))


# A pocketed cue ball comes back once the table has stopped moving: on its
# starting spot, or if a ball is sitting there, the nearest free spot
# straight above or below it.
def respot_cue():
    y = CUE_START_Y
    for k in range(12):
        candidate = clamp(CUE_START_Y + (k + 1) // 2 * (1 if k % 2 else -1) * (2 * R + 2), MIN_Y, MAX_Y)
        free = not any(b is not cue and not b.pocketed
                       and (b.x - CUE_START_X) ** 2 + (b.y - candidate) ** 2 < (2 * R + 1) ** 2
                       for b in balls)
        if free:
            y = candidate
            break
    cue.respot(CUE_START_X, y)
    print(f"cue ball back on the table at ({CUE_START_X + R:.0f},{y + R:.0f})")


# To shoot, put your finger on the cue ball, slide it the way you want the
# ball to go, and lift. The ball rolls in the direction of the slide (from
# where you pressed to where you lifted). Ignored:
#   - swipes that don't start on the cue ball (within TOUCH_SLOP px of it,
#     since the resistive panel's calibration is only approximate);
#   - slides shorter than MIN_SWIPE px, e.g. a plain tap, which has no clear
#     direction;
#   - anything while a ball is still rolling: wait for the table to settle,
#     then take the next shot.
#
# Shot strength follows how fast you slide. SwipeTracker reports the
# finger's peak speed, measured over ~80 ms stretches of the slide (so a
# pause before the flick, or slowing down just before lifting, doesn't
# weaken the shot). The cue ball leaves at that speed times SPEED_GAIN, then
# clamped to MIN_CUE_SPEED..MAX_CUE_SPEED. A deliberate slide on this panel
# peaks around 1500 px/s and a hard flick around 3600, which is 24 to 58
# px/frame raw, so the gain has to scale that down or every shot pins at the
# maximum and sliding faster stops making any difference.
#
# Every frame, friction takes FRICTION px/frame off each ball's speed
# (constant deceleration, like a ball rolling on felt). A ball at speed v
# rolls for v / FRICTION frames and v^2 / (2 * FRICTION) px: a 6 px/frame
# tap stops after ~2 s and 360 px, a full 18 px/frame flick after ~6 s and
# ~3200 px (bouncing off the rails).
FRAME_MS = 16          # animation timer period
SPEED_GAIN = 0.3       # ball speed / finger speed
MIN_CUE_SPEED = 1.5    # px per frame
MAX_CUE_SPEED = 18.0   # px per frame
FRICTION = 0.05        # px per frame, per frame
TOUCH_SLOP = 30.0      # px beyond the ball's edge; a fingertip on the ball's rim landed 40 px from center
MIN_SWIPE = 20.0       # px

# -- Ready LED ----------------------------------------------------------------
# The board's user LED reports the cue ball: solid while the table is at rest,
# blinking while the cue ball rolls, and faster the faster it rolls. The blink
# rate is proportional to the ball's speed, so the half-period is LED_FAST_MS
# at MAX_CUE_SPEED and grows as the ball slows, capped at LED_SLOW_MS so a
# nearly stopped ball still reads as moving.
#
# EPN256.Gpio.Led is PJ14, wired active-low on this board, so PinValue.Low
# lights it. The board's own device tree leaves the pin unclaimed, so opening
# it here costs nothing at boot.
# The half-period is a whole number of FRAME_MS ticks, otherwise the toggle
# lands on alternating frames and the blink visibly jitters.
LED_FAST_MS = 32
LED_SLOW_MS = 240
gpio = GpioController()
gpio.OpenPin(EPN256.Gpio.Led, PinMode.Output)


def set_led(on):
    gpio.Write(EPN256.Gpio.Led, PinValue.Low if on else PinValue.High)


led = {"lit": True, "phase_ms": now_ms()}
set_led(True)


def update_ready_led():
    speed = math.hypot(cue.vx, cue.vy)
    if speed <= 0:
        if not led["lit"]:
            led["lit"] = True
            set_led(True)
        led["phase_ms"] = now_ms()
        return
    half_period = clamp(LED_FAST_MS * MAX_CUE_SPEED / speed, LED_FAST_MS, LED_SLOW_MS)
    if now_ms() - led["phase_ms"] >= half_period:
        led["lit"] = not led["lit"]
        set_led(led["lit"])
        led["phase_ms"] = now_ms()


# Every swipe is logged with the outcome, so if shots don't fire the output
# shows why (where the touch landed vs. the ball, how far the finger slid,
# and how many press/move events the panel sent).
def shoot_cue(swipe):
    from_x, from_y, to_x, to_y, duration_ms, peak_speed, presses, moves = swipe
    start_dist = math.hypot(from_x - (cue.x + R), from_y - (cue.y + R))
    dx, dy = to_x - from_x, to_y - from_y
    length = math.hypot(dx, dy)

    if not state["rack_done"]:
        outcome = "ignored: rack still being set up"
    elif any(b.is_moving for b in balls):
        outcome = "ignored: balls still rolling"
    elif start_dist > R + TOUCH_SLOP:
        outcome = f"ignored: started {start_dist:.0f} px from the cue ball's center (limit {R + TOUCH_SLOP:.0f})"
    elif length < MIN_SWIPE:
        outcome = f"ignored: slide too short (limit {MIN_SWIPE:.0f} px)"
    else:
        power = clamp(peak_speed * FRAME_MS * SPEED_GAIN, MIN_CUE_SPEED, MAX_CUE_SPEED)
        cue.vx = dx / length * power
        cue.vy = dy / length * power
        outcome = f"SHOT at {power:.1f} px/frame"

    print(f"swipe ({from_x},{from_y}) -> ({to_x},{to_y}), {length:.0f} px in {duration_ms:.0f} ms, "
          f"peak {peak_speed * 1000:.0f} px/s, {presses} press / {moves} move events, "
          f"cue ball at ({cue.x + R:.0f},{cue.y + R:.0f}): {outcome}")


def animate():
    if cue.pocketed and not any(b.is_moving for b in balls):
        respot_cue()

    swipe = swipes.take_finished()
    if swipe is not None:
        shoot_cue(swipe)

    for ball in balls:
        if ball.pocketed:
            continue
        touching = ball.step(MIN_X, MIN_Y, MAX_X, MAX_Y)
        if touching != WALL_NONE and falls_in_pocket(ball, touching):
            ball.pocket()
            left = sum(1 for b in balls if b is not cue and not b.pocketed)
            print(f"ball {ball.name} pocketed at ({ball.x + R:.0f},{ball.y + R:.0f}), {left} numbered balls left")
    for i in range(len(balls)):
        for j in range(i + 1, len(balls)):
            if not balls[i].pocketed and not balls[j].pocketed:
                Ball.collide(balls[i], balls[j])
    for ball in balls:
        if ball.pocketed:
            continue
        ball.apply_friction(FRICTION)
        ball.draw()

    update_ready_led()

    fps["frames"] += 1
    elapsed = now_ms() - fps["since_ms"]
    if elapsed >= 1000:
        fps_label.Text = f"{fps['frames'] * 1000.0 / elapsed:.0f} fps"
        fps["frames"] = 0
        fps["since_ms"] = now_ms()


anim_timer = UiTimer(FRAME_MS, animate)

screen.Load()
print("entering UI.Run")
UI.Run()
print("Stop requested - shutting down.")

anim_timer.Dispose()
rack_timer.Dispose()
