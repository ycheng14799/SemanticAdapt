using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ElementModel : MonoBehaviour
{
    [Range(2, 3)]
    public int m_dimension = 2;

    [Range(0, 1)]
    public float m_visibilityRequirement;

    [Range(0, 1)]
    public float m_touchRequirement;

    [Range(0, 1)]
    public float m_utility;

    [HideInInspector]
    public Vector3 m_position;

    [HideInInspector]
    public Vector3Int m_voxelDim;

    public Vector3 scale
    {
        get { return this.transform.Find("bounds").localScale; }
    }

    public void init(Vector3 position, Vector3Int voxelDim)
    {
        m_position = position;
        m_voxelDim = voxelDim;
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
