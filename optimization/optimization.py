from connection import SockFlags, SocketConnection
from optimizer import Optimizer
import struct
import keyboard 
import time
import numpy as np

# Constants
FLOAT_SIZE = 4
INT_SIZE = 4

params = {}
elements = []
voxels = [] 
objects = []
occlusions = []
user = {}
obstacles = [] 
model = None
sock = None

def setParams(data):
    m_weightSemantic = struct.unpack("f", data[:FLOAT_SIZE])[0]
    data = data[FLOAT_SIZE:]
    m_weightCompatibility = struct.unpack("f", data[:FLOAT_SIZE])[0]
    data = data[FLOAT_SIZE:]
    m_weightUtility = struct.unpack("f", data[:FLOAT_SIZE])[0]
    data = data[FLOAT_SIZE:]
    m_weightConsistency = struct.unpack("f", data[:FLOAT_SIZE])[0]
    data = data[FLOAT_SIZE:]
    m_weightStructure = struct.unpack("f", data[:FLOAT_SIZE])[0]
    data = data[FLOAT_SIZE:]
    m_weightOcclusion = struct.unpack("f", data[:FLOAT_SIZE])[0]
    data = data[FLOAT_SIZE:]
    m_weightUtilitySpatial = struct.unpack("f", data[:FLOAT_SIZE])[0]
    data = data[FLOAT_SIZE:]
    m_weightUtilityObject = struct.unpack("f", data[:FLOAT_SIZE])[0]
    data = data[FLOAT_SIZE:]
    m_weightUtilityMax = struct.unpack("f", data[:FLOAT_SIZE])[0]
    data = data[FLOAT_SIZE:]
    m_weightUtilityCompatibility = struct.unpack("f", data[:FLOAT_SIZE])[0]
    data = data[FLOAT_SIZE:]
    m_weightCompatibilityVisibility = struct.unpack("f", data[:FLOAT_SIZE])[0]
    data = data[FLOAT_SIZE:]
    m_weightCompatibilityTouch = struct.unpack("f", data[:FLOAT_SIZE])[0]
    data = data[FLOAT_SIZE:]
    m_weightAnchor = struct.unpack("f", data[:FLOAT_SIZE])[0]
    data = data[FLOAT_SIZE:]
    m_weightAvoid = struct.unpack("f", data[:FLOAT_SIZE])[0]
    data = data[FLOAT_SIZE:]
    m_thresholdAnchor = struct.unpack("f", data[:FLOAT_SIZE])[0]
    data = data[FLOAT_SIZE:]
    m_thresholdAvoid = struct.unpack("f", data[:FLOAT_SIZE])[0]
    data = data[FLOAT_SIZE:]

    params["w_semantic"] = m_weightSemantic
    params["w_compatibility"] = m_weightCompatibility
    params["w_utility"] = m_weightUtility
    params["w_consistency"] = m_weightConsistency
    params["w_structure"] = m_weightStructure
    params["w_occlusion"] = m_weightOcclusion

    params["w_utilitySpatial"] = m_weightUtilitySpatial
    params["w_utilityObject"] = m_weightUtilityObject
    params["w_utilityMax"] = m_weightUtilityMax
    params["w_utilityCompatibility"] = m_weightUtilityCompatibility

    params["w_compatibilityVisibility"] = m_weightCompatibilityVisibility
    params["w_compatibilityTouch"] = m_weightCompatibilityTouch

    params["w_anchor"] = m_weightAnchor
    params["w_avoid"] = m_weightAvoid
    params["t_anchor"] = m_thresholdAnchor
    params["t_avoid"] = m_thresholdAvoid

def setElements(data):
    elements.clear()
    elementNum = struct.unpack("i", data[:INT_SIZE])[0]
    data = data[INT_SIZE:]
    for eIdx in range(elementNum):
        sizeOfID = struct.unpack("i", data[:INT_SIZE])[0]
        data = data[INT_SIZE:]
        elementID = data[:sizeOfID].decode("utf-8")[len("Element-"):]
        data = data[sizeOfID:]
        dimension = struct.unpack("i", data[:INT_SIZE])[0]
        data = data[INT_SIZE:]
        visibility = struct.unpack("f", data[:FLOAT_SIZE])[0]
        data = data[FLOAT_SIZE:]
        touch = struct.unpack("f", data[:FLOAT_SIZE])[0]
        data = data[FLOAT_SIZE:]
        utility = struct.unpack("f", data[:FLOAT_SIZE])[0]
        data = data[FLOAT_SIZE:]
        position = np.frombuffer(data[:3 * FLOAT_SIZE], dtype=np.float32)
        data = data[3 * FLOAT_SIZE:]
        voxelDim = np.frombuffer(data[:3 * INT_SIZE], dtype=np.int32)
        data = data[3 * INT_SIZE:]
        element = {
            "id": elementID,
            "dimension": dimension,
            "visibility": visibility,
            "touch": touch,
            "utility": utility,
            "position": position,
            "size": voxelDim
        }
        print(element)
        elements.append(element)

