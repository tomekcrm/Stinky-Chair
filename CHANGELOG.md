# Stinky Chair Changelog

## 1.1.0

Watering-can washing and barrel shower release.

Highlights:

- Added server-side watering-can washing: direct right-click on a stench-enabled player or test prop starts a wash session, continuously draining the held watering can and reducing target stench at the swimming-cleaning rate
- Wash sessions stop automatically when the can runs dry or the target moves out of range
- Added stream particles from the watering can to the target while actively pouring, and white cleaning particles when stench is actually removed
- Added dedicated wash audio played server-side so nearby players can hear it
- Fast-cleaning particles now also appear during strong natural cleaning such as swimming when reduction is faster than rain or standing in water
- Added `/stenchpropspawn <0-100>` to spawn a vanilla straw dummy with stench behavior attached as a stationary test target
- Added `/smrodprop` and `/stenchprop` commands to set stench on the currently looked-at prop

- Added the barrel shower: a craftable 3-block tall structure built from barrels that stores up to 50 litres of water and washes the player standing inside
- The shower is filled by right-clicking with a water bucket or watering can; right-clicking with an empty hand toggles the valve on and off
- The active shower drains water continuously, spawns falling water droplet particles from the showerhead, and plays a looping shower sound; three distinct sounds play when the tank runs dry
- A flat water surface in the top barrel renders dynamically and rises with the fill level, using the custom barrel water texture from the block atlas
- Crafted from three vanilla barrels stacked in a column

## 1.0.2

Fix release for the watering-can probe patch.

Highlights:

- Fixed the vanilla watering can patch target so the stage-0 probe actually hooks the fired clay watering can in-game

## 1.0.1

Maintenance and testing release focused on watering-can research tooling and debug usability.

Highlights:

- Added a stage-0 watering can probe for `OnHeldInteract*` and raycast diagnostics
- Added a dedicated watering-can patch so vanilla cans can be inspected in-game
- Split the debug overlay into separate client-side sections:
  `Core`, `Sources`, `Effects`, and `Watering Can Probe`
- Added new client config toggles for selective debug visibility
- Fixed release packaging so generated `.zip` files are real zip archives

## 1.0.0

Initial public release of `Stinky Chair`.

Highlights:

- Full `0-100` hygiene system with `5` odor stages
- Activity, weather, clothing, and water-based hygiene changes
- Stage `5` drifter behavior changes
- Animal detection scaling through `animalSeekingRange`
- HUD bar, dirt overlay, particles, and fly buzz audio
- Temporal pressure through the vanilla temporal stability system
- Client UI settings through `ConfigLib`
- Admin commands for hygiene testing and moderation
