# Mimic Busters Map Kit

Make your own maps for **Mimic Busters** in Unity, then walk them in the game with one click.

[Türkçe rehber](README.tr.md)

The kit is a Unity project with the same map code the game uses. You build a map as an ordinary Unity
scene, bake its lighting in Unity, and the kit checks it against the game's rules, packs it into one file
that Windows, Linux and macOS all open, installs it in your game and starts the game on it.

Maps made with this kit can be played in **Standard** (Hunters and Modelers, and its variants
Companion and Infestation, which use the same markers), **Practice** (free building, no rounds) and
**Deathmatch** (everyone armed, no sides).

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
| ModelerSpawn | Practice: where players appear, like HiderSpawn and LobbySpawn. |
| ArenaSpawn | Optional. A 1v1 duel position (Standard). The game's Hunter room has its own. |

Standard needs at least one HunterSpawn and one HiderSpawn. Practice needs a HiderSpawn, LobbySpawn or
ModelerSpawn with Practice ticked. Deathmatch uses every HunterSpawn, HiderSpawn and LobbySpawn with
Deathmatch ticked and puts a player at the one farthest from everyone, so give it at least six, spread
out. Put a few of each, spread out, on the floor, facing into the map. The line drawn from the marker
shows which way the player faces. A marker with no mode ticked is used by no mode.

**Map bounds.** The **Map Bounds Volume** is a box around the whole play area. When it is baked, the
game knows where players can be, and brings back anyone who falls out of the map. Make the box cover
everywhere a player can reach, with room above for jumps and roofs, then **Map Kit > Bake Map Bounds**.
Bake again whenever you move walls, floors or spawn points. If the validator says the play area reaches
the box's wall on a closed map, there is a hole where it says.

**Lighting.** Keep the lighting settings the new map came with: Mixed lighting in **Shadowmask** mode,
**Directional** lightmaps, **Realtime Global Illumination off**. The game carries shaders for this
setup only. Lights are **Mixed** or **Baked**; more than four realtime lights with shadows slow the game
down. Fog, if you want it, must be **Exponential Squared**. The **Adaptive Probe Volume** lights players
and Modelers so they match the map; bake it with the rest (it is part of Generate Lighting). Bake again
after every change to geometry or lights, otherwise the game shows the old light.

**Post-processing.** A **Volume** may carry colour and tone effects only: Bloom, Tonemapping, Color
Adjustments, White Balance, Lift Gamma Gain, Shadows Midtones Highlights, Split Toning, Channel Mixer,
Color Curves, Vignette.

## The rules

The game reads a map before it lets Unity open it, and refuses anything outside these rules. **Map Kit >
Validate** checks all of them in the editor; **Pack and Test** checks the packed files again with the
game's own code.

- **Components:** Mesh Filter, Mesh Renderer, LOD Group, Box, Sphere, Capsule and Mesh Colliders, Light,
  Reflection Probe, Light Probe Group, Occlusion Area and Portal, Adaptive Probe Volume, Volume, and the
  kit's own components (Map Marker, Map Mode Object, Map Bounds Volume, Penetrable Surface, Probe Volume
  Region, Kit Map Descriptor). No scripts of your own, and no cameras, audio, particles, UI, video,
  terrain, animation or physics bodies.
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
