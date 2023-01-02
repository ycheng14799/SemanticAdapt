using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OcclusionModel
{
    private VoxelModel m_voxel, m_occluded;
    public VoxelModel Voxel
    {
        get { return m_voxel; }
    }
    public VoxelModel Occluded
    {
        get { return m_occluded; }
    }

    public OcclusionModel(VoxelModel voxel, VoxelModel occluded)
    {
        m_voxel = voxel;
        m_occluded = occluded;
    }
}
