using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class AdaptationUtils
{
    public const int VoxelLayer = 6;

    public static Matrix4x4 getTransformPose(Transform t)
    {
        Vector3 position = t.position;
        Vector3 forward = t.forward;
        forward.y = 0;
        forward.Normalize();
        return Matrix4x4.TRS(position, Quaternion.LookRotation(forward), Vector3.one);
    }

    public static Vector3Int getVoxelDim(Vector3 scale, Vector3 voxelSize, float buffer, int dimension)
    {
        Vector3Int voxelDim = new Vector3Int(
            Mathf.Max(Mathf.RoundToInt((scale.x + buffer) / voxelSize.x), 1),
            Mathf.Max(Mathf.RoundToInt((scale.y + buffer) / voxelSize.y), 1),
            Mathf.Max(Mathf.RoundToInt((scale.z + buffer) / voxelSize.z), 1));
        if (dimension == 2)
            voxelDim.z = 1;

        return voxelDim;
    }
}
