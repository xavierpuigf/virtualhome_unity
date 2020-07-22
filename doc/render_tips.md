# Execution tips
This section contains some tips to speed up rendering and execution in VirtualHome. We generally recommend to [build an executable](build_exec) when interacting with VirtualHome, instead of using the editor. This will be faster and will allow you to render in a server, or share the simulator without others purchasing assets.

At times, you may want to render in the Unity Editor, to debug or test improvements in the simulator. Executing or rendering in the editor is generally slower than using the executable. Here are two tips to speed things up if you are testing the tool.


## Tips to boost Unity Editor Performance
* Disable scene lighting (it should be toggled off)
![alt text](assets/scene_lighting.png "Scene Lighting")
* Hide Game Window
  * Doesn't matter when you are rendering but you are viewing or editing the scene, not showing the __Game Window__ significantly increases responsiveness (refer to the image below)
  ![alt text](assets/hide_game_view.png "Workspace")
  * Note that your window layout might be different from the image. This is just for reference. 