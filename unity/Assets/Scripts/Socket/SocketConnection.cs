using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

public class SocketConnection : MonoBehaviour
{
    private Socket m_s;

    private object _queueLock = new object();
    public delegate void Task();
    private Queue<Task> TaskQueue = new Queue<Task>();
    private const int MAX_TASK_COUNT = 100;

    private IPEndPoint m_endPoint;

    [Header("Network Settings")]
    public string m_ipaddr;
    public string m_port;

    public enum SockFlag
    {
        HEADER,
        CLOSE,
        SET_PARAMS,
        SET_ELEMENTS,
        SET_VOXELS,
        SET_OBJECTS,
        SET_OCCLUSIONS, 
        SET_USER, 
        SET_OBSTACLES,
        START_OPTIMIZATION,
        RESULTS
    }

    public delegate void ProcessOptimizationResults(OptimizationResult[] results);
    public ProcessOptimizationResults processOptimizationResults;

    private void ScheduleTask(Task newTask)
    {
        lock (_queueLock)
        {
            if (TaskQueue.Count < MAX_TASK_COUNT) TaskQueue.Enqueue(newTask);
            else Debug.Log("Reached Task Capacity");
        }
    }

    private void processReceived(byte[] received)
    {
        SockFlag messageType = (SockFlag)received[0];
        switch (messageType)
        {
            case SockFlag.CLOSE:
                disconnect();
                break;
            case SockFlag.RESULTS:
                parseOptimizationResults(received);
                break;
        }
    }

    private void Received()
    {
        while (m_s.Connected)
        {
            byte[] buffer = new byte[1048576];
            try
            {
                int len = m_s.Receive(buffer);
                if (len == 0) break;
                byte[] message;
                int messageStart = 0;
                while (true)
                {
                    if (messageStart >= len)
                        break;
                    SockFlag header = (SockFlag)buffer[messageStart];
                    if (header != SockFlag.HEADER)
                        break;
                    messageStart += 1;
                    int length = BitConverter.ToInt32(buffer, messageStart);
                    messageStart += sizeof(int);
                    message = new byte[length];
                    Buffer.BlockCopy(buffer, messageStart, message, 0, length);
                    processReceived(message);
                    messageStart += length;
                }
            }
            catch
            {

            }
        }
    }

    private void sendBytes(byte[] b)
    {
        if (!m_s.Connected)
            return;

        int length = b.Length;
        byte[] withHeader = new byte[length + sizeof(int) + sizeof(byte)];
        int offset = 0;
        withHeader[offset] = (byte)SockFlag.HEADER;
        offset += 1;
        byte[] lengthBytes = BitConverter.GetBytes(length);
        Buffer.BlockCopy(lengthBytes, 0, withHeader, offset, sizeof(int));
        offset += sizeof(int);
        Buffer.BlockCopy(b, 0, withHeader, offset, length);
        try
        {
            m_s.Send(withHeader);
        }
        catch //(Exception e)
        {

        }
    }

