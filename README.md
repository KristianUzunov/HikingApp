## HikingApp
A very basic Unity-based Android app that tracks hikes using GPS, records waypoints and saves images of hikes.
## Set-up

Just plug it in Unity, everything is in one scene.

## Here is what your average hike looks like

<img width="512" height="512" alt="699593992_3403696923165500_2912520168689549144_n" src="https://github.com/user-attachments/assets/5a985c60-a35d-42b1-a21a-a769dbb79110" />

You can change the thickness and color of the line in the inspector or in the `MapController` script located on the MapController GameObject.

## Project Structure

 `GPSManager` — Located on AppManager GameObject handles GPS location updates, can change how often it updates location, accuracy and distance per update through inspector in the editor
 
 `HikeTracker` — Located on AppManager GameObject, records waypoints, calculates distance and elevation (since gps is not amazing at perfect location and jumps around you can change the
 
 `HikeStorage` — saves and loads hike data as JSON
 
 `HistoryUIController` — displays saved hikes, handles the history panel on It's own GameObject
 
 `RecordingUIController` — controls start/pause/stop UI during a hike, handles the main panel same as above
 
 `BackgroundServiceBridge` — communicates with the Android background service, uses outside .aar file built with AndroidStudio (nabbed from https://github.com/nintendaii/unity-background-service)
 
 `PermissionManager` — all permission based stuff (added in AndroidManifest.xml too) 
 
  `GPSSimulator` - AI written GPS testing thing (doesn't work very well but It's good enough for basic tests), doesn't work in background only on foreground. (Located on AppManager GameObject turn it off before building)

HikeCard is the hike snapshot prefab in the UnityEditor.

## IMPORTANT

- The app is Android only, It probably won't work on IOS but don't have an IOS device to test it on, so no idea.
-  `HikeTracker` MUST be on the AppManager GameObject to work.
- GPS simulation is available in the Unity Editor for testing
- Snapshots are saved inside the Hikes folder in gallery on Android and in LocalLow\(yourCompanyName)\HikingApp\Hikes on PC by default if you were to click save on them
- Map data provided by OpenStreetMap (openstreetmap.org/copyright). Make sure to keep the copyright thing as It's required by law.

## KNOWN BUGS/ISSUES
- Sometimes after you give the app all permissions the map doesn't start, if that happens just open the app again and it should be fixed
- If there is no 'red line' drawn hike doesn't save as there is nothing to save so it just leaves a blank default HikeCard
- If the app is closed whilst a hike is being recorded the notification for recording a hike stays there and then if you try to open the app again the whole thing crashes, can be fixed by force stopping it through settings
