using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Adaptation : MonoBehaviour
{
    [Header("Inputs")]
    public Transform m_user;
    // Elements 
    public List<ElementModel> m_elements = new List<ElementModel>();
    // Environments 
    public PhysicalEnvironmentManager m_env;

    [Header("Voxel Settings")]
    public Vector3 m_voxelSize;
    public float m_voxelBuffer;
    public Material m_voxelMaterial;
    //public float m_obstacleThreshold;

    [Header("Weights")]
    public float m_weightSemantic;
    public float m_weightCompatibility;
    public float m_weightUtility;
    public float m_weightConsistency;
    public float m_weightStructure;
    public float m_weightOcclusion; 

    [Header("Utility Calculation Weights")]
    public float m_weightUtilitySpatial;
    public float m_weightUtilityObject;
    public float m_weightUtilityMax;
    public float m_weightUtilityCompatibility;

    [Header("Compatibility Calculation Weights")]
    public float m_weightCompatibilityVisibility;
    public float m_weightCompatibilityTouch;

    [Header("Anchor and Avoid Parameters")]
    public float m_weightAnchor;
    public float m_weightAvoid;
    public float m_thresholdAnchor;
    public float m_thresholdAvoid;


    [Header("Optimization")]
    public SocketConnection m_optimizer;

    [Header("Controls")]
    public bool m_setSource;
    private bool m_setSourcePrev; 
    public bool m_setTarget;
    private bool m_setTargetPrev;
    public bool m_optimize;
    private bool m_optimizePrev;

    // User Model 
    private Matrix4x4 m_userSourcePose;
    private Matrix4x4 m_userTargetPose;

    // Voxels 
    private List<VoxelModel[,,]> m_voxels = new List<VoxelModel[,,]>();

    // Occlusions 
    private List<OcclusionModel> m_occlusions = new List<OcclusionModel>();

    // Obstructed assignments
    private List<VoxelModel> m_obstructedAssignments = new List<VoxelModel>();

    private void setSourceInputs()
    {
        // User pose
        m_userSourcePose = AdaptationUtils.getTransformPose(m_user);

        // Elements 
        foreach (ElementModel element in m_elements)
        {
            Vector3 position = element.transform.position;
            Vector3Int voxelDim = AdaptationUtils.getVoxelDim(element.scale, m_voxelSize, m_voxelBuffer, element.m_dimension);
            element.init(position, voxelDim);
        }
    }

    private void setTargetInputs()
    {
        // User pose
        m_userTargetPose = AdaptationUtils.getTransformPose(m_user);

        // Physical Env
        foreach (ObjectModel obj in m_env.m_objects)
        {
            obj.init(obj.transform.position);
        }
        foreach (ContainerModel container in m_env.m_containers)
        {
            container.init(); 
        }
        voxelizeEnvironment();
    }

    private void clearVoxels()
    {
        foreach (VoxelModel[,,] container in m_voxels)
        {
            Vector3Int containerVoxelNum = new Vector3Int(container.GetLength(0), container.GetLength(1), container.GetLength(2));
            for (int xIdx = 0; xIdx < containerVoxelNum.x; xIdx++)
            {
                for (int yIdx = 0; yIdx < containerVoxelNum.y; yIdx++)
                {
                    for (int zIdx = 0; zIdx < containerVoxelNum.z; zIdx++)
                    {
                        VoxelModel voxel = container[xIdx, yIdx, zIdx];
                        GameObject.Destroy(voxel.gameObject);
                    }
                }
            }
        }
        m_voxels.Clear();
    }

    private void voxelizeEnvironment()
    {
        clearVoxels();

        // Voxelixe containers
        int containerNum = m_env.m_containers.Count;
        for (int cIdx = 0; cIdx < containerNum; cIdx++)
        {
            ContainerModel container = m_env.m_containers[cIdx];
            Vector3 containerScale = container.scale;
            Vector3Int containerVoxelNum = new Vector3Int(
                Mathf.RoundToInt(containerScale.x / m_voxelSize.x),
                Mathf.RoundToInt(containerScale.y / m_voxelSize.y),
                Mathf.RoundToInt(containerScale.z / m_voxelSize.z));
            if (container.m_dimension == 2)
                containerVoxelNum.z = 1;
            VoxelModel[,,] containerVoxels = new VoxelModel[containerVoxelNum.x, containerVoxelNum.y, containerVoxelNum.z];
            Vector3 offset = new Vector3(
                Mathf.Floor(containerVoxelNum.x / 2f) - (0.5f * ((containerVoxelNum.x + 1) % 2)),
                Mathf.Floor(containerVoxelNum.y / 2f) - (0.5f * ((containerVoxelNum.y + 1) % 2)),
                Mathf.Floor(containerVoxelNum.z / 2f) - (0.5f * ((containerVoxelNum.z + 1) % 2)));
            for (int xIdx = 0; xIdx < containerVoxelNum.x; xIdx++)
            {
                for (int yIdx = 0; yIdx < containerVoxelNum.y; yIdx++)
                {
                    for (int zIdx = 0; zIdx < containerVoxelNum.z; zIdx++)
                    {
                        Vector3 voxelPosition = new Vector3(
                            m_voxelSize.x * (xIdx - offset.x),
                            m_voxelSize.y * (yIdx - offset.y),
                            m_voxelSize.z * (zIdx - offset.z));
                        voxelPosition = container.transform.TransformPoint(voxelPosition);
                        Vector3 voxelForward = container.transform.forward;
                        Vector3 voxelUp = container.transform.up;

                        Transform voxel = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
                        voxel.SetParent(this.transform);
                        voxel.name = cIdx + ", " + xIdx + ", " + yIdx + ", " + zIdx;
                        voxel.localScale = m_voxelSize;
                        voxel.position = voxelPosition;
                        voxel.rotation = Quaternion.LookRotation(voxelForward, voxelUp);
                        Renderer vr = voxel.GetComponent<Renderer>();
                        vr.material = m_voxelMaterial;
                        voxel.gameObject.layer = AdaptationUtils.VoxelLayer;
                        containerVoxels[xIdx, yIdx, zIdx] = voxel.gameObject.AddComponent<VoxelModel>();
                        containerVoxels[xIdx, yIdx, zIdx].init(cIdx, xIdx, yIdx, zIdx, voxelPosition, voxelForward, container.m_dimension);
                    }
                }
            }
            m_voxels.Add(containerVoxels);
        }
    }

    private void calcOcclusions()
    {
        m_occlusions.Clear();
        List<VoxelModel> occludedVoxels = new List<VoxelModel>();
        foreach (VoxelModel[,,] container in m_voxels)
        {
            Vector3Int containerVoxelNum = new Vector3Int(container.GetLength(0), container.GetLength(1), container.GetLength(2));
            for (int xIdx = 0; xIdx < containerVoxelNum.x; xIdx++)
            {
                for (int yIdx = 0; yIdx < containerVoxelNum.y; yIdx++)
                {
                    for (int zIdx = 0; zIdx < containerVoxelNum.z; zIdx++)
                    {
                        VoxelModel voxel = container[xIdx, yIdx, zIdx];
                        occludedVoxels.Clear();
                        Vector3 toVoxel = (voxel.transform.position - m_user.position);
                        float toVoxelDist = toVoxel.magnitude;
                        toVoxel.Normalize();
                        RaycastHit[] occlusions = Physics.RaycastAll(m_user.position, toVoxel, Mathf.Infinity, 1 << AdaptationUtils.VoxelLayer);
                        foreach (RaycastHit occlusion in occlusions)
                        {
                            VoxelModel occludedVoxel = occlusion.transform.GetComponent<VoxelModel>();
                            if (occludedVoxel != voxel && (occlusion.point - m_user.position).magnitude > toVoxelDist)
                            {
                                if (!occludedVoxels.Contains(occludedVoxel))
                                    occludedVoxels.Add(occludedVoxel);
                            }
                        }
                        foreach (VoxelModel occludedVoxel in occludedVoxels)
                        {
                            m_occlusions.Add(new OcclusionModel(voxel, occludedVoxel));
                        }
                    }
                }
            }
        }
    }

    private void calcObstacles()
    {
        // Avoid obstacles
        m_obstructedAssignments.Clear();
        foreach (ObjectModel obj in m_env.m_objects)
        {
            BoxCollider collider = obj.GetComponentInChildren<BoxCollider>();
            foreach (VoxelModel[,,] container in m_voxels)
            {
                Vector3Int containerVoxelNum = new Vector3Int(container.GetLength(0), container.GetLength(1), container.GetLength(2));
                for (int xIdx = 0; xIdx < containerVoxelNum.x; xIdx++)
                {
                    for (int yIdx = 0; yIdx < containerVoxelNum.y; yIdx++)
                    {
                        for (int zIdx = 0; zIdx < containerVoxelNum.z; zIdx++)
                        {
                            VoxelModel voxel = container[xIdx, yIdx, zIdx];
                            //Collider voxelCollider = voxel.GetComponent<Collider>();
                            // if //(collider.bounds.Contains(voxelCollider.ClosestPoint(obj.transform.position)) || Vector3.Distance(collider.ClosestPoint(voxel.transform.position), voxel.transform.position) < m_obstacleThreshold)
                            if (collider.bounds.Contains(voxel.transform.position)) 
                            {
                                if (!m_obstructedAssignments.Contains(voxel))
                                {
                                    m_obstructedAssignments.Add(voxel);
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    private void placeElements(OptimizationResult[] results)
    {
        Debug.Log("Adaptation.cs: placeElements() - Recieved Results");

        foreach (OptimizationResult result in results)
        {

            // Get element 
            ElementModel element = m_elements[result.eIdx];

            // Place 
            Vector3Int size = element.m_voxelDim;

            List<int> xIdxs = new List<int>();
            if (size.x % 2 == 0)
            {
                xIdxs.Add(result.xIdx + Mathf.FloorToInt(size.x / 2) - 1);
                xIdxs.Add(result.xIdx + Mathf.FloorToInt(size.x / 2));
            }
            else
            {
                xIdxs.Add(result.xIdx + Mathf.FloorToInt(size.x / 2));
            }

            List<int> yIdxs = new List<int>();
            if (size.y % 2 == 0)
            {
                yIdxs.Add(result.yIdx + Mathf.FloorToInt(size.y / 2) - 1);
                yIdxs.Add(result.yIdx + Mathf.FloorToInt(size.y / 2));
            }
            else
            {
                yIdxs.Add(result.yIdx + Mathf.FloorToInt(size.y / 2));
            }

            List<int> zIdxs = new List<int>();
            if (size.z % 2 == 0)
            {
                zIdxs.Add(result.zIdx + Mathf.FloorToInt(size.z / 2) - 1);
                zIdxs.Add(result.zIdx + Mathf.FloorToInt(size.z / 2));
            }
            else
            {
                zIdxs.Add(result.zIdx + Mathf.FloorToInt(size.z / 2));
            }

            List<VoxelModel> centerVoxels = new List<VoxelModel>();
            foreach (int xIdx in xIdxs)
            {
                foreach (int yIdx in yIdxs)
                {
                    foreach (int zIdx in zIdxs)
                    {
                        centerVoxels.Add(m_voxels[result.cIdx][xIdx, yIdx, zIdx]);
                    }
                }
            }
            Vector3 position = Vector3.zero;
            Vector3 forward = Vector3.zero;
            Vector3 up = Vector3.zero;
            float dim = 0;
            foreach (VoxelModel voxel in centerVoxels)
            {
                dim += voxel.m_dimension;
                position += voxel.transform.position;
                forward += voxel.transform.forward;
                up += voxel.transform.up;
            }
            position /= centerVoxels.Count;
            forward /= centerVoxels.Count;
            up /= centerVoxels.Count;
            dim /= centerVoxels.Count;
            dim = Mathf.Round(dim);

            bool snap = false;
            if (dim == 2)
            {
                snap = true;
            }

            if (dim == 3)
            {
                forward = position - m_user.position;
                // TODO: Potentially disable pitch 
                //forward.y = 0;
                forward.Normalize();
            }

            Quaternion rot = Quaternion.LookRotation(forward, up);
            element.transform.SetPositionAndRotation(position, rot);
        }
    }

    IEnumerator optimize()
    {
        m_optimizer.processOptimizationResults = placeElements;

        yield return new WaitForSeconds(0.1f);

        // Parameters
        m_optimizer.sendParams(
            m_weightSemantic,
            m_weightCompatibility,
            m_weightUtility,
            m_weightConsistency,
            m_weightStructure,
            m_weightOcclusion,
            m_weightUtilitySpatial,
            m_weightUtilityObject,
            m_weightUtilityMax,
            m_weightUtilityCompatibility,
            m_weightCompatibilityVisibility,
            m_weightCompatibilityTouch,
            m_weightAnchor,
            m_weightAvoid,
            m_thresholdAnchor,
            m_thresholdAvoid);

        // User
        m_optimizer.sendUser(m_userTargetPose);

        // Elements
        m_optimizer.sendElements(m_userSourcePose, m_elements);

        // Voxels 
        m_optimizer.sendVoxels(m_userTargetPose, m_voxels);
        
        // Objects 
        m_optimizer.sendObjects(m_env.m_objects);

        // Obstructions 
        calcObstacles();
        m_optimizer.sendObstructions(m_obstructedAssignments);
        
        // Occlusions
        calcOcclusions(); 
        m_optimizer.sendOcclusions(m_occlusions);

        // Optimize
        m_optimizer.startOptimization(); 
    }


    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        // Set Source
        if (m_setSource && !m_setSourcePrev)
        {
            setSourceInputs();
            m_setSource = false;
        }

        // Set Target
        if (m_setTarget && !m_setTargetPrev)
        {
            setTargetInputs(); 
            m_setTarget = false;
        }

        // Optimize
        if (m_optimize && !m_optimizePrev)
        {
            StartCoroutine(optimize());
            m_optimize = false;
        }

        m_setSourcePrev = m_setSource;
        m_setTargetPrev = m_setTarget;
        m_optimizePrev = m_optimize;
    }
}
