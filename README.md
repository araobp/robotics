# AI Robot Simulation with Gemini Robotics

This project explores AI robot simulation using the Gemini API.

## Robot 3D Model in Blender

<img src="docs/robot.png" width=200>

The original Blender file is available in this repository: [blender-3d/robots](https://github.com/araobp/blender-3d/tree/main/robots).

[Watch the YouTube video (rendered with Blender's Cycles rendering engine)](https://youtu.be/C_qCqOBFJzs)

## Unity Simulation

Currently, the Unity simulation implementation is in its early stages.

[Access the Unity project code here](./robotics)

### Inverse Kinematics (IK) with Cosine Theorem for the Robot Arm

[Explore the Cosine theorem for Inverse Kinematics implementation](https://github.com/araobp/unity-excavator/blob/master/Excavator/jupyter/IK.ipynb)

### Positioning the robot hand to the work on the table

<img src="docs/P1.jpg" width=600>

<img src="docs/P2.jpg" width=600>

<img src="docs/P3.jpg" width=600>

<img src="docs/P4.jpg" width=600>

<img src="docs/P5.jpg" width=600>

### Gemini API

[`Gemini.cs`](/robotics/Assets/Scripts/Gemini.cs)

**Gemini API Features Explored:**
- [Speech Generation](https://ai.google.dev/gemini-api/docs/speech-generation)

## Robot Scene in Unity

Work on the robot scene in Unity has recently begun.

https://github.com/user-attachments/assets/5b8cf89e-a132-40f8-8c07-1bc9a24380b4

## Robot Scene in Godot (Discontinued)

Initially, Godot was considered for this project. However, due to its limitations in 3D capabilities, development was shifted to Unity. While Godot is a preferred engine for many use cases, it was not suitable for the 3D requirements of this simulation.

https://github.com/user-attachments/assets/52b701a5-fe86-44a4-b6d0-b4fb5e340124

## References

- [Gemini Robotics Overview](https://ai.google.dev/gemini-api/docs/robotics-overview?_gl=1*1lzll2v*_up*MQ..&gclid=CjwKCAiA_orJBhBNEiwABkdmjD7r9CFuZHP7R-rxSMY2zKyRX-Tw3V5xUH2feDADA1mCxsFDtQjQxxoCq18QAvD_BwE&gclsrc=aw.ds&gbraid=0AAAAACn9t67tI7rvJei_ADOP4vnyAYNGl)
