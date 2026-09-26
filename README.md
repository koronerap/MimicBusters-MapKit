# Mimic Busters Map Kit

Make your own maps for **Mimic Busters** in Unity, then walk them in the game with one click.

[Türkçe rehber](README.tr.md)

The kit is a Unity project with the same map code the game uses. You build a map as an ordinary Unity
scene, bake its lighting in Unity, and the kit checks it against the game's rules, packs it into one file
that Windows, Linux and macOS all open, installs it in your game and starts the game on it.

Maps made with this kit can be played in **Standard** (Hunters and Modelers, and its variants
Companion and Infestation, which use the same markers), **Practice** (free building, no rounds),
**Deathmatch** (everyone armed, no sides) and **Gartic** (one player models, the others guess, in turns).

## What you need

- **Unity 6000.3.8f1**, exactly this version, from Unity Hub. Maps are Unity asset bundles, and the game
  can only read bundles made by its own Unity version. No extra modules: the kit packs with what comes with
  the editor.
- **Mimic Busters**, installed through Steam, to test your maps.

## Getting started

1. Download the kit: **Code > Download ZIP** on GitHub, or a zip from **Releases**. Unzip it somewhere
   with a short path.
2. In Unity Hub: **Add > Add project from disk**, pick the unzipped folder, open it with 6000.3.8f1. The
   first import takes a few minutes.
3. **Map Kit > New Map**. Give the map a name, tick its modes, keep **Add an example room** on for your
   first map. The kit makes a scene in `Assets/Maps/<Name>/` with everything the game needs already in
   place: the map info, spawn points, the map bounds, a probe volume and the right lighting settings.
4. **Window > Rendering > Lighting > Generate Lighting**. The first bake takes a while.
5. **Map Kit > Validate**. It should say the game will open the map.
6. **Map Kit > Pack and Test**. The first time, check the **Game** section: the kit looks for the game
   in your Steam libraries; if it is somewhere else, press **Choose...** and pick the game's executable.
   Then press **Pack and Test**. The game starts straight into a private Practice session on your map.

Then make it yours: delete the example room, bring in your own models, and repeat steps 4 to 6.

## Building a map

**Geometry.** Import models (FBX, OBJ, Blender files) and textures as usual and place them in the scene.
Use the Universal Render Pipeline shaders: **Lit**, **Simple Lit**, **Unlit** or **Baked Lit**. Give
everything players walk on or bump into a collider. Mark static geometry **Static** so it gets baked
light.

**Map info.** The `Map` object in the new scene carries a **Kit Map Descriptor**: the map's name,
description, author and modes, as the game shows them. There must be exactly one.

**Spawn points.** A spawn point is an empty object with a **Map Marker**. Its inspector asks two things
in order: **1. Modes**, which game modes use it, and **2. Kinds**, what it is for. Under them it says
which mode uses the marker as what, and warns about a kind none of the ticked modes reads:

| Kind | Used for |
|---|---|
| HiderSpawn | Where Modelers start a round (Standard). Also where Practice players appear. |
| HunterSpawn | Where Hunters come out when the hunt begins (Standard). They spend the preparation time in the game's own Hunter room. |
| LobbySpawn | Where everyone waits while the lobby fills up. Optional: without it the lobby uses the HiderSpawns. |
| HunterDoor | Optional. An object that stays shut during the lobby and preparation and opens when the hunt begins. Only the first one counts. |
| ModelerSpawn | Practice: where players appear, like HiderSpawn and LobbySpawn. Gartic: where the player whose turn it is models. |
| ArenaSpawn | Optional. A 1v1 duel position (Standard). The game's Hunter room has its own. |

Standard needs at least one HunterSpawn and one HiderSpawn. Practice needs a HiderSpawn, LobbySpawn or
ModelerSpawn with Practice ticked. Deathmatch uses every HunterSpawn, HiderSpawn and LobbySpawn with
Deathmatch ticked and puts a player at the one farthest from everyone, so give it at least six, spread
out. Gartic puts the guessers on LobbySpawns (HiderSpawns when there are none) and the player whose turn it
is on one ModelerSpawn, all with Gartic ticked: put that one where everyone can see it. Put a few of each, spread out, on the floor, facing into the map. The line drawn from the marker
shows which way the player faces. A marker with no mode ticked is used by no mode.

