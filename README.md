# SemanticAdapt
![SemanticAdapt](https://augmented-perception.org/assets/publications/2021-semanticadapt.png)
**Authors:** Yi Fei Cheng, Yukang Yan, Xin Yi, Yuanchun Shi, David Lindlbauer

**Publication:** ACM UIST, October 2021

**Project page:** [https://augmented-perception.org/publications/2021-semanticadapt.html](https://augmented-perception.org/publications/2021-semanticadapt.html)

## Project description
We present an optimization-based approach that automatically adapts Mixed Reality (MR) interfaces to different physical environments. Current MR layouts, including the position and scale of virtual interface elements, need to be manually adapted by users whenever they move between environments, and whenever they switch tasks. This process is tedious and time consuming, and arguably needs to be automated by MR systems for them to be beneficial for end users. We contribute an approach that formulates this challenges as combinatorial optimization problem and automatically decides the placement of virtual interface elements in new environments. In contrast to prior work, we exploit the semantic association between the virtual interface elements and physical objects in an environment. Our optimization furthermore considers the utility of elements for users' current task, layout factors, and spatio-temporal consistency to previous environments. All those factors are combined in a single linear program, which is used to adapt the layout of MR interfaces in real time. We demonstrate a set of application scenarios, showcasing the versatility and applicability of our approach. Finally, we show that compared to a naive adaptive baseline approach that does not take semantic association into account, our approach decreased the number of manual interface adaptations by 37%.

## Code 
This repository contains the code published alongside with our UIST 2021 [paper](https://dl.acm.org/doi/10.1145/3472749.3474750). It is organized as follows: [`optimization`](https://github.com/ycheng14799/SemanticAdapt/tree/main/optimization) contains our optimization implementation, [`semantic-connections`](https://github.com/ycheng14799/SemanticAdapt/tree/main/semantic-connections) includes a jupyter notebook for generating anchoring and avoidance associations, and [`unity`](https://github.com/ycheng14799/SemanticAdapt/tree/main/unity) contains a Unity project demonstrating how our optimization procedure can be used to adapt interface layouts between different physical environments.  

[`optimization`](https://github.com/ycheng14799/SemanticAdapt/tree/main/optimization) dependencies and requirements: [python3](https://www.python.org/), [Gurobi 9.5.2](https://www.gurobi.com/), [pandas](https://pandas.pydata.org/), [keyboard](https://pypi.org/project/keyboard/), [numpy](https://numpy.org/).

[`semantic-connections`](https://github.com/ycheng14799/SemanticAdapt/tree/main/optimization) dependencies and requirements: [python3](https://www.python.org/), [pandas](https://pandas.pydata.org/), [numpy](https://numpy.org/), [NLTK](https://www.nltk.org/).

[`unity`](https://github.com/ycheng14799/SemanticAdapt/tree/main/unity) was developed in [Unity 2021.3.6f1](https://unity.com/).

## Usage
The current project contains a simple functional example. To see a basic example of our optimization procedure, follow the steps described below: 

1. Navigate to `optimization` and execute `python optimization.py`. This starts a server which hosts our gurobi optimization procedure. 
2. Open the `unity` project in Unity and start the application. 
3. \[Optional\] Adjust the initial placement of the interface elements using the `Scene` view. 
4. \[Optional\] Select an initial environment using the `Controls` in the `Physical Environment Manager` component attached to the `Environments` object. 
5. Click `Set Source` in the `Adaptation` component attached to the `Adaptation` object. This specifies the input environment and interface arrangement for our optimization procedure.
6. \[Optional\] Select a target environment using the `Controls` in the `Physical Environment Manager` component attached to the `Environments` object. 
7. Click `Set Target` in the `Adaptation` component attached to the `Adaptation` object. This specifies the target environment for our optimization procedure.
8. Click `Optimize` in the `Adaptation` component attached to the `Adaptation` object. This starts the optimization procedure. Interface element placements will be updated automatically after an optimal solution is obtained.

## Citation
If you use this code or data for your own work, please use the following citation:
```
@inproceedings{10.1145/3472749.3474750,
author = {Cheng, Yifei and Yan, Yukang and Yi, Xin and Shi, Yuanchun and Lindlbauer, David},
title = {SemanticAdapt: Optimization-Based Adaptation of Mixed Reality Layouts Leveraging Virtual-Physical Semantic Connections},
year = {2021},
isbn = {9781450386357},
publisher = {Association for Computing Machinery},
address = {New York, NY, USA},
url = {https://doi.org/10.1145/3472749.3474750},
doi = {10.1145/3472749.3474750},
abstract = {We present an optimization-based approach that automatically adapts Mixed Reality (MR) interfaces to different physical environments. Current MR layouts, including the position and scale of virtual interface elements, need to be manually adapted by users whenever they move between environments, and whenever they switch tasks. This process is tedious and time consuming, and arguably needs to be automated for MR systems to be beneficial for end users. We contribute an approach that formulates this challenge as a combinatorial optimization problem and automatically decides the placement of virtual interface elements in new environments. To achieve this, we exploit the semantic association between the virtual interface elements and physical objects in an environment. Our optimization furthermore considers the utility of elements for users’ current task, layout factors, and spatio-temporal consistency to previous layouts. All those factors are combined in a single linear program, which is used to adapt the layout of MR interfaces in real time. We demonstrate a set of application scenarios, showcasing the versatility and applicability of our approach. Finally, we show that compared to a naive adaptive baseline approach that does not take semantic associations into account, our approach decreased the number of manual interface adaptations by 33\%.},
booktitle = {The 34th Annual ACM Symposium on User Interface Software and Technology},
pages = {282–297},
numpages = {16},
keywords = {Adaptive user interfaces, Computational interaction, Mixed Reality},
location = {Virtual Event, USA},
series = {UIST '21}
}
```


