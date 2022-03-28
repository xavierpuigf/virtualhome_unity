# VirtualHome Unity Source Code
This repository contains the source code of the VirtualHome environment, a platform to simulate complex household activities via programs. If you just want to run the simulator you can go to the main [VirtualHome repository](https://github.com/xavierpuigf/virtualhome), containing the VirtualHome API and executables of the simulator for multiple platforms. You can check more information about the project in [virtual-home.org](https://www.virtual-home.org)


## Table of Contents
1. Overview
2. Cite
3. Set Up
4. Testing VirtualHome
5. Documentation 
6. License
7. Contributors

## Overview
VirtualHome is a platform to simulate human activities in household environments. Activities are represented as **activity programs** - lists of actions representing all the steps required to perform a given task. VirtualHome allows executing such programs to generate videos of the given activity. It also allows actions at every single step, and obtaining observations of the environment, making it a suitable platform for RL research.

This repository contains the source code to build the household environments, and translate the activity programs into low level actions that agents can execute. You can use it to modify VirtualHome to fit your research. If you want to use the simulator as it is, you can ignore this repository, and use the [VirtualHome API](https://github.com/xavierpuigf/virtualhome), along with the executables provided.

## Cite
If you use VirtualHome in your research, please consider citing the following paper.

```
@inproceedings{puig2018virtualhome,
  title={Virtualhome: Simulating household activities via programs},
  author={Puig, Xavier and Ra, Kevin and Boben, Marko and Li, Jiaman and Wang, Tingwu and Fidler, Sanja and Torralba, Antonio},
  booktitle={Proceedings of the IEEE Conference on Computer Vision and Pattern Recognition},
  pages={8494--8502},
  year={2018}
}
``` 

## Documentation
You can find more documentation of the VirtualHome executable in the [docs](doc).

## License
VirtualHome is licensed under creative commons. See the License file for more details.


## Contributors
The VirtualHome API and code has been developed by the following people.

- Marko Boben
- Xavier Puig
- Kevin Ra
- Andrew Liao
- Jordan Ren
- Kabir Swain

