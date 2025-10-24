# Hearthstone Access Development Tools
A set of tools for promoting a community effort for keeping Hearthstone Access up and running for a while longer while Blizzard implement Accessibility natively into the game.

Note: This process was approved by Blizzard. Working with diff files is clunky, but I did my best to come up with as much handholding as possible to make things easier.

Please stick to the approved process and ***do not publish and/or share decompiled proprietary code anywhere***.

## Setup
1. Clone the project:
```
git clone --recurse-submodules https://github.com/antonshusharin/DevTools.git
cd DevTools
```

2. Build the dev tools
```
dotnet build
```

3. Create a symlink named `Hearthstone` inside the project to your Hearthstone installation. You can either look for the directory yourself and use `mklink` to do so, or use the provided linking tool:

```
dotnet run --project LinkHearthstoneInstallation
```
Note that both options ***require administrator privileges*** as they both use `mklink` (which requires administrator privileges). `LinkHearthstoneInstallation` simply scans your drives for your Hearthstone installation (similar to how the Hearthstone Access patcher works) and creates the link for you.

4. Run the setup tool:
```
dotnet run --project EnvironmentSetup
```

If all goes well, your local Hearthstone should now have been patched with Hearthstone Access built from source for version 33.6.2.229543.

At this point, your development environment is setup.

## Understanding the development environment structure
Your development environment is automatically created for you during the setup process. The following directories and files are important:
- `Decompiled`: A local git repository (ignored by this repo) containing all the decompiled source for both Hearthstone and Hearthstone Access. More on this further below.
- `ManagedOverrides`: A directory containing assemblies which need to be overriden in order to compile Hearthstone Access back. These include replacements to stripped assemblies (such as Unity and System ones) to screenreader libraries (such as Tolk and its own runtime dependencies)
- `Speeches`: A directory containing all the speeches used during by the in-game narrator during the tutorial, required for playing the tutorial
- `Localization`: A git submodule pointing to all Hearthstone Access translations in https://github.com/HearthstoneAccess/Localization
- `diff.patch`: The (git) diff patch file which can be applied on top of a decompiled version of Hearthstone into a Visual Studio project containing Hearthstone Access
- `baseline.patch`: The (git) diff patch file which can be applied on top of Hearthstone 33.6.2.229543 to initialize the Decompiled directory. Note that this is only used during setup.
- `SetupBaseline`: A directory containing all managed assemblies for Hearthstone 33.6.2.229543. This is only used during setup to establish a baseline.

For the most part, you'll only need to worry about the `Decompiled` directory.

## Understanding the `Decompiled` directory
The `Decompiled` directory is the most important directory during development. This directory is automatically created for you during the setup process and consists in:
- A local git repository (separate from this repository and ignored by it) containing decompiled versions of Hearthstone and VS projects of Hearthstone Access in the `Assembly-CSharp` directory
- The `.product.db` file used during the decompilation process
- The `Hearthstone_Data` directory used during the decompilation process

This local git repository has the following branch structure:
- `x.y.z.m`: a normal branch for a decompiled version `x.y.z.m` of Hearthstone
- `x.y.z.m-HSA`: a branch containing a working VS project of Hearthstone Access for Hearthstone version `x.y.z.m`

Please make sure you ***don't publish and/or share this repository with anyone*** as it contains proprietary code (even if decompiled). This is a ***private (and personal) repository*** meant to be used for development purposes only.

## Decompiling a new version of Hearthstone
Make sure your `Decompiled` directory is clean and in the latest Hearthstone directory. If this is the first time you're doing this, the correct branch will be the baseline branch `33.6.2.229543`.

Once you've done this, run the decompiler tool:
```
dotnet run --project Decompiler
```
This will create a new branch (e.g. `34.0.0.230233`) based on your previous one (e.g. `33.6.2.229543`) and decompile your local Hearthstone installation into it.

Note that this will also commit everything with a generic `Decompiled` commit message inside your local git repository so you can keep track of everything and use it for future patches.

## Patching Hearthstone with Hearthstone Access
Make sure your `Decompiled` directory is clean and in the latest Hearthstone directory. If you've just decompiled `25.0.0.158725`, the branch will be named `25.0.0.158725`.

Run the patcher:
```
dotnet run --project Patcher
```
This will create a new branch named `25.0.0.158725-HSA` based on `25.0.0.158725` and apply the `diff.patch` file on top of it.

