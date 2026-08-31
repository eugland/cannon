# Cannon game plan (Orbital)

## 1. Product idea

Build a short-session 3D physics puzzle game set in space. The player operates a stylized space cannon that fires a projectile across a small solar system of planets. Each planet has its own gravity. The projectile flies straight through empty space and curves when it enters a planet's gravity field. The player aims a direction, charges the shot, and uses gravity slingshots and orbits to reach targets that sit on planet surfaces, then destroys them with impact physics and chain reactions.

This is a game mechanic inspired by Angry Birds Space, not a real orbital-mechanics simulator. Gravity, ranges, controls, and presentation are fictional and tuned for clarity and fun.

## 2. Differentiation

The core fantasy is **reading a curved path through gravity**, not dragging a projectile straight onto a target. The player must account for one or more gravity wells bending the shot. Depth comes from level design — planet placement, field overlap, orbits, and obstacles — rather than character abilities or a large brand.

Compared to references:

- Royal Smash and direct-fire games reward hitting a visible weak point in a straight line. Cannon rewards indirect, gravity-bent fire.
- Angry Birds Space uses a drag-and-release slingshot. Cannon uses a **timed-charge space cannon** (see Section 8) so power comes from hold duration, giving a distinct control feel.

Original art, names, sounds, UI, level layouts, and progression are required. No Angry Birds or Royal Smash assets, characters, presentation, or level designs will be copied.

## 3. Platform and engine

Use the current stable Unity LTS release with C#, Universal Render Pipeline, Unity Input System, and Unity's built-in 3D physics.

Why Unity:

- Mature Android, iOS, Windows, and macOS build pipelines.
- Strong 3D rigid-body physics and profiling tools.
- Touch, mouse, keyboard, and controller support through one input layer.
- Large ecosystem for mobile performance, testing, audio, and release tooling.

The exact Unity editor version will be pinned before engine scaffolding. Android ships first, but the first playable slice must also run on Windows. This catches touch-only assumptions early.

This is a **3D** game. Planets are spheres with real 3D positions and radial gravity. The camera and aiming must keep curved 3D paths readable; if readability fails in play-test, constrain the playfield toward a mostly-planar layout before abandoning 3D.

## 4. Core loop

1. Inspect the system: planets, gravity fields, targets, hazards.
2. Aim the cannon direction.
3. Charge the shot (see Section 8) and view a short, gravity-aware trajectory hint.
4. Fire one projectile.
5. Watch it fly straight, bend through gravity fields, orbit or slingshot, and impact.
6. Watch collapse and chain reactions resolve.
7. Continue aiming if ammunition remains.
8. Win when the objective is met; otherwise retry.

Initial objective: destroy at least a specified percentage of target mass, or pop all marked targets, on the target planet(s). Later objectives may include hitting targets in sequence, using gravity to reach a shielded far side, or preserving protected objects.

## 5. Core physics: gravity model

The heart of the game is the multi-well gravity integrator.

Each fixed physics step, accumulate acceleration on the projectile from every active gravity well within range:

```
a = Σ_i  G · m_i · (p_i − p) / ( |p_i − p|² + ε² )^(3/2)     for each well i where |p_i − p| < R_i
```

- **Field radius `R_i`**: hard cutoff per well. Outside it, that planet exerts no pull, so distant planets do not tug and empty space is truly zero-G (projectile flies straight).
- **Softening `ε`**: prevents a near-center pass from producing near-infinite force and flinging the projectile away.
- **Surface gravity**: once a projectile or debris lands on a planet, local radial gravity toward that planet's center holds it. Structures resting on a planet are also pulled to that planet's center, so they stand "up" relative to their own planet.
- **Safety clamps**: maximum projectile speed, and an out-of-bounds / max-flight-time timeout that ends the shot cleanly if it escapes the system.

### Integration scheme

Use **semi-implicit (symplectic) Euler** at the fixed timestep: each step compute acceleration `a` at the current position, then `v += a · dt`, then `p += v · dt`. Reasons:

- Symplectic Euler conserves orbital energy far better than plain (explicit) Euler, so slingshots and near-orbits stay stable instead of spiralling out — important because levels rely on predictable curves.
- It is cheap and deterministic given the same `dt` and inputs.
- The projectile is moved by this integrator directly (kinematic-style position updates), not by leaving it to Unity's default rigid-body gravity, so flight matches the preview exactly.

