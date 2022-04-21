# SMPL-X for Unity

This package allows you to add [SMPL-X](https://smpl-x.is.tue.mpg.de) skinned meshes to your current Unity project. Each added SMPL-X mesh consists of a shape specific rig, as well as shape keys (blend shapes) for shape, expression and pose correctives.

+ Requirements: Unity 2020.3.8+
+ Additional dependencies: None
+ Used SMPL-X model: SMPL-X v1.1 with 10 shape components, 10 expression components

## Features
+ Add female/male/neutral specific SMPL-X mesh to current scene
    + Model options
        + Full: Shape (10) + expression (10) + pose correctives (486)
        + Medium: Shape (10) + Expression (10), no pose correctives
        + Basic: no shape, no expression, no pose correctives
+ Enable/disable auto corrective poseshapes
+ Set sample albedo texture
+ Position feet on ground plane (y=0)
+ Randomize/reset shape
+ Update joint locations on shape change
+ Randomize/reset face expression shape
+ Change hand pose (flat, relaxed)
+ Change body pose (T-pose, A-pose)
+ Show joint locations
+ Custom inspector GUI

## Usage
+ Open Scenes/SampleScene to see an example of SMPL-X models in a Unity scene
+ We recommend to use the Prefabs from SMPLX/Prefabs/ to add the SMPL-X model of choice to your custom scene
    + These Prefabs have the SMPLX Component attached with proper gender setup
    + Skinned Mesh Renderer is setup to use 4 bones skinning quality
    + Models are positioned so that feet are on the ground (y=0)
    + Models are looking along the negative z axis to face the default Unity camera
    + Available versions:
        + smplx-*: Shape (10), expression (10), pose correctives (486)
            + This is the full SMPL-X model and provides highest visual quality
        + smplx-*-se : Shape (10), expression (10), no pose correctives
        + smplx-*-basic: No shape, no expression, no pose correctives
            + Lightweight high performance model
            + Pivot is at the bottom of the feet to facilitate scene placement


## License
+ Licensed under SMPL-X Model License
    + https://smpl-x.is.tue.mpg.de/modellicense

+ Attribution for publications: 
    + You agree to cite the most recent paper describing the model as specified on the SMPL-X website: https://smpl-x.is.tue.mpg.de

## Code
+ https://gitlab.tuebingen.mpg.de/jtesch/smplx-unity
    + Code-only repository without SMPL-X model files

## Known issues
+ When modifying the shape parameters of an instanced SMPL-X model for the first time, the skinned mesh asset will be copied so that new joint locations can be applied without affecting other body instances. When doing this for mulitple SMPL-X models in the same scene, it will significantly increase the Unity scene file and introduce a long waiting time when saving the scene.

## Acknowledgements
+ We thank [Meshcapade](https://meshcapade.com/) for providing the SMPL-X female/male sample textures (`smplx_texture_f_alb.png`, `smplx_texture_m_alb.png`) under [Attribution-NonCommercial 4.0 International (CC BY-NC 4.0)](https://creativecommons.org/licenses/by-nc/4.0/) license.

+ Sergey Prokudin (rainbow texture data)

+ Vassilis Choutas (betas-to-joints regressor)

## Contact
+ Joachim Tesch (smplx@tue.mpg.de)
