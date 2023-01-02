import numpy as np
import itertools 
from gurobipy import * 
from enum import Enum
import pandas as pd

class Optimizer:
	def __init__(self, params={}, elements=[], voxels=[], objects=[], occlusions=[], user={}, obstacles=[], fNameAnchor="elementObjAnchor.csv", fNameAvoid="elementObjAvoid.csv"):
		self._params = params 	
		self._elements = elements
		self._voxels = voxels
		self._objects = objects
		self._occlusions = occlusions
		self._user = user
		self._obstacles = obstacles
		self._anchorAssociations = pd.read_csv(fNameAnchor,index_col=0)
		self._avoidAssociations = pd.read_csv(fNameAvoid,index_col=0)

	def smoothstep(self, edge0, edge1, x):
		result = np.clip((x - edge0) / (edge1 - edge0), 0, 1)
		return result * result * (3 - (2 * result))

	def computeVisibility(self, voxel):
		toVoxel = voxel["position"] - self._user["position"]
		toVoxelNorm = np.linalg.norm(toVoxel)
		if toVoxelNorm <= 0:
			return 0 
		toVoxel /= toVoxelNorm

		# Based on models of human visual field
		# Returns 0 when angular difference between user forward and voxel is > 60 degrees
		# Returns 1 when angular difference between user forward and voxel is < 2 degrees 
		# Smoothstep interpolation in between values
		dot = np.clip(np.dot(self._user["forward"], toVoxel), -1, 1)
		score = self.smoothstep(np.cos(np.deg2rad(60)), np.cos(np.deg2rad(2)), dot)
		return score 

	def computeReachability(self, voxel):
		toVoxel = voxel["position"] - self._user["position"]
		d = np.linalg.norm(toVoxel)

		# Based on Hincapie-Ramos (Consumed Endurance)
		# 0.8 m approximates out of reach  
		# Modeled with a sigmoid function
		# Returns 1 when distance = 0 
		# Decrease following a sigmoid function centered around 0.8: returns 0.5 when distance = 0.8
		score = 1 / (1 + np.exp(10 * (d - 0.8)))
		return score 

	def computeEnvVisibility(self):
		for c, container in enumerate(self._voxels):
			cDims = container.shape
			for xIdx, yIdx, zIdx in itertools.product(range(cDims[0]), range(cDims[1]), range(cDims[2])):
				voxel = container[xIdx, yIdx, zIdx]
				voxel["visibility"] = self.computeVisibility(voxel)

	def computeEnvReachability(self):
		for c, container in enumerate(self._voxels):
			cDims = container.shape
			for xIdx, yIdx, zIdx in itertools.product(range(cDims[0]), range(cDims[1]), range(cDims[2])):
				voxel = container[xIdx, yIdx, zIdx]
				voxel["reachability"] = self.computeReachability(voxel)

	def computeCompatibility(self, element, voxel):
		compatibilityVisible = element["visibility"] * voxel["visibility"]
		compatibilityTouch = element["touch"] * voxel["reachability"]
		score = 0
		score += self._params["w_compatibilityVisibility"] * compatibilityVisible
		score += self._params["w_compatibilityTouch"] * compatibilityTouch
		return score 

	def computeSpatialUtility(self, voxel):
		toVoxel = voxel["position"] - self._user["position"]
		toVoxel[1] = 0 

		# Distance-based utility
		# 0.5 m approximates within reach, arm slightly bent 
		# Modeled with an exponential function 
		# Optimal at 0.5 m 
		# Lowered utility when either too close or too far 
		dist = np.linalg.norm(toVoxel)
		distUtility = np.exp(-np.power(dist - 0.5, 4)/0.05)

		# Direction-based utility
		direction = toVoxel / dist
		forward = np.dot(direction, self._user["forward"]) 
		right = np.dot(direction, self._user["right"])
		dirUtility = 0
		if abs(forward) > abs(right):
			if forward > 0:
				dirUtility += 1.
		else:
			dirUtility += 0.5

		# Height-based utility 
		hDiff = abs(voxel["position"][1] - self._user["position"][1])
		hUtility = 1 / (1 + np.exp(10 * (hDiff - 0.8)))

		return (distUtility + dirUtility + hUtility) / 3.

	def computeObjectUtility(self, voxel):
		utility = 0.0 
		for obj in self._objects: 
			objUtility =  obj["utility"] * np.exp(-5 * np.linalg.norm(voxel["position"] - obj["position"]))
			utility = max(objUtility, utility)
		return utility

	def computeUtility(self, voxel):
		spatialUtility = self.computeSpatialUtility(voxel)
		objUtility = self.computeObjectUtility(voxel)
		utility = (self._params["w_utilitySpatial"] * spatialUtility + self._params["w_utilityObject"] * objUtility) / (self._params["w_utilitySpatial"] + self._params["w_utilityObject"])
		return utility

	def computeEnvUtility(self):
		for c, container in enumerate(self._voxels):
			cDims = container.shape
			for xIdx, yIdx, zIdx in itertools.product(range(cDims[0]), range(cDims[1]), range(cDims[2])):
				voxel = container[xIdx, yIdx, zIdx]
				voxel["utility"] = self.computeUtility(voxel)

	def computeObjectAssociations(self, voxel):
		associations = {}
		for obj in self._objects: 
			associations[obj["id"]] = np.exp(-5 * np.linalg.norm(voxel["position"] - obj["position"]))
		return associations

	def computeEnvObjectAssociations(self):
		for c, container in enumerate(self._voxels):
			cDims = container.shape
			for xIdx, yIdx, zIdx in itertools.product(range(cDims[0]), range(cDims[1]), range(cDims[2])):
				voxel = container[xIdx, yIdx, zIdx]
				voxel["associations"] = self.computeObjectAssociations(voxel)

	def computeSemanticBehavior(self, element, voxel):
		anchoring = 0.0
		for obj in self._objects:
			association = self._anchorAssociations[element["id"]][obj["id"]]
			if association < self._params["t_anchor"]:
				continue
			anchoring = max(anchoring, voxel["associations"][obj["id"]] * association)

		avoiding = 0.0
		for obj in self._objects:
			association = self._avoidAssociations[element["id"]][obj["id"]]
			if association < self._params["t_avoid"]:
				continue
			avoiding = max(avoiding, voxel["associations"][obj["id"]] * association)

		return self._params["w_anchor"] * anchoring - self._params["w_avoid"] * avoiding


	def computeSpatialConstancy(self, element, voxel):
		elementToVoxel = voxel["relativePosition"] - element["position"]
		d = np.linalg.norm(elementToVoxel)
		score = np.clip((np.exp(-5*d) - np.exp(-5)) / (1 - np.exp(-5)), 0, 1)
		return score
	
	def optimize(self):
		print("optimize")

		model = Model("semantic-adapt")

		validDims = {}

		# Decision variables
		x = {} 
		voxels = {}
		for c, container in enumerate(self._voxels):
			cDims = container.shape
		for e, element in enumerate(self._elements):
			for c, container in enumerate(self._voxels):
				cDims = container.shape
				validDims[e,c] = cDims - element["size"] + np.ones(3, dtype=int)
				for xIdx, yIdx, zIdx in itertools.product(range(validDims[e,c][0]), range(validDims[e,c][1]), range(validDims[e,c][2])):
					x[e, c, xIdx, yIdx, zIdx] = model.addVar(vtype=GRB.BINARY, name="x_%s_%s_%s_%s_%s" % (element["id"], c, xIdx, yIdx, zIdx))
				for xIdx, yIdx, zIdx in itertools.product(range(cDims[0]), range(cDims[1]), range(cDims[2])):
					voxels[e, c, xIdx, yIdx, zIdx] = model.addVar(vtype=GRB.BINARY, name="voxel_%s_%s_%s_%s_%s" % (element["id"], c, xIdx, yIdx, zIdx))
		occupanices = {} 
		for c, container in enumerate(self._voxels):
			cDims = container.shape
			for xIdx, yIdx, zIdx in itertools.product(range(cDims[0]), range(cDims[1]), range(cDims[2])):
				occupanices[c, xIdx, yIdx, zIdx] = model.addVar(vtype=GRB.BINARY, name="occupied_%s_%s_%s_%s" % (c, xIdx, yIdx, zIdx))
		occlusions = []
		for o, occlusion in enumerate(self._occlusions):
			occlusions.append(model.addVar(vtype=GRB.BINARY, name="occlusion_%s" % (o)))
		hNeighbors = {}
		vNeighbors = {} 
		for c, container in enumerate(self._voxels):
			cDims = container.shape
			for xIdx, yIdx, zIdx in itertools.product(range(cDims[0] - 1), range(cDims[1]), range(cDims[2])):
				hNeighbors[c, xIdx, yIdx, zIdx] = model.addVar(vtype=GRB.BINARY, name="hNeighbors_%s_%s_%s_%s" % (c, xIdx, yIdx, zIdx))
			for xIdx, yIdx, zIdx in itertools.product(range(cDims[0]), range(cDims[1] - 1), range(cDims[2])):
				vNeighbors[c, xIdx, yIdx, zIdx] = model.addVar(vtype=GRB.BINARY, name="vNeighbors_%s_%s_%s_%s" % (c, xIdx, yIdx, zIdx))

		# Constraints 
		# Each element assigned once 
		for e, element in enumerate(self._elements):
			lhs = 0
			for c, container in enumerate(self._voxels):
				for xIdx, yIdx, zIdx in itertools.product(range(validDims[e,c][0]), range(validDims[e,c][1]), range(validDims[e,c][2])):
					lhs += x[e, c, xIdx, yIdx, zIdx]
			model.addConstr(lhs == 1, "element_assignment_%s" % (e))
		
		# Elements spanning multiple voxels
		for e, element in enumerate(self._elements):
			size = element["size"]
			for c, container in enumerate(self._voxels):
				for xIdx, yIdx, zIdx in itertools.product(range(validDims[e,c][0]), range(validDims[e,c][1]), range(validDims[e,c][2])):
					for exIdx, eyIdx, ezIdx in itertools.product(range(size[0]), range(size[1]), range(size[2])):
						model.addGenConstrIndicator(x[e, c, xIdx, yIdx, zIdx], True, voxels[e, c, xIdx + exIdx, yIdx + eyIdx, zIdx + ezIdx] == 1)
		for e, element in enumerate(self._elements):
			lhs = 0
			for c, container in enumerate(self._voxels):
				cDims = container.shape
				for xIdx, yIdx, zIdx in itertools.product(range(cDims[0]), range(cDims[1]), range(cDims[2])):
					lhs += voxels[e, c, xIdx, yIdx, zIdx]
			size = element["size"]
			totalVoxels = size[0] * size[1] * size[2]
			model.addConstr(lhs <= totalVoxels, "multi_assignment_%s" % (e))

		# Overlaps
		for c, container in enumerate(self._voxels):
			cDims = container.shape
			for xIdx, yIdx, zIdx in itertools.product(range(cDims[0]), range(cDims[1]), range(cDims[2])):
				model.addConstr(quicksum([voxels[e, c, xIdx, yIdx, zIdx] for e, element in enumerate(self._elements)]), GRB.LESS_EQUAL, 1, name="overlap_%s_%s_%s_%s" % (c, xIdx, yIdx, zIdx))
		
		# Obstacles 
		for obstacle in self._obstacles: 
			#print(obstacle[0], obstacle[1], obstacle[2], obstacle[3])
			for e, element in enumerate(self._elements):
				model.addConstr(voxels[e, obstacle[0], obstacle[1], obstacle[2], obstacle[3]], GRB.LESS_EQUAL, 0, name="obstacle__%s_%s_%s_%s_%s" % (e, obstacle[0], obstacle[1], obstacle[2], obstacle[3]))

		# Occupancies
		for c, container in enumerate(self._voxels):
			cDims = container.shape
			for xIdx, yIdx, zIdx in itertools.product(range(cDims[0]), range(cDims[1]), range(cDims[2])):
				model.addGenConstrOr(occupanices[c, xIdx, yIdx, zIdx], [voxels[e, c, xIdx, yIdx, zIdx] for e, element in enumerate(self._elements)])

		# Occlusions 
		for o, occlusion in enumerate(self._occlusions):
			model.addGenConstrAnd(occlusions[o], 
				[occupanices[occlusion["cId"], occlusion["voxel"][0], occlusion["voxel"][1], occlusion["voxel"][2]], 
				occupanices[occlusion["cId_occluded"], occlusion["voxel_occluded"][0], occlusion["voxel_occluded"][1], occlusion["voxel_occluded"][2]]])

		# Neighbors 
		for c, container in enumerate(self._voxels):
			cDims = container.shape
			for xIdx, yIdx, zIdx in itertools.product(range(cDims[0] - 1), range(cDims[1]), range(cDims[2])):
				model.addGenConstrAnd(hNeighbors[c, xIdx, yIdx, zIdx], [occupanices[c, xIdx, yIdx, zIdx], occupanices[c, xIdx + 1, yIdx, zIdx]])
			for xIdx, yIdx, zIdx in itertools.product(range(cDims[0]), range(cDims[1] - 1), range(cDims[2])):
				model.addGenConstrAnd(vNeighbors[c, xIdx, yIdx, zIdx], [occupanices[c, xIdx, yIdx, zIdx], occupanices[c, xIdx, yIdx + 1, zIdx]])

		totalElementVoxels = np.sum([(element["size"][0] * element["size"][1] * element["size"][2]) for e, element in enumerate(self._elements)])

		# Objectives

		# Semantic Term 
		self.computeEnvObjectAssociations()
		semanticTerm = 0.0 
		for e, element in enumerate(self._elements):
			size = element["size"]
			for c, container in enumerate(self._voxels):
				for xIdx, yIdx, zIdx in itertools.product(range(validDims[e,c][0]), range(validDims[e,c][1]), range(validDims[e,c][2])):
					semanticBehavior = 0.0 
					for exIdx, eyIdx, ezIdx in itertools.product(range(size[0]), range(size[1]), range(size[2])):
						semanticBehavior += self.computeSemanticBehavior(element, container[xIdx + exIdx, yIdx + eyIdx, zIdx + ezIdx])
					semanticTerm += semanticBehavior * x[e, c, xIdx, yIdx, zIdx] / (size[0] * size[1] * size[2])
		semanticTerm /= len(self._elements)

		# Compatibility Term 
		self.computeEnvReachability()
		self.computeEnvVisibility()
		compatibilityTerm = 0.0
		for e, element in enumerate(self._elements):
			size = element["size"]
			for c, container in enumerate(self._voxels):
				for xIdx, yIdx, zIdx in itertools.product(range(validDims[e,c][0]), range(validDims[e,c][1]), range(validDims[e,c][2])):
					placementCompatibility = 0.0 
					for exIdx, eyIdx, ezIdx in itertools.product(range(size[0]), range(size[1]), range(size[2])):
						placementCompatibility += self.computeCompatibility(element, container[xIdx + exIdx, yIdx + eyIdx, zIdx + ezIdx])
					compatibilityTerm += placementCompatibility * x[e, c, xIdx, yIdx, zIdx] / (size[0] * size[1] * size[2])
		compatibilityTerm /= len(self._elements)

		# Utility Term 
		self.computeEnvUtility()
		utilityTerm = 0.0
		for e, element in enumerate(self._elements): 
			size = element["size"]
			for c, container in enumerate(self._voxels):
				for xIdx, yIdx, zIdx in itertools.product(range(validDims[e,c][0]), range(validDims[e,c][1]), range(validDims[e,c][2])):
					placementUtility = 0.0 
					for exIdx, eyIdx, ezIdx in itertools.product(range(size[0]), range(size[1]), range(size[2])):
						placementUtility += container[xIdx + exIdx, yIdx + eyIdx, zIdx + ezIdx]["utility"]
					placementUtility = placementUtility / (size[0] * size[1] * size[2])
					placementUtilityDiff = abs(placementUtility - element["utility"])
					utilityCompatibility = (math.exp(-placementUtilityDiff) - math.exp(-1.0)) / (1 - math.exp(-1.0))
					utilityMax = element["utility"] * placementUtility
					utilityTerm += ((self._params["w_utilityCompatibility"] * utilityCompatibility) + (0.6 * self._params["w_utilityMax"])) * x[e, c, xIdx, yIdx, zIdx]
		utilityTerm /= len(self._elements)

		# Spatial Constancy Term
		spatialConstancyTerm = 0.0
		for e, element in enumerate(self._elements):
			size = element["size"]
			for c, container in enumerate(self._voxels):
				for xIdx, yIdx, zIdx in itertools.product(range(validDims[e,c][0]), range(validDims[e,c][1]), range(validDims[e,c][2])):
					elementSpatialConstancy = 0.0 
					for exIdx, eyIdx, ezIdx in itertools.product(range(size[0]), range(size[1]), range(size[2])):
						elementSpatialConstancy += self.computeSpatialConstancy(element, container[xIdx + exIdx, yIdx + eyIdx, zIdx + ezIdx])
					spatialConstancyTerm += elementSpatialConstancy * x[e, c, xIdx, yIdx, zIdx] / (size[0] * size[1] * size[2])
		spatialConstancyTerm /= len(self._elements)

		# Structure Term 
		structureTerm = 0.0
		for c, container in enumerate(self._voxels):
			cDims = container.shape
			structureTerm += 0.5 * quicksum(hNeighbors[c, xIdx, yIdx, zIdx] for xIdx, yIdx, zIdx in itertools.product(range(cDims[0] - 1), range(cDims[1]), range(cDims[2])))
			structureTerm += 0.5 * quicksum(vNeighbors[c, xIdx, yIdx, zIdx] for xIdx, yIdx, zIdx in itertools.product(range(cDims[0]), range(cDims[1] - 1), range(cDims[2])))
		structureTerm /= totalElementVoxels
		
		# Occlusion Avoidance Term 
		occlusionTerm = 0.0 
		occlusionTerm += quicksum(occlusions[o] for o in range(len(occlusions)))
		occlusionTerm /= totalElementVoxels

		model.ModelSense = GRB.MAXIMIZE
		
		model.setObjectiveN(semanticTerm, index=0, weight=self._params["w_semantic"], priority=0)
		model.setObjectiveN(compatibilityTerm, index=1, weight=self._params["w_compatibility"], priority=0)
		model.setObjectiveN(utilityTerm, index=2, weight=self._params["w_utility"], priority=0)
		model.setObjectiveN(spatialConstancyTerm, index=3, weight=self._params["w_consistency"], priority=0)
		model.setObjectiveN(structureTerm, index=4, weight=self._params["w_structure"], priority=0)
		model.setObjectiveN(occlusionTerm, index=5, weight=-self._params["w_occlusion"], priority=0)

		model.setParam("TimeLimit", 60)
		model.update()

		model.optimize() 

		assignments = [] 

		for e, element in enumerate(self._elements):
			for c, container in enumerate(self._voxels):
				for xIdx, yIdx, zIdx in itertools.product(range(validDims[e,c][0]), range(validDims[e,c][1]), range(validDims[e,c][2])):
					if x[e, c, xIdx, yIdx, zIdx].X > 0:
						assignments.append({
							"element": element["id"], 
							"assigment": [c, xIdx, yIdx, zIdx]})

		model.dispose()

		return assignments