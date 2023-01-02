using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ContainerModel : MonoBehaviour
{
    [Range(2, 3)]
    public int m_dimension = 2;

    public Vector3 scale
    {
        get { return this.transform.Find("bounds").localScale; }
    }

    public void init()
    {

    }
}
