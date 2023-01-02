using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectModel : MonoBehaviour
{
    [Range(0, 1)]
    public float m_utility;

    [HideInInspector]
    public Vector3 m_position; 

    public void init(Vector3 position)
    {
        m_position = position;
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