**Map bounds.** The **Map Bounds Volume** is a box around the whole play area. When it is baked, the
game knows where players can be, and brings back anyone who falls out of the map. Make the box cover
everywhere a player can reach, with room above for jumps and roofs, then **Map Kit > Bake Map Bounds**.
Bake again whenever you move walls, floors or spawn points. If the validator says the play area reaches
the box's wall on a closed map, there is a hole where it says.

The bake fills the air from your spawn points outwards, in half-metre cells. A room players only reach
by teleport is never found that way: add a point in it to the volume's **Seeds**, and one for the rest
of the map too, because with Seeds filled in the fill starts from those alone. The validator says so if
a spawn point or a teleport's destination ends up outside. A gap only one cell across (a slot over a
wall, a narrow crack beside a platform) is reported as a possible leak: make gaps either tight or at
least a metre.

**Lighting.** Keep the lighting settings the new map came with: Mixed lighting in **Shadowmask** mode,
**Directional** lightmaps, **Realtime Global Illumination off**. The game carries shaders for this
setup only. Lights are **Mixed** or **Baked**; more than four realtime lights with shadows slow the game
down. Fog, if you want it, must be **Exponential Squared**. The **Adaptive Probe Volume** lights players
and Modelers so they match the map; bake it with the rest (it is part of Generate Lighting). Bake again
after every change to geometry or lights, otherwise the game shows the old light.

**Post-processing.** A **Volume** may carry colour and tone effects only: Bloom, Tonemapping, Color
Adjustments, White Balance, Lift Gamma Gain, Shadows Midtones Highlights, Split Toning, Channel Mixer,
Color Curves, Vignette.

## Map logic

You can make a map interactive the way Hammer does: a platform that moves when a button is pressed and
comes back a while later, a sound, a zone that hurts. No code is written; everything is wired in the
Inspector, and the game does the work.

Add these from **Add Component > Map Kit > Logic**:

| Component | What it does |
|---|---|
| Map Button | A button players press with the interact key. Needs a collider. Who may press it, a cooldown, a prompt. |
| Map Mover | Moves and turns the object and its children along a list of stops, carrying the players on it. Return After: comes back by itself that many seconds after reaching the last stop. PingPong and Loop run on their own. |
| Map Trigger | An invisible box: events when a player enters or leaves, and for the first in and the last out. |
| Map Teleport | Sends whoever walks into its box to a destination object, facing where it faces. |
| Map Hurt Zone | Hurts whoever is inside every interval, or kills at once. Nobody scores a death the map causes. |
| Map Sound | A sound where it stands: once or looping, fading with distance or the same everywhere. |
| Map Effect | One of the game's own effects: smoke, steam, fire, sparks, dust, drips. Colour, size, amount. |
| Map Toggle | Shows or hides the object and its children (what is drawn, what is solid, lights). |
| Map Relay | Passes one input on to several outputs, each with its own delay. |
| Map Timer | Raises an event every interval while enabled. |
| Map Counter | Counts, and raises an event when the count reaches its target. |
| Map Round Events | Round started, hunt started, round ended. |

**Wiring.** Every component has a list of **Outputs**: an *event* (Pressed, ArrivedAtEnd, Entered...),
a *target* (another logic component, dragged in), an *input* (Forward, Back, Play, Enable, Disable...),
a *delay* and, where the input takes one, a number (a stop, an amount). The inspector offers only the
events the component raises and the inputs its target takes, marks a connection that cannot work in
orange, and lists underneath who is wired into the component. **Map Kit > Logic** shows every
connection on one page.

Example: the button's *Pressed* event to the lift's *Forward* input. The lift has one stop (1.8 m up)
and Return After 4: press, it rises, four seconds later it comes down. **New Map**'s example room comes
with exactly this, plus a motor sound (the lift's *Started* to a Map Sound's *Play*) and, in the other
room, a heater: one button toggles a Map Hurt Zone and a fire Map Effect at once.