The trajectory preview (Section 7) must run the **identical** scheme, `dt`, and `GravityField` code so the dotted path and the real shot cannot diverge. Full rigid-body simulation (debris, collapse) still uses Unity physics; only the in-flight projectile uses the custom integrator.

2D-vs-3D note: the game is 3D, but the integrator is dimension-agnostic — the same formula runs on 3D vectors.

### Scale and units

Fictional game-scale units, not real astronomy. Fix a scale early so `G`, masses, field radii, and forces are all tuned against the same reference and levels stay comparable:

- **World size**: a level spans roughly **20–60 Unity units** across; the projectile is about **0.3 units** so it reads clearly against planets of radius **1–4 units**.
- **Mass** is an abstract number chosen for feel, not kilograms. Reference: a small planet ≈ **1**, a sun ≈ **8–15**, a black hole ≈ **30+** with a much smaller radius.
- **Field radius `R_i`** defaults to about **3–5× the body radius**, larger for sun and black hole, so fields are visible zones the player can read and only overlap where a level intends a combined pull.
- **`G`** is a single global constant tuned once so that a typical mid-charge shot noticeably curves inside a planet field but still crosses open space in a second or two; it is not derived from real physics.
- **`ε` (softening)** is set near a small fraction of the smallest body radius so it only matters on near-center passes.

These are starting values to be tuned in play-test, but the ratios (sun and black-hole mass far above planets; field radius a small multiple of body radius) should hold so shots behave predictably across levels.

## 6. Level pieces

### Gravity bodies

All bodies feed the same integrator (Section 5). They differ only in mass, field radius, and what happens when the projectile touches them.

| Body | Mass / field | Surface rule | Role |
| --- | --- | --- | --- |
| **Planet** | small to medium | solid; pigs and structures rest on it; some destructible | main playfield, where targets live and hits land |
| **Sun** | large mass, large field | **lethal** — projectile is destroyed on contact | strong attractor you curve around but never touch |
| **Black hole** | huge mass, small radius, tight field | **event horizon** — projectile is lost instantly on contact | strongest bender; tightest curves; skim to slingshot, fall in to waste the shot |

- `GravityWell` holds the field data (mass, radius, field radius `R`, softening) for any body.
- Only planets carry pigs and structures. Suns and black holes are pure gravity tools and hazards; they never hold targets.
- Later flavor: moons (small orbiting wells) and asteroids (moving wells).

### Targets: pigs

- **Pigs** are the discrete targets the player must destroy to clear a level (original art and name to be finalized; "pig" is the working term).
- Pigs sit on planet surfaces, oriented to that planet's local gravity.
- **Damage model**: a pig has hit points and takes damage from a collision when the impact impulse (or relative kinetic energy) exceeds a small threshold; below the threshold it is a harmless nudge. Damage scales with impact strength above the threshold, so a fast direct hit can pop a pig in one blow while a light graze does nothing.
- A pig is destroyed when hit points reach zero, **or** immediately if it leaves the playfield or contacts a sun or black hole (knocked off the planet into a hazard counts as a kill).
- Damage sources are unified: the projectile, dislodged structure blocks, and other debris all apply the same impulse-based rule, so chain reactions and collapses can kill pigs without special cases.
- Threshold and hit points are per-pig level data so designers can place tougher targets.

### Structures

Structures are rigid blocks that protect pigs. A structure can be placed two ways:

- **Resting**: sitting on a planet surface, held down by that planet's local gravity. Standard cover.
- **Orbiting**: circling a body so cover and pigs move, forcing the player to time the shot, not just aim it. Use a **scripted circular orbit**, not free rigid-body simulation: each orbiting object stores a center body, radius, angular speed, and start angle, and is moved kinematically each `FixedUpdate` along that circle. This keeps orbits stable and repeatable (a full physics orbit drifts and desyncs across platforms), while the object still collides normally with the projectile and can be knocked out of orbit into free physics on impact. Orbit parameters are level data so designers place predictable threats.

Structures are knocked away, collapsed onto pigs, or bypassed. Debris and dislodged pigs can fall into a sun or black hole.

### Projectile and goal

