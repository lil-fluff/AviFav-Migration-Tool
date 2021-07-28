# AviFav-Migration-Tool

THIS REPOSITORY IS BEING ARCHIVED DUE TO INTERNAL CHANGES WITH THE API ON VRCHAT'S END.
PLEASE UNDERSTAND THAT YOU TAKE THE FULL RISK OF ATTEMPTING TO DO ANYTHING FURTHER WITH THIS CODE.


Hi, hello,
Are you missing your old VRCTools AvatarFav list and want something to fix it?
Then I have just the thing for you!
In the VRCTools discord there is a mod called AviFav+ which brings back local avatar favorite lists.
But you're probably wondering, well if [insert client name here] that I used for so long only spits out a list of avatar ids, 
how do i convert them to AviFav+ format, without looking each one up by hand?

Allow me to introduce the brand new AviFav+ Migration Tool, made by yours truly.

### 1) First you will need to authenticate with the VRChat API by entering your username and password.

Clicking "Login to VRCAPI" does two things: 

First, it checks to make sure the API can be contacted and grabs the current 'apiKey' value, 
which is needed to be tacked on the the actual avatar requests later.

Second, it then assembles your credentials into http Basic auth format, which is then sent as a header along with furtue requests.

Why use basic auth, that's so inefficent?
Well, for one, it doesn't require storing your password anywhere, as soon as you close the app, poof, authkey gone.
And also, I don't care enough to support authtoken stuff. This tool was written in a few hours.

### 2) Once you're logged in, select if you wish to fetch data for a single avatar, or multiple.

A single avatar request can be made siply by clicking 'fetch avatar data' at this point, and the JSON string will be returned in the output box.
However, if you have a list of avatar ids, keep reading.

### 3) Fetching multiple avatars without tripping API spam, ddos protection Et al.

The utility has been designed to use a longer delay per request based on the number of avatars you're polling for.
The general math is as follows: 
2500 ms for lists over 100.
1500 ms for 50 - 100.
900 ms for 20 - 50.
650 ms for less than 20.

As I'm unsure what the actual request limit is or what may trip the system into a 'red flag' these are only values I have guessed are safe based on data from previous tools that use the VRCAPI.

The UI will lock during the fetch process, and the console output will update on the right of the screen with the progress for each request.

When the list is completed, the UI unlocks and spits out the preformatted final JSON data that should be put in your AviFav+ 'avatars.json' file, located at 'VRChat\404Mods\AviFavorites'.


### [Known issues]
##### 1) There is a trailing comma that gets spit out in the JSON output for multi mode. For now, just delete the last comma when pasting into the avatars.json file.

##### 2) For whatever reason, when copying output to the avatars.json file, if using Visual Studio, the text encoding breaks. Solution: dont use Visual studio to edit .json files. Use Notepad or Notepad++.

##### 3) Sometimes some requests may take slightly more than 2500ms to return. You may see "request timed out" in the log. Copy the avatar ids that failed somewhere (like notepad), and run the tool again with just those ones. Note that this does not apply to (404) not found. Those avatars are likely removed.
