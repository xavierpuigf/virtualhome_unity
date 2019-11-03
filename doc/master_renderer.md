## Overview

On the __MasterScene__, __Master Renderer__ component is attached to the only GameObject on the scene. __Master Renderer__ has static class __RenderPref__. Static variables lasts throughout the scenes so we can access __RenderPref__ from any loaded scenes.

Since there wasn't any method call EditorWindow methods from MonoBehaviour without losing information, the only way to communicate was to save data to storage and load them upon starting the __MasterScene__. Thus, __Apply__ button must be pressed if the user want to use current setting on the __Master Settings__ window.

## Files

### MasterSettings.cs (Under Editor folder)
* Responisble for __Master Settings__ UI window. It displays current settings, rendering progress (only after the scene is started) and provides buttons to save, load and apply current settings.

### MasterRenderer.cs (Under StoryGenerator/Scripts/Rendering)
* Upon start, it loads the default master setting file (Config/RenderPreference.json) that is saved by clicking __Apply__ button on __Master Settings__ window.


### RenderSettings.cs (Under StoryGenerator/Scripts/Rendering)
* Provides methods to save/load current __Master Settings__ window.
* Provies methods to sample next scene, character, script and script folder (if done with current current script folder)
* Has RenderProgress class that keeps track of rendered data and their distribution.


### ConvertToVideo.cs (Under StoryGenerator/Scripts/Rendering)
* Calls __frames2mp4.py__ under StoryGenerator/Utilites folder to move generated files and convert frames into video using ffmpeg.

### TestDriver.cs (Under StoryGenerator/Scripts)
* Modified to be compatible with both Master Renderer mode and Single Rendering mode.

### ScriptExecution.cs (Under StoryGenerator/Scripts)
* Modified to be compatible with both Master Renderer mode and Single Rendering mode.


## Misc
* In order to load a scene that is not itself, the scene must be added to __Build Settings__ under __Files__ > __Build Settings__. Current project has all TestScene_X on the build setting