- **Projectile**: one type first. Later: split-shot, heavy, explosive.
- **Goal**: destroy all marked pigs (or at least a specified count / target-mass percentage) within limited ammunition. Loss when ammunition runs out with pigs still alive.

## 7. Trajectory preview

- Simulate the same gravity integrator forward `N` fixed steps and draw a dotted path. Start with `N` covering roughly **0.6–0.8 s** of flight (about 30–50 steps at a 60 Hz fixed step), then tune for readability.
- Draw a **dot every few steps** (for example every 3rd sample), not every step, so the dotted line reads as spaced marks rather than a solid arc that gives away the exact path.
- **Recompute** the preview whenever aim or charge changes (and while charging, since force changes each frame); it is a pure forward simulation with no side effects and does not touch the live projectile or scene physics.
- Deliberately **truncate**: cap at `N`, and additionally stop early at the first gravity-field entry or first predicted collision, whichever comes first, so the player sees where the curve begins but not the full solution. Preview length (`N`) is the primary difficulty / assist knob and can be unlocked as an assist.
- Preview and real launch must use identical parameters, `dt`, and the identical `GravityField` and integration scheme (Section 5) so the hint cannot diverge from flight.

## 8. Controls and user experience

### Launch model: timed-charge space cannon

- The player **aims a direction** (where the cannon points).
- The player **holds** to charge. Launch force grows with hold duration, from a minimum up to a maximum.
- A **charge timer** runs while holding. If the player keeps holding until the timer maxes out, the cannon **auto-fires at maximum force**.
- **Releasing early fires immediately with less force**, proportional to how long it was held.
- Net effect: direction is free aim; power is a hold-duration commitment with a hard time cap. This gives a tense "how long do I dare hold" feel distinct from a pure drag-and-release slingshot.

Concrete charge model (starting values, tuned in play-test):

- Charge time `T_charge` from zero to max: about **1.2 s**.
- Force maps from hold duration `t` by `force = lerp(F_min, F_max, clamp01(t / T_charge))`, with `F_min` roughly **25%** of `F_max` so even a tap launches a usable shot.
- The curve is **linear first**; only switch to eased (for example `t/T_charge` squared) if play-test shows the low end feels dead.
- Reaching `T_charge` auto-fires at `F_max` on the same frame; there is no "hold past max" state.
- The charge meter fills over `T_charge` and is the single source of truth read by both the fired shot and the trajectory preview, so the previewed arc always reflects the current charge.
- All three values (`T_charge`, `F_min`, `F_max`) live in the cannon's data (ScriptableObject or serialized fields), not hard-coded in logic, so levels can tune them.

### Mobile

- Drag / swipe on the cannon sets aim direction.
- Press-and-hold builds charge; the on-screen charge meter and timer show current force.
- Release to fire, or hold to timer-max for a full-power auto-fire.

### Desktop

- Mouse aims direction; hold left button (or Space) to charge; release to fire.
- Keyboard offers optional fine aim adjustment.
- Input must not depend on screen resolution or aspect ratio.

Aim changes show immediate cannon movement, a visible charge meter and timer, and a short gravity-aware predicted path. Accessibility options (later): reduced camera shake, color-independent target marking, separate sensitivity, and an option to adjust or disable the auto-fire timer.

## 9. First vertical slice

One complete gray-box level:

- One space cannon on a launch platform or launch planet.
- Timed-charge control: aim direction, hold-to-charge, release-early-for-less-force, timer auto-fire at max.
- Two planets: the launch side and one target planet.
- One gravity field (`GravityWell`) on the target planet that visibly bends the shot.
- Straight flight in empty space, curved flight inside the field.
- Short dotted, gravity-aware trajectory preview.
- One projectile type.
- One destructible structure of roughly 15 rigid pieces on the target planet surface, oriented to that planet's local gravity.
- Limited ammunition.
- Clear win, loss, reset, and next-shot states.
- Android development build and Windows development build.
- Basic sound, camera shake, and impact feedback only after controls and physics work.

### Acceptance conditions

- The target can only be reached by using the gravity curve, not a straight line, on at least the intended solution.
- Same level completes on Android and Windows.
- Input does not depend on screen resolution or aspect ratio.
- Identical aim direction and hold duration produce acceptably similar results on repeated runs.
- The dotted preview matches actual flight for the shown portion.
- Charge timer, auto-fire, and early-release force are understandable without written instructions longer than one screen.
- Level resets without reloading the application.
- Physics settles or times out cleanly; no shot orbits or drifts forever — escaped or endless shots time out and end the turn.
- Mid-range Android target maintains acceptable frame pacing during collapse.
- Automated tests cover the gravity integrator, charge-to-force mapping, and state transitions.

