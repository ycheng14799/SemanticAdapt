using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PhysicalEnvironmentManager : MonoBehaviour
{
    public List<Transform> m_environments; 
    public enum Environment { Bedroom, OpenOffice, CoffeeShop, StudyRoom };

    public List<ContainerModel> m_containers;
    public List<ObjectModel> m_objects;

    [Header("Controls")]
    public Environment m_evironment;
    public bool m_clearEnvironment;
    public bool m_useEnvironment; 

    public void clearEnv()
    {
        foreach (Transform env in m_environments)
        {
            env.gameObject.SetActive(false);
        }
        m_containers.Clear();
        m_objects.Clear();
    }

    public void useEnv()
    {
        clearEnv();

        GameObject env = m_environments[(int)m_evironment].gameObject;
        env.SetActive(true);
        Transform containers = env.transform.Find("containers");
        foreach (Transform container in containers)
        {
            m_containers.Add(container.GetComponent<ContainerModel>());
        }
        Transform objects = env.transform.Find("objects");
        foreach (Transform obj in objects)
        {
            m_objects.Add(obj.GetComponent<ObjectModel>());
        }
    }


    // Start is called before the first frame update
    void Start()
    {
        useEnv();
    }

    // Update is called once per frame
    void Update()
    {
        if (m_clearEnvironment)
        {
            clearEnv();
            m_clearEnvironment = false;
        }

        if (m_useEnvironment)
        {
            useEnv();
            m_useEnvironment = false;
        }
    }
}