    private byte[] getVector3Bytes(Vector3 vec)
    {
        byte[] bytes = new byte[3 * sizeof(float)];
        for (int i = 0; i < 3; i++)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(vec[i]), 0, bytes, i * sizeof(float), sizeof(float));
        }
        return bytes;
    }

    private byte[] getVector3IntBytes(Vector3Int vec)
    {
        byte[] bytes = new byte[3 * sizeof(float)];
        for (int i = 0; i < 3; i++)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(vec[i]), 0, bytes, i * sizeof(int), sizeof(int));
        }
        return bytes;
    }

    public void sendParams(
        float weightSemantic,
        float weightCompatibility, 
        float weightUtility,
        float weightConsistency,
        float weightStructure,
        float weightOcclusion, 
        float weightUtilitySpatial,
        float weightUtilityObject, 
        float weightUtilityMax, 
        float weightUtilityCompatibility,
        float weightCompatibilityVisibility,
        float weightCompatibilityTouch,
        float weightAnchor,
        float weightAvoid,
        float thresholdAnchor, 
        float thresholdAvoid)
    {
        byte[] message = new byte[sizeof(byte) + 16 * sizeof(float)];
        int offset = 0;
        message[offset] = (byte)SockFlag.SET_PARAMS;
        offset += 1;

        Buffer.BlockCopy(BitConverter.GetBytes(weightSemantic), 0, message, offset, sizeof(float));
        offset += sizeof(float);
        Buffer.BlockCopy(BitConverter.GetBytes(weightCompatibility), 0, message, offset, sizeof(float));
        offset += sizeof(float);
        Buffer.BlockCopy(BitConverter.GetBytes(weightUtility), 0, message, offset, sizeof(float));
        offset += sizeof(float);
        Buffer.BlockCopy(BitConverter.GetBytes(weightConsistency), 0, message, offset, sizeof(float));
        offset += sizeof(float);
        Buffer.BlockCopy(BitConverter.GetBytes(weightStructure), 0, message, offset, sizeof(float));
        offset += sizeof(float);
        Buffer.BlockCopy(BitConverter.GetBytes(weightOcclusion), 0, message, offset, sizeof(float));
        offset += sizeof(float);
        Buffer.BlockCopy(BitConverter.GetBytes(weightUtilitySpatial), 0, message, offset, sizeof(float));
        offset += sizeof(float);
        Buffer.BlockCopy(BitConverter.GetBytes(weightUtilityObject), 0, message, offset, sizeof(float));
        offset += sizeof(float);
        Buffer.BlockCopy(BitConverter.GetBytes(weightUtilityMax), 0, message, offset, sizeof(float));
        offset += sizeof(float);
        Buffer.BlockCopy(BitConverter.GetBytes(weightUtilityCompatibility), 0, message, offset, sizeof(float));
        offset += sizeof(float);
        Buffer.BlockCopy(BitConverter.GetBytes(weightCompatibilityVisibility), 0, message, offset, sizeof(float));
        offset += sizeof(float);
        Buffer.BlockCopy(BitConverter.GetBytes(weightCompatibilityTouch), 0, message, offset, sizeof(float));
        offset += sizeof(float);
        Buffer.BlockCopy(BitConverter.GetBytes(weightAnchor), 0, message, offset, sizeof(float));
        offset += sizeof(float);
        Buffer.BlockCopy(BitConverter.GetBytes(weightAvoid), 0, message, offset, sizeof(float));
        offset += sizeof(float);
        Buffer.BlockCopy(BitConverter.GetBytes(thresholdAnchor), 0, message, offset, sizeof(float));
        offset += sizeof(float);
        Buffer.BlockCopy(BitConverter.GetBytes(thresholdAvoid), 0, message, offset, sizeof(float));
        
        sendBytes(message);
    }

    public void sendUser(Matrix4x4 target)
    {
        Vector3 targetPosition = target.GetPosition();
        Vector3 targetForward = target.MultiplyVector(Vector3.forward).normalized;
        Vector3 targetRight = target.MultiplyVector(Vector3.right).normalized;
        Vector3 targetUp = target.MultiplyVector(Vector3.up).normalized;

        byte[] position = getVector3Bytes(targetPosition);
        byte[] forward = getVector3Bytes(targetForward);
        byte[] up = getVector3Bytes(targetUp);
        byte[] right = getVector3Bytes(targetRight);

        byte[] message = new byte[sizeof(byte) + 4 * 3 * sizeof(float)];
        int offset = 0;
        message[offset] = (byte)SockFlag.SET_USER;
        offset += 1;

        Buffer.BlockCopy(position, 0, message, offset, position.Length);
        offset += position.Length;
        Buffer.BlockCopy(forward, 0, message, offset, forward.Length);
        offset += forward.Length;
        Buffer.BlockCopy(up, 0, message, offset, up.Length);
        offset += up.Length;
        Buffer.BlockCopy(right, 0, message, offset, right.Length);

        sendBytes(message);
    }

    public void sendElements(Matrix4x4 userSourcePose, List<ElementModel> elements)
    {
        Matrix4x4 userSourcePoseInv = userSourcePose.inverse;

        int elementNum = elements.Count;

        // Get identifiers in bytes
        List<byte[]> elementIDs = new List<byte[]>();
        int sizeOfIDs = 0;
        foreach(ElementModel element in elements)
        {
            byte[] id = System.Text.Encoding.UTF8.GetBytes(element.name);
            elementIDs.Add(id);
            sizeOfIDs += id.Length;
        }

        byte[] message = new byte[sizeof(byte) + sizeof(int) + sizeOfIDs + elementNum * (4 * sizeof(int) + 7 * sizeof(float))];
        int offset = 0;
        message[offset] = (byte)SockFlag.SET_ELEMENTS;
        offset += 1;
        Buffer.BlockCopy(BitConverter.GetBytes(elementNum), 0, message, offset, sizeof(int));
        offset += sizeof(int);
        for (int eIdx = 0; eIdx < elementNum; eIdx++)
        {
            byte[] id = elementIDs[eIdx];
            byte[] sizeOfID = BitConverter.GetBytes(id.Length);
            ElementModel element = elements[eIdx];
            byte[] dimension = BitConverter.GetBytes(element.m_dimension);
            byte[] visibilityRequirement = BitConverter.GetBytes(element.m_visibilityRequirement);
            byte[] touchRequirement = BitConverter.GetBytes(element.m_touchRequirement);
            byte[] utility = BitConverter.GetBytes(element.m_utility);
            byte[] position = getVector3Bytes(userSourcePoseInv.MultiplyPoint3x4(element.m_position));
            byte[] voxelDim = getVector3IntBytes(element.m_voxelDim);

            Buffer.BlockCopy(sizeOfID, 0, message, offset, sizeOfID.Length);
            offset += sizeOfID.Length;
            Buffer.BlockCopy(id, 0, message, offset, id.Length);
            offset += id.Length;
            Buffer.BlockCopy(dimension, 0, message, offset, dimension.Length);
            offset += dimension.Length;
            Buffer.BlockCopy(visibilityRequirement, 0, message, offset, visibilityRequirement.Length);
            offset += visibilityRequirement.Length;
            Buffer.BlockCopy(touchRequirement, 0, message, offset, touchRequirement.Length);
            offset += touchRequirement.Length;
            Buffer.BlockCopy(utility, 0, message, offset, utility.Length);
            offset += utility.Length;
            Buffer.BlockCopy(position, 0, message, offset, position.Length);
            offset += position.Length;
            Buffer.BlockCopy(voxelDim, 0, message, offset, voxelDim.Length);
            offset += voxelDim.Length;
        }
        sendBytes(message);
    }

    public void sendVoxels(Matrix4x4 userTargetPose, List<VoxelModel[,,]> voxels)
    {
        Matrix4x4 userTargetPoseInv = userTargetPose.inverse;

        int containerNum = voxels.Count;
        int totalVoxelNum = 0;
        for (int cIdx = 0; cIdx < containerNum; cIdx++)
        {
            VoxelModel[,,] container = voxels[cIdx];
            totalVoxelNum += container.GetLength(0) * container.GetLength(1) * container.GetLength(2);
        }
        byte[] message = new byte[sizeof(byte) +
            sizeof(int) +
            containerNum * 3 * sizeof(int) +
            totalVoxelNum * (sizeof(int) + 3 * sizeof(int) + 3 * 3 * sizeof(float))];
        int offset = 0;
        message[offset] = (byte)SockFlag.SET_VOXELS;
        offset += 1;
        Buffer.BlockCopy(BitConverter.GetBytes(containerNum), 0, message, offset, sizeof(int));
        offset += sizeof(int);
        foreach (VoxelModel[,,] container in voxels)
        {
            // Dimensions 
            Vector3Int dim = new Vector3Int(container.GetLength(0), container.GetLength(1), container.GetLength(2));
            byte[] dimBytes = getVector3IntBytes(dim);
            Buffer.BlockCopy(dimBytes, 0, message, offset, dimBytes.Length);
            offset += dimBytes.Length;

            // Voxel properties
            for (int xIdx = 0; xIdx < dim.x; xIdx++)
            {
                for (int yIdx = 0; yIdx < dim.y; yIdx++)
                {
                    for (int zIdx = 0; zIdx < dim.z; zIdx++)
                    {
                        VoxelModel voxel = container[xIdx, yIdx, zIdx];
                        byte[] index = getVector3IntBytes(new Vector3Int(xIdx, yIdx, zIdx));
                        byte[] voxelDim = BitConverter.GetBytes(voxel.m_dimension);
                        byte[] relativePosition = getVector3Bytes(userTargetPoseInv.MultiplyPoint3x4(voxel.m_position));
                        byte[] position = getVector3Bytes(voxel.m_position);
                        byte[] forward = getVector3Bytes(voxel.m_forward);

                        Buffer.BlockCopy(index, 0, message, offset, index.Length);
                        offset += index.Length;

                        Buffer.BlockCopy(voxelDim, 0, message, offset, voxelDim.Length);
                        offset += voxelDim.Length;

                        Buffer.BlockCopy(relativePosition, 0, message, offset, relativePosition.Length);
                        offset += relativePosition.Length;

                        Buffer.BlockCopy(position, 0, message, offset, position.Length);
                        offset += position.Length;

                        Buffer.BlockCopy(forward, 0, message, offset, forward.Length);
                        offset += forward.Length;
                    }
                }
            }
        }
        sendBytes(message);
    }

    public void sendObjects(List<ObjectModel> objects)
    {
        int objectNum = objects.Count;

        // Get identifiers in bytes
        List<byte[]> objectIDs = new List<byte[]>();
        int sizeOfIDs = 0;
        foreach (ObjectModel obj in objects)
        {
            byte[] id = System.Text.Encoding.UTF8.GetBytes(obj.name);
            objectIDs.Add(id);
            sizeOfIDs += id.Length;
        }

        byte[] message = new byte[sizeof(byte) + sizeof(int) + sizeOfIDs + objectNum * (1 * sizeof(int) + 4 * sizeof(float))];
        int offset = 0;
        message[offset] = (byte)SockFlag.SET_OBJECTS;
        offset += 1;
        Buffer.BlockCopy(BitConverter.GetBytes(objectNum), 0, message, offset, sizeof(int));
        offset += sizeof(int);
        for (int oIdx = 0; oIdx < objectNum; oIdx++)
        {
            byte[] id = objectIDs[oIdx];
            byte[] sizeOfID = BitConverter.GetBytes(id.Length);
            ObjectModel obj = objects[oIdx];
            byte[] position = getVector3Bytes(obj.m_position);
            byte[] utility = BitConverter.GetBytes(obj.m_utility);

            Buffer.BlockCopy(sizeOfID, 0, message, offset, sizeOfID.Length);
            offset += sizeOfID.Length;
            Buffer.BlockCopy(id, 0, message, offset, id.Length);
            offset += id.Length;
            Buffer.BlockCopy(utility, 0, message, offset, utility.Length);
            offset += utility.Length;
            Buffer.BlockCopy(position, 0, message, offset, position.Length);
            offset += position.Length;
        }
        sendBytes(message);
    }

    public void sendOcclusions(List<OcclusionModel> occlusions)
    {
        int numOcclusions = occlusions.Count;
        byte[] message = new byte[sizeof(byte) + sizeof(int) + numOcclusions * 2 * 4 * sizeof(int)];
        int offset = 0;
        message[offset] = (byte)SockFlag.SET_OCCLUSIONS;
        offset += 1;
        Buffer.BlockCopy(BitConverter.GetBytes(numOcclusions), 0, message, offset, sizeof(int));
        offset += sizeof(int);
        foreach (OcclusionModel occlusion in occlusions)
        {
            VoxelModel voxel = occlusion.Voxel;
            Buffer.BlockCopy(BitConverter.GetBytes(voxel.m_cId), 0, message, offset, sizeof(int));
            offset += sizeof(int);
            Buffer.BlockCopy(getVector3IntBytes(voxel.m_voxel), 0, message, offset, 3 * sizeof(int));
            offset += 3 * sizeof(int);

            VoxelModel occluded = occlusion.Occluded;
            Buffer.BlockCopy(BitConverter.GetBytes(occluded.m_cId), 0, message, offset, sizeof(int));
            offset += sizeof(int);
            Buffer.BlockCopy(getVector3IntBytes(occluded.m_voxel), 0, message, offset, 3 * sizeof(int));
            offset += 3 * sizeof(int);
        }
        sendBytes(message);
    }

    public void sendObstructions(List<VoxelModel> obstructed)
    {
        int numObstructions = obstructed.Count;
        byte[] message = new byte[sizeof(byte) + sizeof(int) + numObstructions * 4 * sizeof(int)];
        int offset = 0;
        message[offset] = (byte)SockFlag.SET_OBSTACLES;
        offset += 1;
        Buffer.BlockCopy(BitConverter.GetBytes(numObstructions), 0, message, offset, sizeof(int));
        offset += sizeof(int);
        foreach (VoxelModel voxel in obstructed)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(voxel.m_cId), 0, message, offset, sizeof(int));
            offset += sizeof(int);
            Buffer.BlockCopy(getVector3IntBytes(voxel.m_voxel), 0, message, offset, 3 * sizeof(int));
            offset += 3 * sizeof(int);
        }
        sendBytes(message);
    }

    public void startOptimization()
    {
        byte[] message = new byte[] { (byte)SockFlag.START_OPTIMIZATION };
        sendBytes(message);
    }

    public void parseOptimizationResults(byte[] results)
    {
        int offset = 1;
        int assignmentNum = BitConverter.ToInt32(results, offset);
        offset += sizeof(int);

        OptimizationResult[] optimizationResults = new OptimizationResult[assignmentNum];
        for (int eIdx = 0; eIdx < assignmentNum; eIdx++)
        {
            int elementId = BitConverter.ToInt32(results, offset);
            offset += sizeof(int);
            int cIdx = BitConverter.ToInt32(results, offset);
            offset += sizeof(int);
            int xIdx = BitConverter.ToInt32(results, offset);
            offset += sizeof(int);
            int yIdx = BitConverter.ToInt32(results, offset);
            offset += sizeof(int);
            int zIdx = BitConverter.ToInt32(results, offset);
            offset += sizeof(int);
            optimizationResults[eIdx] = new OptimizationResult(elementId, cIdx, xIdx, yIdx, zIdx);
        }

        if (processOptimizationResults != null)
        {
            ScheduleTask(new Task(delegate {
                processOptimizationResults(optimizationResults);
            }));
        }

    }

    private void connect()
    {
        try
        {
            m_s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            m_s.Connect(m_endPoint);
            Debug.Log("Connection OK");
            Thread receiveThread = new Thread(Received);
            receiveThread.IsBackground = true;
            receiveThread.Start();
        }
        catch (Exception ex)
        {
            Debug.Log("Connection FAIL - " + ex);
        }

    }

    private void disconnect()
    {
        Debug.Log("Socket Disconnecting");
        if (m_s != null)
        {
            m_s.Close();
        }
    }

    private void tryConnect()
    {
        Thread connectThread = new Thread(connect);
        connectThread.IsBackground = true;
        connectThread.Start();
    }

    private void init()
    {
        int port = int.Parse(m_port);
        IPAddress ipaddr = IPAddress.Parse(m_ipaddr);
        m_endPoint = new IPEndPoint(ipaddr, int.Parse(m_port));

        tryConnect();
    }

    // Start is called before the first frame update
    void Start() 
    {
        init();
    }

    // Update is called once per frame
    void Update()
    {
        lock (_queueLock)
        {
            if (TaskQueue.Count > 0)
            {
                TaskQueue.Dequeue()();
            }
        }

    }

    private void OnApplicationQuit()
    {
        if (m_s != null)
        {
            m_s.Close();
        }
    }
}