Note: If your patch fails, you can manually use `git apply -v diff.patch` to see what went wrong. In the case of updates (e.g. attempting to apply a patch built for version `33.6.2.229543` on top of `34.0.0.230233`) the patch will naturally fail as the baseline has changed.

While you could certainly try to sift through the 60k lines of changes in `diff.patch` and adjust them until `git apply` works, there are better ways of doing this (explained further below).

## Building Hearthstone Access
Make sure your `Decompiled` directory is in a `HSA` branch. Your Hearthstone Access project lives under `Decompiled/Assembly-CSharp`. You can either open the solution file (`Decompiled/Assembly-CSharp/Assembly-CSharp.sln`) in Visual Studio and build it there, or run the following command:
```
cd Decompiled/Assembly-CSharp
dotnet build
```
This command will:
1. Compile the project
2. Copy the resulting `Assembly-CSharp.dll` into your Hearthstone `Managed` directory, overriding it
3. Copy all assemblies in the `ManagedOverrides` directory to your Hearthstone `Managed` directory (pointed by the `Hearthstone` symlink)
4. Copy all `enUS` Hearthstone Access translations (i.e. `ACCESSIBILITY.txt`) from the `Localization` directory into your Hearthstone `Strings` directory
5. Copy all speeches used in the in-game tutorial from `Speeches` into your Hearthstone `Accessibility` directory

If everything went well, your local installation of Hearthstone should now be running Hearthstone Access. You can simply launch Hearthstone using the Blizzard App to run the game.

## Handling Blizzard Patches
Once a patch lands, the latest `diff.patch` file will no longer work as the baseline has changed. While you're free to do whatever you want (including adjusting the 60k lines of changes manually until it works), my suggestion (and the one I've been using for a very long time) would be to use the local repository to merge and fix any conflicts.

Imagine the new version of Hearthstone is `34.0.0.230233`. If you've never decompiled Hearthstone before, you'll have to use the latest baseline branch (i.e. `33.6.2.229543`). If you have compiled, say, a version named `33.6.4.229600`, however, it'll be easier if you just use this one. In any case, either way works (and most developers will be working on top of `33.6.2.229543` initially as that's the current baseline).

Assuming `34.0.0.230233` as the new version and `33.6.2.229543` as the latest available one on your local `Decompiled` directory, a normal flow would be:

```
cd Decompiled
git checkout 33.6.2.229543
cd ..
dotnet run --project Decompiler
cd Decompiled # git status would return 34.0.0.230233 at this point
git checkout 33.6.2.229543-HSA # This will be the last working version of Hearthstone you had decompiled
git checkout -b 34.0.0.230233-HSA
git merge 34.0.0.230233
```
After running this, you'll get a bunch of merge conflicts. These will be caused by everything that changed from version `33.6.2.229543` to `34.0.0.230233`.

After fixing all conflicts, you can now try to fix the resulting syntatic errors and compile Hearthstone Access again. Once you can run the game again, the hard part begins: find everything that broke and make it work again. Depending on how much changed behind the scenes, this may range from simple adjustments to complete refactors.

Once everything seems to be working again, you're ready to generate the new `diff.patch` file with your changes and share it with the community.

## Generating the new `diff.patch`
Assuming your `Decompiled` directory is in a branch named `34.0.0.230233-HSA`, you can simply run:
```
dotnet run --project DiffGenerator
```
After running this, your `diff.patch` file will be updated so it now contains the patch that should be applied on top of `34.0.0.230233` to get the same exact VS project you have.

Once you've opened a Pull Request with this new `diff.patch` file, other developers will be able to:
1. Decompile `34.0.0.230233` themselves into their own `Decompiled` repository
2. Run the patcher tool using your `diff.patch` file to get the same `34.0.0.230233-HSA` branch (and project) that you have
3. Review and validate any changes using `git diff` locally
4. Test the game themselves

## Releases
Once a PR has been validated by the community, I will personally review it and run my testing program to validate things if possible.

Note that I can't share this program unfortunately as it does depend on one of my personal accounts (and making it not depend on it would take an extremly long time given it covers thousands of journeys).

Once everything looks ok, I'll accept the PR and run the release script so players can patch the game normally using the Hearthstone Access patcher.

## Finding Help
You can find most of the developers willing to contribute on the official Hearthstone Access Discord server (in the #developer-talk channel).

Here's the invite link: https://discord.com/channels/874150781144670208/1039226995952853072