### Explicit non-goals

- Multiplayer.
- Accounts or cloud saves.
- Advertising, in-app purchases, analytics, or live events.
- Hundreds of levels.
- Multiple weapons or shell upgrade trees.
- Real orbital-mechanics accuracy.
- Final art, narrative, cosmetics, or store submission.
- iOS release during the first slice.

Work stops when the acceptance conditions pass. New features wait for play-test evidence.

## 10. Technical shape

Keep components small and owned by one responsibility:

- `GameFlow`: controls Aim, Charging, Fired, Resolving, Won, and Lost states.
- `SpaceCannon`: stores and clamps aim direction; owns charge timer and charge-to-force mapping; produces launch velocity.
- `GravityWell`: per-planet field data (mass, radius, field radius, softening).
- `GravityField`: aggregates active wells and computes accumulated acceleration for a query point; single source of truth for both preview and flight.
- `OrbitalProjectile`: owns flight (integrated against `GravityField`), impact, safety clamps, and one-shot effects.
- `TrajectoryPreview`: forward-simulates `GravityField` and displays a sampled, deliberately truncated path.
- `PlanetSurface`: marks a body and provides local radial gravity for resting objects and debris.
- `DestructiblePiece`: wraps rigid-body state and score contribution.
- `LevelGoal`: evaluates victory after physics settles.
- `LevelDefinition`: stores ammunition, target threshold, planet and structure prefab references.
- `CameraController`: frames the system and follows the shot without affecting simulation.
- `InputAdapter`: maps touch and desktop input onto the same aim/charge/fire commands.

Use Unity scenes for boot and gameplay. Use prefabs for planets and structures, and ScriptableObjects only for level data that genuinely varies. Avoid service locators, dependency-injection frameworks, networking layers, and speculative platform abstractions.

Gameplay code must not call advertising, store, analytics, or operating-system APIs directly. Platform-specific SDKs sit behind thin adapters only when one is introduced.

## 11. Physics approach

- Use a fixed physics timestep. Run the gravity integrator in `FixedUpdate`.
- Compute preview and launch from the same parameters and the same `GravityField` code.
- Use per-well field radius cutoffs so empty space is zero-G and distant planets do not pull.
- Use distance softening `ε` so near-center passes do not fling the projectile.
- Clamp maximum projectile speed; enforce an out-of-bounds and max-flight-time timeout.
- Give each planet local surface gravity so structures rest correctly on curved surfaces; sleep or remove settled debris.
- Use collision layers to prevent irrelevant contacts.
- Use deterministic random seeds for break effects, accepting that full rigid-body simulation may differ slightly across platforms.
- Define tolerant win conditions. Never require one fragment to land at an exact coordinate.

**Resolution / "settled" definition** (drives the `Resolving → Won/Lost` transition): after a shot ends (projectile impacts, is destroyed by a hazard, or times out), the level is considered settled when either every active rigid body's speed stays below a small threshold for a short continuous window (for example under ~0.05 units/s for ~0.5 s), or a hard resolve-timeout (for example ~6 s) elapses — whichever comes first. `LevelGoal` evaluates win/loss only at that point, never mid-collapse, so a pig briefly clipped but not destroyed is judged fairly.

If cross-platform replay accuracy becomes necessary, record resolved transforms or replace selected interactions with deterministic custom logic. Do not build that system before a real requirement exists.

## 12. Performance budget

- Roughly 15 to 20 active structural rigid bodies in the first level.
- A small number of gravity wells per level (start with one to three).
- Gravity integration is cheap: a bounded loop over active wells per projectile per step.
- Hard caps established through profiling before content production.
- Minimal transparent materials and real-time lights; baked lighting where possible.
- Object pooling only after profiling proves repeated allocation is material.
- Quality tiers for shadows, effects, debris, and resolution scale.
- Primary test device represents a mid-range Android phone, not a flagship.

## 13. Development milestones

### Milestone 0: bootstrap

