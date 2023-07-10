
# VPMPublish

VPMPublish is a small program to assist with publishing vpm packages to github.

This program requires you to use `git`, follow the [common changelog style guide](https://common-changelog.org) and it uses `gh`, the GitHub CLI.

// TODO: Add note about and link to the VCC listing generator.

# Installing this program

- Make sure you have dotnet 7 installed. Check by running the command `dotnet sdk check`, you need both the 7.x sdk and the runtime.
- Either `git clone` this project, see the clone button on github to get the right url, or download the source zip file and extract the files.
- Once downloaded, open a command line/terminal in that folder and run `dotnet build`
- "Install" the program so all you have to do is type `VPMPublish` in the command line/terminal to run it
  - If you are on Linux or MacOS
    - Run `sudo ln -s $PWD/bin/Debug/net7.0/VPMPublish /usr/local/bin/vpm-publish`
      - This creates a symbolic link called `vpm-publish` inside of a directory which is part of the PATH environment variable by default
      - If you'd like, you can use a different name than `vpm-publish`, like `VPMPublish` or anything else
  - If you are on Windows
    - Navigate to `bin/Debug/net7.0` and copy the entire folder path
    - Search for "environment" in windows search
    - Click on environment variables, or so
    - Edit the PATH variable, be it system or user doesn't matter, it's up to you
    - Add the copied path in there
    - Apply
    - You are now able to run `VPMPublish` in the command line.
      - If you want a different name... you could rename the exe file, but then if you update it at any point, you'd have to do that again
      - Maybe there's some way to create aliases in MSDos, I don't know

# Creating a new VPM Package

<!-- cSpell:ignore jansharp, vrchat, udonsharp, autocrlf -->

- It would be a good idea to read this: https://vcc.docs.vrchat.com/vpm/packages
- And this: https://docs.unity3d.com/2019.4/Documentation/Manual/CustomPackages.html (it's also linked in the above page, so this is just emphasis.)
- Create a `com.user-name.package-name` folder in `Packages` in the unity project
- Create a `package.json` file, you can use this as a template:

```json
{
  "name": "com.jansharp.dummy",
  "version": "0.1.5",
  "description": "A truly wonderful dummy package.",
  "displayName": "Dummy Package",
  "unity": "2019.4",
  "author": {
    "name": "JanSharp",
    "email": "foo@bar.com",
    "url": "https://jansharp.github.io"
  },
  "license": "MIT",
  "vpmDependencies": {
    "com.vrchat.udonsharp": "^1.1.x"
  },
  "url": "https://github.com/JanSharp/VCCDummyPackage/releases/download/v0.1.5/com.jansharp.dummy.zip",
  "changelogUrl": "https://github.com/JanSharp/VCCDummyPackage/blob/v0.1.5/CHANGELOG.md"
}
```

- Here's [Unity's fields in package.json](https://docs.unity3d.com/2019.4/Documentation/Manual/upm-manifestPkg.html) and here's [VRChat's additions](https://vcc.docs.vrchat.com/vpm/packages#vpm-manifest-additions) to the package.json.
- Note that this vpm publish program ultimately formats the package.json whenever it publishes (to be exact, after it's published)
  - To prevent this from being annoying, there's the `vpm-publish normalize-package-json` command (remember, `VPMPublish` on windows, if you've followed the installation steps)
- Make sure to `git init`
  - I'd generally recommend to run `git config --local core.autocrlf false` for unity projects
- Make a bunch of git commits in the process of working on the project, as per usual

## Notes for new projects

- Do _not_ create a `CHANGELOG.md` file, it will be created when the first release happens.

# Creating a release

- Make sure you have `gh`, the GitHub CLI, installed
  - Make sure to run `gh auth login --hostname github.com` and follow the steps. (hostname is technically optional, but VPMPublish assumes `github.com` is used)
- Make sure you have a clean working tree
- Run `vpm-publish changelog-draft` (remember, `VPMPublish` on windows, if you've followed the installation steps)
- Modify the `CHANGELOG.md` file in accordance with https://common-changelog.org
- Once the changelog is done
  - Run `git add CHANGELOG.md`
  - Run `git commit -m '<message>'` where `<message>` is the message you got when running `changelog-draft`
- Run `vpm-publish publish`
  - If you'd like to be cautious, you can run `vpm-publish publish --validate-only` first, but the publish command itself runs validation first anyway
  - I'd recommend reading everything the publish command is doing so you, well, know what it's doing with your project, see here: [notes.md](notes.md)
- Update the VCC listing // TODO: Add link to that project