def setVoxels(data):
    voxels.clear()
    numContainers = struct.unpack("i", data[:INT_SIZE])[0]
    data = data[INT_SIZE:]
    for cIdx in range(numContainers):
        voxelNum = np.frombuffer(data[:3 * INT_SIZE], dtype=np.int32)
        data = data[3 * INT_SIZE:]
        container = [] 
        for xIdx in range(voxelNum[0]):
            x = [] 
            for yIdx in range(voxelNum[1]):
                y = [] 
                for zIdx in range(voxelNum[2]):
                    voxelId = np.frombuffer(data[:3 * INT_SIZE], dtype=np.int32)
                    data = data[3 * INT_SIZE:]
                    dim = struct.unpack("i", data[:INT_SIZE])[0]
                    data = data[INT_SIZE:]
                    relativePosition = np.frombuffer(data[:3 * FLOAT_SIZE], dtype=np.float32)
                    data = data[3 * FLOAT_SIZE:]
                    position = np.frombuffer(data[:3 * FLOAT_SIZE], dtype=np.float32)
                    data = data[3 * FLOAT_SIZE:]
                    forward = np.frombuffer(data[:3 * FLOAT_SIZE], dtype=np.float32)
                    data = data[3 * FLOAT_SIZE:]
                    y.append({
                        "id": voxelId,
                        "dimensions": dim,
                        "relativePosition": relativePosition, 
                        "position": position, 
                        "forward": forward
                    })
                x.append(y)
            container.append(x)
        voxels.append(np.array(container))

def setObjects(data):
    objects.clear()
    objectNum = struct.unpack("i", data[:INT_SIZE])[0]
    data = data[INT_SIZE:]
    for eIdx in range(objectNum):
        sizeOfID = struct.unpack("i", data[:INT_SIZE])[0]
        data = data[INT_SIZE:]
        objectID = data[:sizeOfID].decode("utf-8")[len("Obj-"):]
        data = data[sizeOfID:]
        utility = struct.unpack("f", data[:FLOAT_SIZE])[0]
        data = data[FLOAT_SIZE:]
        position = np.frombuffer(data[:3 * FLOAT_SIZE], dtype=np.float32)
        data = data[3 * FLOAT_SIZE:]
        obj = {
            "id": objectID,
            "utility": utility,
            "position": position
        }
        objects.append(obj)

def setOcclusions(data):
    occlusions.clear()
    numOcclusions = struct.unpack("i", data[:INT_SIZE])[0]
    data = data[INT_SIZE:]
    for i in range(numOcclusions):
        cId = struct.unpack("i", data[:INT_SIZE])[0]
        data = data[INT_SIZE:]
        voxel = np.frombuffer(data[:3 * INT_SIZE], dtype=np.int32)
        data = data[3 * INT_SIZE:]

        cIdOccluded = struct.unpack("i", data[:INT_SIZE])[0]
        data = data[INT_SIZE:]
        voxelOccluded = np.frombuffer(data[:3 * INT_SIZE], dtype=np.int32)
        data = data[3 * INT_SIZE:]

        occlusion = {
            "cId": cId,
            "voxel": voxel, 
            "cId_occluded": cIdOccluded,
            "voxel_occluded": voxelOccluded
        }
        occlusions.append(occlusion)

def setUser(data):
    position = np.frombuffer(data[:3 * FLOAT_SIZE], dtype=np.float32)
    data = data[3 * FLOAT_SIZE:]
    forward = np.frombuffer(data[:3 * FLOAT_SIZE], dtype=np.float32)
    data = data[3 * FLOAT_SIZE:]
    up = np.frombuffer(data[:3 * FLOAT_SIZE], dtype=np.float32)
    data = data[3 * FLOAT_SIZE:]
    right = np.frombuffer(data[:3 * FLOAT_SIZE], dtype=np.float32)
    
    user["position"] = position
    user["forward"] = forward
    user["up"] = up
    user["right"] = right

def setObstacles(data):
    obstacles.clear()
    numObstacles = struct.unpack("i", data[:INT_SIZE])[0]
    data = data[INT_SIZE:]
    for i in range(numObstacles):
        cId = struct.unpack("i", data[:INT_SIZE])[0]
        data = data[INT_SIZE:]
        voxel = np.frombuffer(data[:3 * INT_SIZE], dtype=np.int32)
        data = data[3 * INT_SIZE:]
        obstacles.append([cId, voxel[0], voxel[1], voxel[2]])

def getResults(results):
    message = bytes([SockFlags.RESULTS.value]) + struct.pack("i", len(results))
    for i, assignment in enumerate(results): 
        #message += struct.pack("i", assignment["element"])
        message += struct.pack("i", i)
        message += struct.pack("i", assignment["assigment"][0])
        message += struct.pack("i", assignment["assigment"][1])
        message += struct.pack("i", assignment["assigment"][2])
        message += struct.pack("i", assignment["assigment"][3])
    sock.send_bytes(message)
    

def optimize():
    model = Optimizer(params, elements, voxels, objects, occlusions, user, obstacles)
    results = model.optimize()

    # Return results
    getResults(results)

def receive(message):
    header = SockFlags(message[0])
    message = message[1:]
    if header == SockFlags.CLOSE:
        SocketConnection.disconnect()
    elif header == SockFlags.SET_PARAMS:
        setParams(message)
    elif header == SockFlags.SET_ELEMENTS:
        setElements(message)
    elif header == SockFlags.SET_VOXELS:
        setVoxels(message)
    elif header == SockFlags.SET_OBJECTS:
        setObjects(message)
    elif header == SockFlags.SET_OCCLUSIONS:
        setOcclusions(message)
    elif header == SockFlags.SET_USER:
        setUser(message)
    elif header == SockFlags.SET_OBSTACLES:
        setObstacles(message)
    elif header == SockFlags.START_OPTIMIZATION:
        optimize()

if __name__ == "__main__":
    sock = SocketConnection(callback=receive)
    print("Connecting...")
    sock.connect()
    print("Connected")
    
    while not keyboard.is_pressed("c"):
        time.sleep(0.1)

    time.sleep(0.1)

    sock.disconnect()