- Pin Unity LTS version.
- Create URP 3D project.
- Configure Android and Windows targets.
- Add Input System and test framework.
- Create one automated build command per target.

### Milestone 1: playable gray box

- Implement `GravityField` multi-well integrator with field cutoff, softening, and safety clamps.
- Implement `SpaceCannon` timed-charge control (aim, hold-to-charge, early release, timer auto-fire).
- Fire `OrbitalProjectile` integrated against `GravityField`.
- Implement gravity-aware `TrajectoryPreview` sharing the integrator.
- Build the two-planet, one-well slice with one destructible structure on a planet surface.
- Add game-flow states and retry.
- Produce Android and Windows builds.

### Milestone 2: game feel

- Tune camera framing for curved 3D paths, charge-meter feedback, audio, particles, hit pause, and collapse timing.
- Run a five-player usability test.
- Measure whether players understand the charge timer, auto-fire, and reading the gravity curve.
- Remove or revise controls that require explanation.

### Milestone 3: content pipeline

- Create planet and structure prefab rules and a level layout convention.
- Add level validation for reachable solutions, unsupported pieces, ammunition, and goals.
- Build 10 varied levels using existing mechanics (single well, multiple wells, slingshot around a planet, shielded far side).
- Add one new mechanic (for example a black hole or a moving asteroid) only if the first 10 levels become repetitive.

### Milestone 4: Android alpha

- Profile representative devices.
- Add save progression and settings.
- Add crash reporting and analytics only after privacy review.
- Test install, suspend, resume, rotation lock, and interrupted audio.

### Milestone 5: cross-platform release work

- Validate iOS input, safe areas, performance, signing, and store rules.
- Validate desktop resolution, window mode, keyboard, mouse, and controller.
- Add platform services through adapters without changing core gameplay.

## 14. Testing

- Unit tests: gravity integrator (single and multiple wells, field cutoff, softening), charge-to-force mapping and timer auto-fire, aim clamps, and goal thresholds.
- Play-mode tests: charge/fire state transitions, retry, timeout of escaped shots, win, and loss.
- Golden level: fixed aim and hold inputs checked after physics or engine upgrades.
- Device smoke tests: install, launch, complete level, suspend, resume, and relaunch.
- Performance capture: worst collapse on the minimum supported Android profile.

Physics tests use ranges and tolerances, not exact fragment coordinates.

## 15. Main risks

| Risk | Response |
| --- | --- |
| Slingshot feels like an Angry Birds copy | Use the timed-charge cannon control, original presentation, and multi-well puzzle design rather than the brand. |
| Gravity flings the projectile to infinity | Distance softening, per-well field cutoff, max-speed clamp, and out-of-bounds timeout. |
| Curved preview solves every puzzle | Truncate preview length; unlock more only as an assist. |
| Curved 3D paths are hard to read | Tune camera framing; constrain toward a mostly-planar layout before dropping 3D. |
| Surface-anchored structures jitter on curved planets | Sleep resting bodies; tune local surface gravity, friction, and colliders. |
| Physics differs across platforms | Tolerant goals, fixed timestep, seeded effects, and device tests. |
| Destruction causes mobile frame drops | Limit debris, simplify colliders, use quality tiers, and profile early. |
| Scope expands into live-service work | Do not add services until the vertical slice passes player tests. |

## 16. Reference: Angry Birds Space and lessons

Angry Birds Space established the mainstream gravity-well slingshot: planets with gravity fields, straight flight in empty space, and curved flight near planets. The core idea of reading a bent path remains fun and viable.

Product lesson: the projectile-and-gravity idea is strong on its own, but the Angry Birds franchise now depends on a large brand, ongoing content, events, and cross-media reach. Cannon should first prove its distinct timed-charge control and a satisfying single-level gravity puzzle. It should not attempt franchise-scale live operations during prototype development.

## 17. Next decision

Confirmed choices:

1. 3D planets and gravity.
2. Timed-charge space cannon: aim direction, hold to charge, release early for less force, timer auto-fires at maximum.
3. Multi-well gravity integrator shared by preview and flight.
4. Unity as the cross-platform engine.

After approval, bootstrap only Milestone 0 and prove Android and Windows builds before implementing gameplay. Then Milestone 1 proves the gravity integrator, the timed-charge cannon, and the curved preview in one gray-box level.
