# ![Brickwork](resources/logo/icon-32.png) Brickwork

## For Users

Brickwork is a tool that helps you to get battlemaps created in Inkarnate into
FoundryVTT, without having to recreate walls there. Brickwork opens an offline
backup .ink file from Inkarnate, extracts wall metadata, allows you to quickly
adjust wall types and exact positioning. You can then export the map to
FoundryVTT.

Brickwork also supports importing from and to .uvtt (also known as .dd2vtt and
.df2vtt). When exporting to .uvtt, the various Foundry wall types are mapped
down to the more restrictive .uvtt types.

### Usage

1. Download the Brickwork binary for your OS from the
   [releases page](https://github.com/KjeldSchmidt/brickwork/releases)
2. Save an Inkarnate Backup.

   ![In the Inkarnate Editor, click "Save Offline Backup](./resources/images/save-inkarnate-backup.png)
3. Open the .ink file in Brickwork
4. Adjust wall types by clicking or dropdown, disable walls by middle-clicking,
   adjust wall position and gaps/doors by click-and-drag.
5. Export to .vtt, select FoundryVTT as target
6. In Foundry, create a new Scene, right-click -> Import. Select the .json file
   created by Brickwork. 
7. Now, set the Background image as normal, selecting the Inkarnate export.

### FAQ

**Q**: Windows says this software is dangerous!

**A**: It does say that. It's safe to click the `Run Anyway` button. I just
haven't yet decided to swallow the cost of buying the "Trusted Publisher"
status. If Brickwork gets decent feedback and sees actual use, I might.

----

**Q**: Why doesn't the export contain the background image, even though I can see
it in Brickwork?

**A**: Brickwork uses a low-resolution preview image embedded in the Inkarnate
backup. On most maps, this is not sufficient for good gameplay, and you should
use the full-resolution export from Foundry instead.

----

**Q**: Can you add support for...
**A**: Probably? I have plans to continue work on this. First, I want to expand
what can be done with walls: deleting, adding and rerouting instead of just
nudging. In this way, Brickwork could serve as a generic Wall Creation Tool even
without .ink or .uvtt files. I also want to look into supporting lights in
addition to walls. I have other ideas, but they are vague and depend on feedback.

If you want me to support a different .vtt as an export target or a different
mapmaking tool as an input, absolutely also reach out. There might be some
difficulty if those are pay-to-use, but there's a good chance we can figure
something out.

----

**Q**: Isn't this just AI Slop?

**A**: This project was indeed created with heavy usage of LLM-driven coding
tools. I am a professional software developer - I could have implemented every
individual part of this application myself, but probably never would have found
the free time to do it without AI coding tools. Even so, I've spent several days
of full-time work on searching for existing solutions, finding a technical
approach to getting wall data from Inkarnate, checking for edge-cases, bugs,
awkward interactions and applying lots of polish until it felt great to use.

I have intensely validated a complete flow from Inkarnate through Brickwork into
FoundryVTT and polished the flow carefully to make this a piece of software that
truly helps me.

If you think I should approach this tool, or other work for the PnP-Community,
with my full professional standards instead of hobbyist standards, I'll be happy
to talk about doing freelance work for or with you. 😉

----

**Q**: How does this find walls?

**A**: The .ink file is a complete history of edits made in Inkarnate. Brickwork
makes the simple assumption that every Path - closed or open - might be a wall
and tracks all of those, recreating them in a lower-resolution approximation.

This makes it different from [Auto Wall](https://autowallvtt.com/), which uses a
visual approach to wall detection. Auto Wall is a strong general-purpose
solution when you only have the final map. However, I usually make my own maps
in Inkarnate, and I believe that Brickwork offers better results in cases where
the .ink file is available.

----

**Q**: Could this be integrated directly into FoundryVTT?

**A**: In principle, yes. Initial development was much smoother in the
unconstrained space of a purpose-built tool with, where I could dissect the 
.ink-file and problems with my parsing/tracking/wall generation independent of
Foundry APIs. I am still considering the option of taking all the knowledge
gained until now and translating it into a Foundry Module. Do let me know if
this is something you want to see.


## For Developers

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (run `just setup-repo` on Windows to install automatically)

### Build

From `converter/` (requires [just](https://github.com/casey/just)):

```bash
just recompile
```

Or with `dotnet` directly:

```bash
dotnet build converter/Brickwork.sln
dotnet test converter/Brickwork.sln
```

### Run (GUI)

```bash
cd converter && just gui
```

Open an Inkarnate `.ink` backup, preview wall overlays, edit walls, and use **Export to VTT** (Foundry JSON).

### Run (CLI)

```bash
cd converter && just cli convert -i ../resources/test-maps/empty-backup.ink -o output.json -f foundry
cd converter && just cli analyze -i ../resources/test-maps/empty-backup.ink
cd converter && just cli analyze -i ../resources/test-maps/empty-backup.ink --summary
```

CLI export formats: `foundry`, `uvtt1`

### Releases

Create a release from GitHub Actions → **Release** → Run workflow, and enter a version such as `0.1.0-beta.1`.

Optional patch notes: add `patch-notes/<version>.md` (same version string as the workflow input). If that file is missing, the GitHub release is created with an empty body.

Builds are published for Windows, Linux, and macOS (x64 and arm64).