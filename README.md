
# VPMPublish

VPMPublish is a small program to assist with publishing vpm packages to github.

This program requires you to use `git`, follow the [common changelog style guide](https://common-changelog.org) and it uses `gh`, the GitHub CLI.

It also includes a `changelog-draft` and a `generate-vcc-listing` command to complete the process of publishing a package.
The generated vcc listing is just 2 json files, the listing json itself and a tiny extra json file for latest package info, both of which are meant to be used to dynamically populate a human readable listing page.

# Installing this program

- Make sure you have dotnet 8 installed. Check by running the command `dotnet sdk check`, you need both the 8.x sdk and the runtime.
- Either `git clone` this project, see the clone button on github to get the right url, or download the source zip file and extract the files.
- Once downloaded, open a command line/terminal in that folder and run `dotnet build`
- "Install" the program so all you have to do is type `VPMPublish` in the command line/terminal to run it
  - If you are on Linux or MacOS
    - Run `sudo ln -s $PWD/bin/Debug/net8.0/VPMPublish /usr/local/bin/vpm-publish`
      - This creates a symbolic link called `vpm-publish` inside of a directory which is part of the PATH environment variable by default
      - If you'd like, you can use a different name than `vpm-publish`, like `VPMPublish` or anything else
  - If you are on Windows
    - Navigate to `bin/Debug/net8.0` and copy the entire folder path
    - Search for "environment" in windows search
    - Click on environment variables, or so
    - Edit the PATH variable, be it system or user doesn't matter, it's up to you
    - Add the copied path in there
    - Apply
    - You are now able to run `VPMPublish` in the command line.
      - If you want a different name... you could rename the exe file, but then if you update it at any point, you'd have to do that again
      - Maybe there's some way to create aliases in MSDos, I don't know

# Creating a VCC Listing

## Hosing the listing

<!-- cSpell:ignore vcclisting -->

The VCC Listing generated by `generate-vcc-listing` needs to be distributed and hosted somewhere. I am using a github gist because to my knowledge (I could be very wrong) this is one of the intended purposes of gists. At the end of the day you could use any file sharing service you want, so long as it has persistent URLs and is easily updated with but a few commands. Note that if you're going to use the website template that is provided here, it requires 2 files to be hosted on the service you choose. Otherwise, when not using the website template, there is the option to omit the latest versions info file and just generate the listing json file.

To create a github gist for this:

- Go here: https://gist.github.com
- Create a new gist. What you initialize it with doesn't really matter, but if you want you can already decide on what to call your VCC Listing json file. I like `vcclisting.json` personally. And again, it doesn't matter what you put there, some place holder text works
- If you didn't make it public, you can edit it to change the visibility. (It technically does not have to be public, but considering this is meant to be public data I prefer it being public)
- In the top right there's a field with a dropdown to the left of it. Click on that and select `Clone via ...`, either HTTPS or SSH, whichever you're using for git
- Choose a local location where you'd like to have the local clone of the listing, which will be where this program should generate updated versions to
- Run `git clone <the url you copied from the gist to clone it> VCCListing` where `VCCListing` is the folder name to clone into, so you can call it whatever you want
- Technically that's it, but it's good to also generate the initial empty listing both for testing this setup and for testing the webpage later
  - Run `vpm-publish generate-vcc-listing` with the appropriate arguments which will fill in the listing metadata (remember, `VPMPublish` on windows, if you've followed the installation steps)
    - Note that when using github gists, you can get the url to the hosted file by pressing the `raw` button and copying that url, however make sure to remove the second hash from the url. By removing that hash, it will always point to the latest version of that file on the gist, while with that hash it'll always point to that exact version, which is not what we want. Here's an example of the url we want: https://gist.githubusercontent.com/JanSharp/f8e5bf0bc971c99cdbaa59039a1efe4d/raw/dummylisting.json
    - The output directory should be the root of the local clone of the gist you just created
  - If this succeeds without errors, there should now be valid json file(s) in the output directory
  - With that confirmed
  - Open a terminal in the folder containing the github gist for the listing
    - Note that `cd` can be used to change directory when writing a script to automate this process later (or simply using a terminal in general)
  - Run `git add .`, `git commit -m "Update listing"` and `git push`

## Creating the webpage

To create the webpage, you need some way to host a webpage. If you already have a webpage then adding a listing webpage shouldn't be too difficult, so long as you have a way to use straight xhtml (or html I suppose), css and js files.

When you don't have a webpage yet, you can use [github pages](https://docs.github.com/en/pages). Here's [my github pages repo](https://github.com/JanSharp/jansharp.github.io) for reference.

Now to actually create the webpage you can use the following files as templates from [my github pages repo](https://github.com/JanSharp/jansharp.github.io):

- [docs/styles.css](https://github.com/JanSharp/jansharp.github.io/blob/main/docs/styles.css)
- [docs/vrc/vcclisting.xhtml](https://github.com/JanSharp/jansharp.github.io/blob/main/docs/vrc/vcclisting.xhtml)
- [docs/vrc/listing.js](https://github.com/JanSharp/jansharp.github.io/blob/main/docs/vrc/listing.js)
- Do note that these files are licensed under [MIT](https://github.com/JanSharp/jansharp.github.io/blob/main/LICENSE.txt). Don't worry though, MIT is a super small and very permissive license, you can read it in like 2 minutes. When it says "shall be included" it really does mean just that - I like making a LICENSE_THIRD_PARTY.txt file [like this for example](https://github.com/JanSharp/phobos/blob/main/LICENSE_THIRD_PARTY.txt) - and you can license whatever you're using these files for however you want. I believe it's generally considered to be good faith if you also include a link to third party code, [like I'm doing here](https://github.com/JanSharp/phobos#libraries-dependencies-and-licenses), or on a credits page or something.

The only file that _requires_ modification is the vcclisting.xhtml file, we'll get to that last.

The absolute location of those files does not matter, they only expect to be relative to each other in that hierarchy, with the `vrc` folder. However you are free to change that hierarchy, just make sure to update references in the `vcclisting.xhtml` file.

You can of course modify styles.css however you want, and change the layout in the vcclisting.xhtml file however you want. The listing.js file does expect several `id`s to exist however.

**Required modification of the template:**

The only file that requires modification is `vcclisting.xhtml`

- Change or remove the favicon link
- Change the `<title>` of the page to the display name you'd like to use for your VCC listing
- Change the `var listingUrl = [...];` line to point to the listing file you're hosting
- Change or remove the link to `Home`
- Change the first `<h1>` header, which makes the most sense imo to be the same as the page title, aka the display name of your VCC listing

## Links to External Package Dependencies

The template webpage has support for linking to external pages when a given package depends on, well, external packages. Those are packages which are not part of vrchat's listing (like `com.vrchat.worlds`) nor the your listing itself. It does so using a third json file. This file is not generated by this publish tool (it doesn't have the data to generate it).
It is therefore optional, the website works without it. It does make a request for it, but it handles it being 404.

So simply manually create another file next to the listing json and the `*.latest.json` files, called `*.external.json`. The file takes the form:

- It is just 1 json object (`{}`)
- The keys are the internal names of the external packages
- The values are the urls to the external package websites. For consistency I would recommend to link to the repositories for the external packages, since internal packages reference (ones within your listing) also link to the repositories in the unmodified template. The repositories should have a link to their own listing as installation instructions anyway

For example: `vcclisting.external.json`:

```json
{
  "com.jansharp.common": "https://github.com/JanSharp/VRCJanSharpCommon",
  "com.jansharp.music-control": "https://github.com/JanSharp/VRCMusicControl"
}
```

# Creating a new VPM Package

<!-- cSpell:ignore jansharp, vrchat, autocrlf -->

- It would be a good idea to read this: https://vcc.docs.vrchat.com/vpm/packages
- And this: https://docs.unity3d.com/2022.3/Documentation/Manual/CustomPackages.html (it's also linked in the above page, so this is just emphasis.)
- Create a `com.user-name.package-name` folder in `Packages` in the unity project
- Create a `package.json` file, you can use this as a template (the vrc worlds dependency version here may be outdated):

<!-- TODO: mention "documentationUrl", add add support for it to the tool itself too - it should also increment the version of the documentation url when bumping all the other version numbers. -->

```json
{
  "name": "com.jansharp.dummy",
  "version": "0.1.5",
  "description": "A truly wonderful dummy package.",
  "displayName": "Dummy Package",
  "unity": "2022.3",
  "author": {
    "name": "JanSharp",
    "email": "foo@bar.com",
    "url": "https://jansharp.github.io"
  },
  "license": "MIT",
  "vpmDependencies": {
    "com.vrchat.worlds": "^3.4.x"
  },
  "url": "https://github.com/JanSharp/VCCDummyPackage/releases/download/v0.1.5/com.jansharp.dummy.zip",
  "changelogUrl": "https://github.com/JanSharp/VCCDummyPackage/blob/v0.1.5/CHANGELOG.md"
}
```

- Here's [Unity's fields in package.json](https://docs.unity3d.com/2022.3/Documentation/Manual/upm-manifestPkg.html) and here's [VRChat's additions](https://vcc.docs.vrchat.com/vpm/packages#vpm-manifest-additions) to the package.json.
- Note that this vpm publish program ultimately formats the package.json whenever it publishes (to be exact, after it's published)
  - To prevent this from being annoying, there's the `vpm-publish normalize-package-json` command (remember, `VPMPublish` on windows, if you've followed the installation steps)
- Make sure to `git init`
  - I'd generally recommend to run `git config --local core.autocrlf false` for unity projects
- Make a bunch of git commits in the process of working on the project, as per usual

## Ignoring files when packaging

The following files are excluded by default (and cannot be included):

- `.gitignore`
- `.gitkeep`
- `.vpmignore`
- `.git` (file or folder, it gets excluded entirely)

You can add a `.vpmignore` file at the root of the project to specify file globs which should be excluded when creating a vpm package zip file. All specified globs are relative to the root of the package. Empty lines are ignored. Lines starting with `#` are ignored. You can only exclude, including files once they are excluded isn't possible. In general, the globs are very similar to those in `.gitignore`, however unfortunately I cannot tell you how exactly they work because the program is using the [Microsoft.Extensions.FileSystemGlobbing](https://www.nuget.org/packages/Microsoft.Extensions.FileSystemGlobbing/10.0.0-preview.2.25163.2) package, and the best documentation I found so far is this [here](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.filesystemglobbing.matcher?view=dotnet-plat-ext-7.0#remarks). Also, unlike `.gitignore`, you can only have a `.vpmignore` file in the root of the project, not any additional ones in sub folders. And a keep variant also doesn't exist.

**Important:** The globs are matching _files_, not directories. If you want to exclude all files in a directory, use `my/dir/**/*`.

**Important:** Make sure to also exclude the `.meta` file for every excluded file. Chances are high that you'll just be duplicating lines and adding `.meta` to them.

To double check if the `.vpmignore` file is doing what you want it to, use the `vpm-publish publish --list-url <...> --package-only` command (remember, `VPMPublish` on windows, if you've followed the installation steps).

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
- Run `vpm-publish generate-vcc-listing` to generate an updated VCC listing locally
- Open a terminal in the folder containing the github gist for the listing
    - Note that `cd` can be used to change directory when writing a script to automate this process (or simply using a terminal in general)
- Run `git add .`, `git commit -m "Update listing"` and `git push`

From `vpm-publish publish` onwards you can create a script (on Linux/MacOs shell/bash, on Windows most commonly MSDos batch) that runs all of those commands with the correct arguments already defined in sequence, allowing you to just sit back and relax while it (most likely very quickly) publishes your package.
