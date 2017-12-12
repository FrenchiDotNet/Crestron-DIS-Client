#
#        File | DIS_Test_Server.py
#      Author |	Ryan French
# Description |	...
#

import socket
import sys
import datetime

# Get computer info from prompt
server_ip   = input("IP Address: ")
server_port = int(input("Port: "));

# Set up delegate list & status
delegateSeatOccupied = [
	True,
	True,
	True,
	True,
	True, # 5
	True,
	True,
	True,
	True,
	True, # 10
	True,
	True,
	True,
	True,
	True, # 15
	True,
	True,
	True,
	True,
	True, # 20
	True,
	True,
	True,
	True
]

delegateName = [
	"Fred",
	"Bob",
	"George",
	"Sue",
	"Hank", # 5
	"Tom",
	"Samantha",
	"Amanda",
	"Timothy",
	"Aziz", # 10
	"Michael",
	"Maddison",
	"Barbara",
	"Henry",
	"Nigel", # 15
	"Langdon",
	"Ryan",
	"Joe",
	"Randy",
	"Mei", # 20
	"Nadia",
	"Hailey",
	"David",
	"Ashley"
]

serverOnline            = True
serverNameDataAvailable = True
serverNameDataValid     = True
serverCpuTempError      = False
serverStorageMediaError = False
serverVoltageError      = False

def GetStatusBits():
	byte = 0

	if(serverOnline):
		byte |= (1 << 0)
	if(serverNameDataAvailable):
		byte |= (1 << 1)
	if(serverNameDataValid):
		byte |= (1 << 2)
	if(serverCpuTempError):
		byte |= (1 << 3)
	if(serverStorageMediaError):
		byte |= (1 << 4)
	if(serverVoltageError):
		byte |= (1 << 5)

	return byte

def GetOccupiedList():
	byte1 = 0
	byte2 = 0
	byte3 = 0

	for i in range(8):
		if(delegateSeatOccupied[i]):
			byte1 |= (1 << i)

	for i in range(8):
		if(delegateSeatOccupied[i+8]):
			byte2 |= (1 << i)

	for i in range(8):
		if(delegateSeatOccupied[i+16]):
			byte3 |= (1 << i)

	return [byte1, byte2, byte3]


# Create a TCP/IP socket
sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)

server_address = (server_ip, server_port)
print('starting up on {} port {}'.format(*server_address))
sock.bind(server_address)

sock.listen(1)

rxbuf = ""

while True:
	# Wait for a connection
	print('waiting for a connection')
	connection, client_address = sock.accept()
	try:
		print('connection from', client_address)
		
		# Receive the data
		while True:

			data = connection.recv(2048)
			for i in data:
				rxbuf += chr(i)		
			
			if("\x0D" in rxbuf):

				# Extract commands from received bytes and leave the rest
				l = rxbuf.rfind("\x0D")
				p = rxbuf[:l]
				if(l+1 < len(rxbuf)):
					rxbuf = rxbuf[l+1:]
				else:
					rxbuf = ""

				cmds = p.split('\x0D')

				for strin in cmds:
				
					if strin == "":
						continue
					
					print('[%s] Received command: %s' % (str(datetime.datetime.now()), strin))
					
					if strin == 'get status':

						ret = b"report status%c\xFE" % GetStatusBits()
						connection.sendall(ret)
						print(ret)

					elif strin == "get activedelegates":

						dat = GetOccupiedList()
						ret = b"report activedelegates%c%c%c\xFE" % (dat[0], dat[1], dat[2])
						connection.sendall(ret)
						print(ret)

					elif "get delegatename" in strin:

						targ = ord(strin[16]) - 65
						if(delegateSeatOccupied[targ]):
							ret = "report delegatename%c%s" % (strin[16], delegateName[targ])
							ret = ret.encode()
							ret += b'\xFE'
						else:
							ret = b"error delegatename\xFE"
						connection.sendall(ret)
						print(ret)			

	finally:
		# Clean up the connection
		connection.close()