**Whoever set it off.** An event caused by a player (a press, walking into a trigger, being hurt)
remembers that player along the whole chain, delays included. Two inputs act on them:
*HurtActivator* on a hurt zone gives them its damage once (or the output's number instead), and
*TeleportActivator* on a teleport sends them to its destination. A trap door button that sends only the
presser down, a trigger that hurts only the one who stepped in: wire it, no box needed. A teleport used
only this way should start disabled, or its own box sends people too.

**Seeing it before packing.** In the Scene view each output's line is labelled with its event and input.
A selected Map Mover has a handle on every stop, and under Preview a see-through copy of it that you can
slide along the path or play the way the game plays it, with the time the journey takes.

At the start of every round everything goes back to how you wrote it. A component with Start Enabled
off does nothing until an *Enable* input arrives.

**Mind these.**

- Nothing that moves may be **Static**: static batching freezes it in place and its baked light stays
  behind. Probes light what moves.
- A moving platform pushes a player in its way; when it cannot, it does what **When Blocked** says:
  turns back, crushes, or carries on.
- A player who jumps off a moving platform keeps its sideways speed until they land, so a jump on a
  moving ferry comes down on the ferry.
- A teleport's destination is checked like a spawn point: nothing solid there, a floor under it. One
  inside a teleport's own box sends the player on again a second later; the validator warns.
- Sounds: at most 2 minutes each, mono or stereo, up to 48 kHz; all of a map's sounds at most 32 MB in
  memory. Load long ones **Compressed In Memory** or **Streaming**. Sounds go through the player's
  effects volume.
- A loop with no delay (A triggers B and B triggers A at once) is cut at 32 steps in the game, and the
  validator counts it as an error. Give one output in the loop a delay. Only inputs that make their
  target act there and then close such a loop (a press, a relay's trigger, a counter's change, a mover
  sent somewhere): a button that switches itself off, or a tune that starts another when it finishes,
  is fine.
- Limits: 256 logic components, 16 outputs each, 200 inputs a second for the whole map.

**Trying it.** Pack and Test opens the map in Practice, which has no Hunters or Modelers. In the
developer console, `maprole hunter` or `maprole hider` lets you try role filters, `maplogic` lists every
component's state, and `mapfire <number> <input>` sends an input by hand.

### The example map: Logic Playground

**Map Kit > Examples > Logic Playground** builds a greybox map where every logic component is at work,
in `Assets/Maps/LogicPlayground/`. Bake its lighting when it asks, then Pack and Test it like any map,
or open its objects to see how they are wired (**Map Kit > Logic** lists every connection). Its colours
say what things are: grey is the world, orange is something you press, yellow moves, red hurts, blue
teleports, purple appears and disappears. The ceiling is invisible: a collider with nothing drawn,
which closes the open courtyard for the map bounds bake.

Nine rooms on a 3 by 3 grid, 36 m across. Everybody starts in the middle one.

| Room | What it shows |
|---|---|
| 00 Hub (middle) | Map Round Events: a chime when a round starts; a siren and a red smoke beacon when the hunt starts; fireworks when the round ends (a relay playing sparks and a sound with delays). A generator humming on a loop from the start. |
| 01 Doors (south) | A sliding door that closes by itself four seconds after it opens (Return After), turns back if someone is in the way, and buzzes. A trigger in front of it that presses its button for Hunters (Press). A swinging door that either button turns open or shut (Reverse). |
| 02 Elevator (north) | Three floors: call buttons on each, buttons riding on the car (Go To Stop with the floor as the number), a motor when it starts, a chime at every floor. |
| 03 Platforms (east) | A pit that kills whoever falls in. A ferry going to and fro over it (PingPong) with buttons to stop and start it (Pause, Resume); a carousel turning in the middle (Loop), switched on and off (Toggle). Jump from the ferry: you keep its speed. |
| 04 Hazards (west) | A crusher (When Blocked: Crush). Fire vents lit at random times: a timer triggers a relay that switches three hurt zones and fires on, and off again a second and a half later. A steam pipe that scalds. A power switch that pauses it all. |
| 05 Vault (north-east) | Three runes, each counted once (Add, and the rune switches itself off); at three the vault's button unlocks. Pressed while locked, it buzzes. A decoy rune takes one back (Subtract); the Hunters' master key sets the count at once (Set Value); a reset closes everything again (Reset Count, Lock, Back, Enable). |
| 06 Teleports (north-west) | Two pads sending players to each other. A room with no door: a pad takes you in, a button inside takes only you out (Teleport Activator on a switched-off teleport). A Modelers' shortcut to the south-east room's ledge. |
| 07 Triggers and Toggles (south-east) | A pressure plate: while somebody stands on it (Occupied, Emptied) a bridge is there to the ledge. The bridge's railing is a toggle inside the bridge's toggle. An alarm that rings once a round when a Hunter comes in. A hidden nook behind a panel. |
| 08 Sounds and Effects (south-west) | Every effect on a pedestal with its button. A jukebox of two tunes, each starting the other when it finishes (Finished). An announcement heard all over the map, once a round. A button that hurts only whoever presses it (Hurt Activator). |

## The rules

The game reads a map before it lets Unity open it, and refuses anything outside these rules. **Map Kit >
Validate** checks all of them in the editor; **Pack and Test** checks the packed files again with the
game's own code.

- **Components:** Mesh Filter, Mesh Renderer, LOD Group, Box, Sphere, Capsule and Mesh Colliders, Light,
  Reflection Probe, Light Probe Group, Occlusion Area and Portal, Adaptive Probe Volume, Volume, and the
  kit's own components (Map Marker, Map Mode Object, Map Bounds Volume, Penetrable Surface, Probe Volume
  Region, Kit Map Descriptor) and the map logic components. No scripts of your own, and no cameras, audio
  sources, particle systems, UI, video, terrain, animation or physics bodies. Sound and effects come
  through Map Sound and Map Effect.
