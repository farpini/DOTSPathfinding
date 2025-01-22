# DOTS Pathfinding
-----------------------------------------------
![](https://github.com/farpini/DOTSPathfinding/blob/main/TitleImage.JPG)<br />

This project is a prototype to perform multiple pathfinding operations with high-performance using Unity DOTS features.<br />
It's initially based on the Sebastian Lague A* Pathfiding series, but with heap optimization recoded in a data structure way to be compatible with burst compiler and parallel jobs.
It also have been designed using of ECS architecture. The agents are created as entities instead of gameobjects, and pathfinding computation in job systems.

The pathfinding algorithm run in an abstraction of a grid map (map sizes can be selected in the Main Controller component).

Obstacles can be added/removed on the map before or while agents are being spawned or performing pathing.

![](https://github.com/farpini/DOTSPathfinding/blob/main/Obstacle.gif)<br />

Agents are spawned at random positions and they will start continuously requesting to the pathfinding system a path to a random target position.

![](https://github.com/farpini/DOTSPathfinding/blob/main/Performance.gif)<br />

Large maps:

![](https://github.com/farpini/DOTSPathfinding/blob/main/LargeMaps.JPG)<br />

-----------------------------------------------
To be implemented:
- check invalid path waypoints due to obstacle placement/removed at runtime and request a new path.
