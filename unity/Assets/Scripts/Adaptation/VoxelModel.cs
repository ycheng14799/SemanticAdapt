using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VoxelModel : MonoBehaviour
{
    [HideInInspector]
    public int m_cId;

    [HideInInspector]
    public Vector3Int m_voxel;

    [HideInInspector]
    public Vector3 m_position;

    [HideInInspector]
    public int m_dimension;

    [HideInInspector]
    public Vector3 m_forward;

    public void init(int cId, int xIdx, int yIdx, int zIdx, Vector3 position, Vector3 forward, int dimension)
    {
        m_cId = cId;
        m_voxel = new Vector3Int(xIdx, yIdx, zIdx);
        m_position = position;
        m_forward = forward;
        m_dimension = dimension;

    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
