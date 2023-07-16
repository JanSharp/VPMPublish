
# Publish Workflow

- validation
  - ensure that the `git` and `gh` commands are available
  - ensure that `gh` has an authorized token... maybe?
  - ensure that the the `main` (or `master`) branch is checked out
  - ensure that the working tree is clean
  - ensure that the remote is currently reachable
    - use `git fetch --dry-run`
  - validate project structure
    - `package.json` exists, is valid json and name matches folder name
    - `CHANGELOG.md` exists
  - validate and extract `"version"` number
  - validate both `"url"` and `"changelogUrl"`, for example:
    - "url": "https://github.com/JanSharp/VCCDummyPackage/releases/download/v0.1.0/com.jansharp.dummy.zip",
    - "changelogUrl": "https://github.com/JanSharp/VCCDummyPackage/blob/v0.1.0/CHANGELOG.md",
    - abort if they are invalid or if the version number doesn't match
  - ensure the tag `v{packageJson.version}` doesn't exist yet
  - extract changelog entry
    - validate changelog's top block's version number matches, abort if it doesn't
    - validate changelog date, abort if it doesn't match, telling the user what date it should have so they can update it and `git add .` and `git commit --amend --no-edit`. It'll use UTC
    - what to extract
      - don't include the # changelog header
      - don't include the ## `[x.x.x]` line
      - include the whole version block, but not the previous one
- package
  - create zip file, in tmp
    - include everything except `.git`
    - think about also ignoring files ignored by `.gitignore` but I don't think it should.
    - calculate sha256 checksum of the zip
  - generate release notes
    - insert link to human readable listing page at the top
    - next `# Changelog`
    - generate the `## [x.x.x] - YYYY-MM-DD` line
    - use the previously extracted version block from the changelog
    - append the sha256 checksum at the bottom of the release notes
- create annotated git tag for this version
  - use the form `vx.x.x`
  - include the sha256 checksum in the annotation for the tag in a machine readable way
- create GitHub release
  - push the previously created tag
  - attach the zip file
  - use the generated release notes
  - use the freshly created git tag
  - set title to `vx.x.x`
- increment the version
  - update version in `package.json`
    - `"version"`
    - `"url"`
    - `"changelogURL"`
  - git commit
    - don't push to allow for the programmer to change the commit if wanted

# Changelog Util

- generate draft
  - add new entry at the top with the version from the package.json, and the current UTC date
  - use git log to generate a changelog draft

# Normalize package.json

Why? you might ask. Because the order of fields is fixed when serializing, so this actually changes field order, unless the given package.json already has the "correct" order.

- Deserialize `package.json`
- Validate, using the same validation for name, version, url and changelogUrl as publish
- Serialize `package.json`

# Generate VCC listing

- Validate
  - Ensure the `git` command is available
  - Validate `--url`, must be an actual url and end with `.json`
  - Validate `--author`, must be an email address
  - Validate `--out-dir`, must be an existing directory
  - Validate each given package path argument
    - Has a `package.json` file
    - Is a git repo, by checking for the `.git` folder
- Load all required data
  - For each package
    - Get all version tags
      - Get all tags starting with `v`
      - Filter them to only keep the ones where the part after `v` is a valid SemVer version
      - Extract the sha256 checksum from the annotated git tag
        - If it doesn't have the sha256 checksum in the tag, forget about that version
    - Use `git show refs/tags/vx.x.x:package.json` to load the `package.json` for every version of the package
      - Validate the retrieved `package.json`, using the same validation as publish
      - Validate that the version matches the tag version, otherwise abort
      - Add the `zipSHA256` field, using the extracted checksum from the git tag
- Generate the listing json file
  - Use the `--name`, `--id`, `--url` and `--author` provided as command line options
  - Use the retrieved `package.json` from every version of every package to build the `packages` object
  - Write the file, again using the filename extracted from `--url`
- Generate latest versions json file
  - All this data is purely used by the human readable webpage
  - An array where each value is an object with the fields:
    - "name": "com.foo.bar (internal name)" - obtained from the package json
    - "version": "x.x.x (string, semver)" - obtained from the git tag or the package json
      - This removes the need of the semver library for the webpage (more info below)
    - "updateDate": "yyyy-MM-ddThh:mm:ss+00:00 (string, ISO 8601)" - obtained from the creation time of the git tag
      - Makes it easy to add a Updated On column to the webpage, including it being sortable
  - Sort the array by the version, then by display name
    - By having the list pre sorted by version, the website can sort the list by version too without requiring a semver js library

The reason the latest versions json file is created is because it's much easier to use the SemVer package in C# than it is to try and figure out how to use the node js package for semver. Plus it makes the website like literally a fraction of the size, because the library would be way bigger than anything else.

Also note that the website is using the listing json file (and the latest versions json file) to populate itself dynamically, using a bit of js scripting.
