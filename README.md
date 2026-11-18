# Wardrobe and tack overhaul for Horses of Hoofprint Bay

Gives your rider an outfit and each horse a set of tack for every kind of ride in [Horses of Hoofprint Bay](https://store.steampowered.com/app/3727090/): training, trail rides, lessons, Caprilli, jumping and cross country. When a ride starts, the mod puts on the set you saved for it. It also adds halters with lead ropes in five colours, plus custom colours you pick yourself.

## Outfits

Open the wardrobe in the main house and pick Normal Outfit or Competition Outfit. A column of tabs appears next to the clothes:

- Normal Outfit: Default, Training, Trail rides
- Competition Outfit: Default, Caprilli, Jumping, Cross country

Default is the game's own outfit. Click another tab, dress the rider, then press Save at the bottom of the paper. That outfit is used for that kind of ride from then on. A tab you never saved uses Default, and so would a new kind of competition if the game adds one.

Lessons have no outfit tab, because a student rides in lessons and your rider isn't shown.

## Tack

On a horse's screen, open Edit Tack. The tabs are Default, Halter, Training, Trail rides, Lessons, Caprilli, Jumping and Cross country. Pick one, tack up the horse and press Save. Each horse keeps its own sets. Close does the same as the game's Save & Close.

The game's own tack stays as it was. Default goes back on when you close the cabinet, switch to another horse, or the game saves.

## Halters

Every horse starts with the blue halter. Red, yellow, green and purple halters with lead rope cost 40 each in the equipment store, in the ear bonnet list. Put one in the basket and pay at the counter like any other item. In Edit Tack, the Halter tab shows the horse as it stands in the stable, with the halter and lead rope on, so you can see the colour before you pick it.

Once the store sells the game's custom colour ear bonnet, the ear bonnet list also has a Custom Color Halter with Lead Rope for 60. You choose its colour in the store's colour picker, and you can buy each colour once. Every colour you buy gets a button on the Halter tab.

On walking trails such as the Forest Walk, where your rider leads the horse, the lead rope has the colour of the horse's halter.

## Settings

The only setting is Enabled, which switches the whole mod on or off. With [Mod Manager](https://github.com/LaylasLethalParadox/HoofprintBay-ModManager) installed it is in the Mods menu; without it, it is in `BepInEx\config\hoofprintbay.wardrobeandtackoverhaul.cfg`.

## Install

You need BepInEx 6 bleeding edge build 793 for IL2CPP (Windows x64). BepInEx 5 and the older 6.0.0-pre builds can't load this game. Mod Manager is optional.

1. Install BepInEx as described on the [Mod Manager page](https://github.com/LaylasLethalParadox/HoofprintBay-ModManager#install).
2. Extract the Wardrobe and tack overhaul zip into the game folder, so that you have `BepInEx\plugins\WardrobeAndTackOverhaul\WardrobeAndTackOverhaul.dll`.
3. Start the game.

To uninstall, delete `BepInEx\plugins\WardrobeAndTackOverhaul`. Your horses then wear the game's normal halter and tack again.

## Saves

Wardrobe and tack overhaul doesn't change `save.json`. The saved outfits, tack sets and halters are in a file called `WardrobeAndTackOverhaul.json` in the save's folder. Delete it to start over.

## Known limits

- Bought halters are only kept by this mod, so the game's Sell list doesn't show them.

## Building

You need the .NET 6 SDK, the game with BepInEx installed and started at least once, and Mod Manager installed, because the project compiles against files from both. Players don't need Mod Manager; only the build does. This repository contains no game files.

```
dotnet build -c Release -p:GameDir="C:\Path\To\HorsesOfHoofprintBay\\"
```

The build copies the DLL into the game's `BepInEx\plugins\WardrobeAndTackOverhaul` folder, so close the game first. `package.ps1` makes the release zip.

## License

GPL-3.0, see [LICENSE](LICENSE).

This is an unofficial fan project and isn't affiliated with or endorsed by the developers of Horses of Hoofprint Bay.
