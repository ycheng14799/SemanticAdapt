import enum
import socket 
import struct
import time 
import threading

FLOAT_SIZE = 4
INT_SIZE = 4

class SockFlags(enum.Enum): 
	HEADER = 0 
	CLOSE = 1
	SET_PARAMS = 2
	SET_ELEMENTS = 3
	SET_VOXELS = 4
	SET_OBJECTS = 5
	SET_OCCLUSIONS = 6
	SET_USER = 7
	SET_OBSTACLES = 8
	START_OPTIMIZATION = 9
	RESULTS = 10

class SocketConnection: 
	def __init__(self, ipaddr = '127.0.0.1', port=8080, callback=None):
		# initialise TCP Client/UDP Server to Unity
		self._ipaddr = ipaddr
		self._port = port
		self._sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
		self._sock.bind((self._ipaddr, self._port))
		self._sock.listen()
		self._connection = None
		self._message = bytes()
		self._recvThread = None 
		self._callback = callback

	@property
	def is_connected(self):
		# return True if server is connected and otherwise False
		try:
			pn = self._connection["socket"].getpeername()
			return True
		except:
			return False

	def receive(self): 
		while True:
			try: 
				data = self._connection["socket"].recv(1048576)
			except: 
				break 
			if not data:
				break 
			while len(data) > 0:
				header = SockFlags(data[0])
				if header != SockFlags.HEADER:
					break
				data = data[1:]
				messageLen = struct.unpack("i", data[:INT_SIZE])[0]
				data = data[INT_SIZE:]
				message = data[:messageLen]
				if self._callback:
				   self._callback(message) 
				data = data[messageLen:]
			time.sleep(0.1)
		print("Exiting receive thread")

	def connect(self):
		# start connection
		sock, addr = self._sock.accept()
		self._connection = {
			"addr": str(addr[0]),
			"port": str(addr[1]),
			"socket": sock 
		}
		print("Connection:", addr[0], ":", addr[1])
		self._recvThread = threading.Thread(target=self.receive)
		self._recvThread.start()

	def disconnect(self):
		# gracefully disconnect to server
		if self.is_connected:
			self._message = bytes([SockFlags.CLOSE.value])
			self.send_bytes(self._message)
			self._connection["socket"].close()

	def send_bytes(self, b):
		if self.is_connected:
			self._connection["socket"].sendall(bytes([SockFlags.HEADER.value]) + struct.pack("i", len(b)) + b)