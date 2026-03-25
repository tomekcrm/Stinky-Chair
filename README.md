# Stinky Chair

`Stinky Chair` is a hygiene and odor survival mod for Vintage Story `1.21.6`.

It turns dirt, sweat, rain, water, armor, exhaustion, smell, and player presence into a readable survival loop. As your hygiene changes, the world reacts: drifters behave differently, animals notice you at different distances, your HUD becomes dirtier, flies gather around you, and at the worst stage your odor starts to feel like a local temporal disturbance.

This mod is built as an immersion-focused survival feature, not a joke meter. The goal is to make grime feel like part of the world.

Release version: `1.1.2`

## Features

- A full `0-100` hygiene value mapped to `5` odor stages
- Dynamic gain from activity, stamina drain, movement, clothing, weather, and water exposure
- Custom drifter behavior at the worst stage
- Global animal detection tuning through `animalSeekingRange`
- Stage-based HUD bar, dirt overlay, particles, and fly buzz audio
- Watering-can washing for other players and stench test props
- Barrel shower block: fillable with water, washes the player standing inside, shows a dynamic water level
- Extra wash particles during fast cleaning, including swimming and active washing
- Temporal pressure at stage `5` through the vanilla temporal stability system
- Client-side UI settings through `ConfigLib`
- Admin command support for testing and moderation
- Multiplayer-friendly behavior, including shared fly buzz audio for nearby players

## Core Gameplay

Your character gradually becomes dirtier over time.

What pushes hygiene upward:

- passive buildup over time
- sprinting, movement, and stamina usage
- exhaustion
- heavier clothing and armor

What brings hygiene down:

- swimming
- standing in water
- rain, if the player is exposed to the sky
- being washed by another player using a fired clay watering can
- standing inside an active barrel shower

The watering-can wash flow is session-based on the server:

- direct right-click on a valid stench target starts washing immediately
- continuing to pour keeps the same wash session alive
- vanilla block watering remains untouched

The default stage thresholds are:

- `0-9.99` -> Stage 1
- `10-29.99` -> Stage 2
- `30-69.99` -> Stage 3
- `70-89.99` -> Stage 4
- `90-100` -> Stage 5

The intended rhythm is:

- Stage `1-2`: mostly clean, mild gameplay impact
- Stage `3`: the stealth sweet spot for animal detection
- Stage `4`: visibly dirty, audible flies, but still no sanity loss
- Stage `5`: full reek, drifter reaction changes, and temporal pressure begins

## Barrel Shower

The barrel shower is a 3-block tall structure that provides a fixed wash point in a base or camp.

How it works:

- crafted from three vanilla barrels stacked in a column
- right-click with a water bucket or watering can to fill it (up to 50 litres)
- right-click with an empty hand to toggle the valve on or off
- standing inside an active shower reduces stench at the same rate as swimming
- the tank drains slowly while active; three different sounds play when it runs dry
- a flat water surface in the top barrel shows the current fill level and rises as the tank fills

The shower has no active effect when the valve is off or the tank is empty. It can be broken from any section and drops as a single item.

## AI Behavior

### Drifters

Stage `5` changes how standard drifters treat the player:

- they ignore the player by default
- they only become hostile after being provoked
- the behavior is tracked per drifter and per player, so it stays consistent in multiplayer

### Animals

Animals use a global detection modifier through the player stat `animalSeekingRange`.

That means the system affects both:

- predators that detect and pursue the player
- passive animals that flee from the player

The default curve is:

- Stage `1`: `x1.15`
- Stage `2`: `x0.80`
- Stage `3`: `x0.35`
- Stage `4`: `x0.90`
- Stage `5`: `x1.20`

So stage `3` is the stealthiest hygiene state, while very clean and very filthy characters are easier for animals to notice.

## Temporal Stability

Stage `5` feeds into the vanilla temporal stability system.

Current default behavior:

- radius: `2.0`
- mode: `temporalaura`
- stage `4`: no sanity loss
- stage `5`: local temporal pressure

The system is hybrid:

- it applies a local odor-based temporal penalty
- it can neutralize strongly positive local stability down to a light instability floor
- if that still is not enough to reach the intended minimum pressure, the mod adds only the missing amount as a small fallback drain

This produces visible vanilla feedback:

- the temporal gear should react like it does in unstable areas
- nearby players can also be affected if they stand too close to a stage `5` player

## Audio and Visual Feedback

### HUD

The hygiene bar supports:

- a pivot-style presentation around the stage `2 -> 3` break
- optional client-side repositioning
- live reload through `Mods Settings`

### Overlay and Particles

- dirt overlay becomes noticeable at higher stages
- particles begin at stage `3`
- stage `3` is light
- stage `4` is visible
- stage `5` is intense

### Fly Buzz

Stage `4` and `5` can play spatial fly buzz audio.

Default behavior:

- stage `4`: rare
- stage `5`: more frequent
- sound is played server-side so nearby players can hear it too

## Optional Mod Integrations

`Stinky Chair` works on its own, but it supports:

- `ConfigLib`
- `Vigor`
- `HydrateOrDiedrate`

## Commands

Admin and server operators can set hygiene directly:

- `/smrod <player> <0-100>`
- `/stench <player> <0-100>`
- `/stenchpropspawn <0-100>`
- `/smrodprop <0-100>`
- `/stenchprop <0-100>`

These are useful for testing visuals, audio, AI, temporal effects, and the watering-can wash flow without waiting for natural buildup.

`/stenchpropspawn` creates a stationary vanilla straw dummy with stench behavior attached.
`/smrodprop` and `/stenchprop` set the value on the prop you are currently looking at.

## Configuration

The mod uses two config files:

- `VintagestoryData/ModConfig/stench.json`
  - server and gameplay settings
- `VintagestoryData/ModConfig/stench-client.json`
  - client UI settings only

The client file controls:

- HUD visibility
- debug overlay visibility
- overlay visibility
- manual HUD offsets

## Installation

1. Copy the mod zip into `VintagestoryData/Mods` for singleplayer or client use.
2. Copy the same zip into `VintagestoryData/ServerMods` for multiplayer servers.
3. Restart the game or server.

The same release zip is used on both client and server.

## Compatibility

- Built for Vintage Story `1.21.6`
- Designed for multiplayer
- Client UI settings reload without reconnecting

Technical note:

- the internal mod id remains `stench` for save, config, and compatibility continuity

## Credits

The drifter freeze-style AI structure is inspired by the `cats` mod by `G3rste and contributors`.

Formal attribution and license details are included in `THIRD_PARTY_NOTICES.md`.
