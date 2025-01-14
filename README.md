# ZStats

<img align="right" src="https://github.com/zybexXL/MCStats/blob/main/ZStats/Docs/zstatsPlaylists.png">

**ZStats aka MCStats is a tool to compile Play History statistics for JRiver Media Center**. It can also be used to execute an MC Expression against each file on the library, eg. to update a pre-calculated field value.

**ZStats** reads the contents of *[Play History]* field from JRiver's Media Center, computes user-defined statistics from the data, and writes back the calculated statistics to an MC field for usage in Views/Expressions/Smartlists. **ZStats** can also create user-defined Playlists for the Top N most played tracks in any given time range. 

**ZStats** is designed to be executed periodically (nighly) as a scheduled task to update statistics for all files in an MC library, so that this heavy computation task doesn't need to be run in realtime in MC. Because of this, it does not have a GUI and all settings are done via a *zstats.ini* configuration file.

Please post any issues or requests using MC's [Interact forum](https://yabb.jriver.com/interact/index.php), where I'm known as **zybex**.

<br>

Features
------
- Play History statistics per File, Album, Artist, or grouped by any other MC field
- writes statistics back to an MC field for usage in Views/Expressions
- creates TOP N Playlists for a given date range using multiple criteria
- daily/weekly/monthly/etc statistics, custom date ranges supported
- optionally executes an Expression before/after calculating statistics
- designed to run as a scheduled task to update statistics periodically (overnight)

<br>

Requirements
------

**ZStats** requires MC v28.0.93 or above.

**ZStats** v1.2 requires Net8 Runtime. Please install the appropriate runtime from here if needed:<br>
https://dotnet.microsoft.com/en-us/download/dotnet/8.0

For Linux/MacOS you may need to mark the downloaded binary as Executable:
> chmod +x ./ZStats-linux-x64

MacOS may also require the binary to be signed:
> codesign --force --deep -s - ./ZStats-osx-x64

<br>

Instructions
------

1. Create the *[Play History]* field in MC and setup an "After Playback" expression (instructions [below](#mc-configuration))
2. download the [latest release](https://github.com/zybexXL/MCStats/releases) and place the executable in a folder with Write permissions
3. run it once - it will create a default *zstats.ini* configuration
4. edit the config file and enter your MCWS hostname, username and password
5. run **ZStats.exe** again to process all files and generate Statistics and Playlists
6. fine tune the config file to your taste, re-run to update statistics/playlists
7. Use Windows Task Scheduler to schedule a nightly run of the tool

<br>


ZStats Configuration
------
The *ZStats.ini* configuration file is generated by default. You can define which statistics and playlists you want to create, as well as the output format for the *[Play Stats]* field and other settings. Please read the comments on the [default zstats.ini](https://github.com/zybexXL/MCStats/blob/v0.95/ZStats/SampleConfig.ini) to learn about all the available options and time range tokens.

The default configuration sets *UpdateStats=0* and *UpdatePlaylists=1*, so it will only create the Top Playlists which is relatively fast. To create/update the *[Play Stats]* field you need to set *UpdateStats=1*. The first run with this option enabled may be VERY slow depending on the size of your library because it needs to update *[Play Stats]* for all files; however, on subsequent runs **ZStats** will only update the files which had changes since last run (files which were played since last run), so it will be much faster.

<br>

MC configuration
------
To use **ZStats** you need to first setup MC to collect Play History timestamps, as described in [this thread](https://yabb.jriver.com/interact/index.php/topic,130266.0.html). In short:
1. Create a string field (NOT calculated) called [Play History]
2. Set the expression in "Options > Library & Folders > Expressions: After Playback Expression" to
   
   `setfield(Play History,[Last Played,0];[play history])`

After setting this up and playing tracks for some time, the *[Play History]* field have been updated with enough information for **ZStats** to process and generate the statistics and playlists.

**ZStats** will automatically create the *[Play Stats]* field if it doesn't exist (or any other field name as specified in the *ZStats.ini*). However, the created field will have the flag "*save in file tags*" enabled by default as it cannot be disabled automatically via the MCWS API. This may cause the update process to become even slower as MC will update the *sidecar.xml* for each file as **ZStats** is updating the field. Because of this, I recommend that you manually disable this flag on the *[Play Stats]* MC field.