- **Shaders:** Universal Render Pipeline Lit, Simple Lit, Unlit, Baked Lit. Sky: Skybox Procedural,
  Cubemap, 6 Sided or Panoramic.
- **Layers:** Default, Ignore Raycast, MapBlocker (invisible walls), GarticMapBlocker.
- **Lighting:** as above. At most 64 lights.
- **Size:** at most 256 MB for the package, 512 MB of textures, 8192 pixels on a texture's longest
  side, 3 million triangles, 6 million vertices and 10,000 renderers.
- **Name:** the map's id is its scene name in lower case: letters, digits, `-` and `_`, up to 23
  characters. Scene names the game uses itself (Startup, Menu, Game, HunterRoom and others, and anything
  starting with `Map_` or `GameMode_`) are not allowed.

Errors mean the game will not open the map. Warnings are worth a look but do not stop it.

## Playing with friends

Everyone in the session needs exactly the same map files: the game compares them before it loads the map.
The package is the same for every system, so pack the map once and send your friends the folder from
`Builds/Maps/<id>/` in the kit (zip it). Each of them puts the folder, unchanged, into their game's maps
folder:

| System | Folder |
|---|---|
| Windows | `%USERPROFILE%\AppData\LocalLow\DevAdam\MimicBusters\Maps\Local\` |
| macOS | `~/Library/Application Support/DevAdam/MimicBusters/Maps/Local/` |
| Linux | `~/.config/unity3d/DevAdam/MimicBusters/Maps/Local/` |

The result looks like `...\Maps\Local\<id>\map.json` with `map.any.bundle` next to it. The map
then appears in the game's map picker under **Community**. Steam Workshop support will come later, from
inside the game.

## When something goes wrong

- **The kit says the game would refuse the map.** The reasons are listed in the Pack and Test window. The
  Console has the full report, and so does `Builds/Maps/<id>.report.txt`.
- **The game says the map could not be opened.** Turn on **Settings > Gameplay > Developer console** in
  the game, open the console with the key under Esc, and type `kitmaps`: it lists your maps and why one
  cannot be played. `kitinspect <id>` reads a map without loading it.
- **The map looks too dark or has the wrong light.** Bake the lighting again, then pack again. If the
  validator says the scene uses another scene's lighting data (this happens to copied scenes), use
  **Map Kit > Tools > Detach Copied Lighting**, then bake.
- **Players fall through the floor or spawn inside walls.** Check the colliders, move the spawn point,
  and bake the map bounds again.
- **The game reports that the map was made with an older kit.** Download the new kit, open your map in
  it, and pack again.

## License

See [LICENSE.md](LICENSE.md). Mimic Busters and its logo belong to DevAdam and are not part of the
license.
