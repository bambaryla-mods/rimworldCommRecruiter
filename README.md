# Comm Recruiter

A RimWorld 1.7 mod. Hire pawns from friendly and neutral factions through your comms console.

Player guide: https://bambaryla-mods.github.io/rimworldCommRecruiter/

## What it does

- A **Hire pawns** button on every comms console opens a hire window. Pick a non-hostile faction and choose from its limited roster. Each pawn costs its combat power in silver, paid from orbital trade beacon range.
- Goodwill unlocks stronger pawn kinds (4 tiers by combat power) and a bigger roster (3 to 10 pawns). World Domination 2's raised goodwill cap is supported.
- Hirees arrive 2 to 3 days later, by drop pod (Spacer tech and up) or on foot, wearing only a T-shirt and pants.
- They work for you for 15 days, then go home with whatever they wear and carry.
- Deaths cost goodwill, enslaving a hiree turns its faction hostile, and hirees who were happy enough may ask to stay for good.

## Building

The C# source is in `Source/`. It targets net10.0 (RimWorld 1.7). `CommRecruiter.csproj` builds `1.7/Assemblies/CommRecruiter.dll`, and `UIRethemedCompat/CommRecruiter.UIRethemed.csproj` builds the optional [LFS] UI Rethemed theme in `Compat/UIRethemed/1.7/Assemblies`.

## License

[CC BY-NC-SA 4.0](LICENSE)
