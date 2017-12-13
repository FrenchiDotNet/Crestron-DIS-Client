/*
**        File | DIS_Client.cs 
**      Author | Ryan French
** Description | ...
*/

using System;
using System.Text;
using System.Collections.Generic;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronSockets;

namespace DIS_Client {

    //-------------------------------------//
    //       Class | Core
    // Description | ...
    //-------------------------------------//

    public static class Core {

    	//===================// Members //===================//

    		// IP Connection //

    	internal static string serverIp;
    	internal static int    serverPort;
    	internal static bool   okToConnect;
    	internal static bool   waitForTx;
    	internal static bool   _serverConnected;

        internal static string rxBuf;

    	internal static string txDelimiter = "\x0D";

    	internal static CTimer reconnectTimer;

    	internal static TCPClient client;
    	internal static List<string> MessageQueue;

    		// DIS Server Status //

    	internal static bool serverOnline;
    	internal static bool serverNameDataAvailable;
    	internal static bool serverNameDataValid;
    	internal static bool serverCpuTempError;
    	internal static bool serverStorageError;
    	internal static bool serverPowerError;

    	internal static bool queryInProgress;

    		// Delegate Information

    	public static bool[]   delegateSeatOccupied;
    	public static string[] delegateNames;

    		// S+ Delegates //

    	public static DelegateUshort ConnectionStatusEvent { get; set; }
    	public static DelegateString ServerStatusError { get; set; }

    	//===================// Constructor //===================//

    	static Core() {

    		MessageQueue = new List<string>();

    		delegateSeatOccupied = new bool[24];
    		delegateNames        = new string[24];
            rxBuf                = "";

    	}

    	//===================// Methods //===================//

    	//-------------------------------------//
        //    Function | TCPClientSettings
        // Description | Called by S+ symbol to pass TCP client settings from SIMPL program, then 
        //               attempts to connect.
        //-------------------------------------//

        public static void TCPClientSettings(string _ip, ushort _prt) {

            serverIp    = _ip;
            serverPort  = _prt;

            client = new TCPClient(serverIp, serverPort, 10240);
            client.SocketStatusChange += new TCPClientSocketStatusChangeEventHandler(clientSocketChange);

        }

        //-------------------------------------//
        //    Function | EnableConnect
        // Description | ...
        //-------------------------------------//

        public static void EnableConnect() {

            okToConnect = true;
            ServerConnect();

        }

        //-------------------------------------//
        //    Function | DisableConnect
        // Description | ...
        //-------------------------------------//
        public static void DisableConnect() {

            okToConnect = false;
            ServerDisconnect();
        }

        //-------------------------------------//
        //    Function | ServerConnect
        // Description | Attempts to connect to server.
        //-------------------------------------//

        internal static void ServerConnect () {

            try {

                client.ConnectToServerAsync(clientConnect);
                reconnectTimer = new CTimer(reconnectTimerHandler, 15000);

            } catch (Exception _er) {

                ErrorLog.Error("[ERROR] Error connecting to DIS at address {0}: {1}", serverIp, _er);

            }

        }

        //-------------------------------------//
        //    Function | ServerDisconnect
        // Description | Disconnects from the server.
        //-------------------------------------//

        internal static void ServerDisconnect() {

            try {

                client.DisconnectFromServer();
                reconnectTimer.Stop();

            } catch (Exception _er) {

                ErrorLog.Error("[ERROR] Error disconnecting from DIS at address {0}: {1}", serverIp, _er);

            }

        }

        //-------------------------------------//
        //    Function | QueueCommand
        // Description | Formats outgoing commands before checking if a current send action is 
        //               in progress by the TCP client. If so, it appends the command to the 
        //               message queue, otherwise it sends it immediately.
        //-------------------------------------//

        public static void QueueCommand(string _cmd) {

            // Ignore new commands if controller is disconnected or command is blank
            if (client.ClientStatus != SocketStatus.SOCKET_STATUS_CONNECTED || _cmd == "")
                return;

            string command = _cmd + txDelimiter;

            if (waitForTx) {
                MessageQueue.Add(command);
            } else {
                waitForTx = true;
                client.SendDataAsync(Encoding.ASCII.GetBytes(command), command.Length, clientDataTX);
            }

        }

        //-------------------------------------//
        //    Function | RequestDelegateInfo
        // Description | Called by S+, begins delegate query process by checking the server for errors.
        //				 Further steps are automatically initiated by favorable server feedback.
        //-------------------------------------//

        public static void RequestDelegateInfo() {

            if(serverConnected && !queryInProgress) {

                queryInProgress = true;
                QueueCommand("get status");

            }

        }

    	//-------------------------------------//
        //    Function | ParseFeedback
        // Description | Receives data from RX event handler and extracts data points per API specification.
        //-------------------------------------//

    	internal static void ParseFeedback(string _data) {

    		string[] lines = _data.Split('\xFE');

    		for (int i = 0; i < lines.Length; i++) {

    			if(lines[i] == "" || lines[i].IndexOf("report") < 0)
    				continue;

                if(lines[i].IndexOf("status") >= 0  && lines[i].Length >= 14) {

    				char dat = lines[i][13];

    				if(CheckStatus(dat))
    					QueueCommand("get activedelegates");
    				else
    					queryInProgress = false;

    			} else if(lines[i].IndexOf("activedelegates") >= 0 && lines[i].Length >= 25) {

    				char dat1 = lines[i][22];
    				char dat2 = lines[i][23];
    				char dat3 = lines[i][24];

    				CheckActiveDelegates(dat1, dat2, dat3);
    				queryInProgress = false;

    			} else if(lines[i].IndexOf("delegatename") >= 0 && lines[i].Length >= 21) {

    				int dat1    = Convert.ToInt32(lines[i][19])-65;
    				string dat2 = lines[i].Substring(20);

    				delegateNames[dat1] = dat2;

    			}

    		}

    	}

        //-------------------------------------//
        //    Function | CheckSeatsByte
        // Description | Gets state of each bit in passed character and returns
        //				 a bool array.
        //-------------------------------------//

    	internal static bool[] CheckSeatsByte(char _c) {

    		bool[] seats = new bool[8];
    		byte b       = Convert.ToByte(_c);

    		for(int i = 0; i < 8; i++)
    			seats[i] = (b & (1 << i)) != 0 ? true : false;

    		return seats;

    	}

        //-------------------------------------//
        //    Function | CheckActiveDelegates
        // Description | ...
        //-------------------------------------//

	    internal static void CheckActiveDelegates(char _seats1_8, char _seats9_16, char _seats17_24) {

	    	// Compile list of occupied seats
	    	for(int i = 0; i < 8; i++)
	    		delegateSeatOccupied[i] = CheckSeatsByte(_seats1_8)[i];
	    	for(int i = 0; i < 8; i++)
	    		delegateSeatOccupied[8+i] = CheckSeatsByte(_seats9_16)[i];
	    	for(int i = 0; i < 8; i++)
	    		delegateSeatOccupied[16+i] = CheckSeatsByte(_seats17_24)[i];

	    	// Get Delegate names for occupied seats
	    	for(int i = 0; i < 24; i++)
	    		if(delegateSeatOccupied[i])
	    			QueueCommand("get delegatename" + (char)(i+65));

	    }

        //-------------------------------------//
        //    Function | CheckStatus
        // Description | Gets state of each bit and checks for error conditions.
        //-------------------------------------//

    	internal static bool CheckStatus(char _c) {

    		byte b = Convert.ToByte(_c);

    		serverOnline            = (b & (1 << 0)) != 0 ? true : false;
    		serverNameDataAvailable = (b & (1 << 1)) != 0 ? true : false;
    		serverNameDataValid     = (b & (1 << 2)) != 0 ? true : false;
    		serverCpuTempError      = (b & (1 << 3)) != 0 ? true : false;
    		serverStorageError      = (b & (1 << 4)) != 0 ? true : false;
    		serverPowerError        = (b & (1 << 5)) != 0 ? true : false;

    		if(serverOnline &&
    		   serverNameDataAvailable &&
    		   serverNameDataValid &&
    		   !serverCpuTempError && 
    		   !serverStorageError &&
    		   !serverPowerError) {

    			return true;

    		} else  {

    			GenerateErrors();
    			return false;

    		}

    	}

    	//-------------------------------------//
        //    Function | GenerateErrors
        // Description | ...
        //-------------------------------------//

	    internal static void GenerateErrors() {

	    	string errs   = "";

	    	if(!serverOnline) {
	    		errs += "Server Is Offline<br/>";
	    	}
	    	if(!serverNameDataAvailable) {
	    		errs += "Name Data Is Not Available<br/>";
	    	}
	    	if(!serverNameDataValid) {
	    		errs += "Name Data Is Not Valid<br/>";
	    	}
	    	if(serverCpuTempError) {
	    		errs += "CPU Overtemp Alert<br/>";
	    	}
	    	if(serverStorageError) {
	    		errs += "Storage Drive Failure<br/>";
	    	}
	    	if(serverPowerError) {
	    		errs += "Power Supply Voltage Alert<br/>";
	    	}

	    	ServerStatusError(errs);

	    }

    	//===================// Event Handlers //===================//

        //-------------------------------------//
        //    Function | clientSocketChange
        // Description | Event handler for TCP client socket status. If socket disconnects, function 
        //               attempts to reconnect and starts timer to re-attempt connection every 15s.
        //               Also sends connection status (H/L) to S+.
        //-------------------------------------//

        internal static void clientSocketChange(TCPClient _cli, SocketStatus _status) {

            if (_status != SocketStatus.SOCKET_STATUS_CONNECTED) {

                serverConnected = false;
                queryInProgress = false;

                if(okToConnect)
                    ServerConnect();

            } else if (_status == SocketStatus.SOCKET_STATUS_CONNECTED) {

                serverConnected = true;

            }

        }

        //-------------------------------------//
        //    Function | clientConnect
        // Description | Handler for TCP client connect event. Begins listening for incoming 
        //               data from server, then notifies RTS class so that information can
        //               be requested from server if a meeting is in progress.
        //-------------------------------------//

        internal static void clientConnect (TCPClient _cli) {

            client.ReceiveDataAsync(clientDataRX);
            RTS.ServerConnectedCallback();

        }

        //-------------------------------------//
        //    Function | clientDataRX
        // Description | Called asynchronously by TCP client on receive. Decodes incoming byte stream
        //				 & performs basic data validation.
        //-------------------------------------//

        internal static void clientDataRX(TCPClient _cli, int _bytes) {

            byte[] bytes  = _cli.IncomingDataBuffer;
            string data = "";

            for(int i = 0; i < _bytes; i++) {
                data += (char)bytes[i];
            }

            if (data.IndexOf("report") >= 0 && data != "")
                ParseFeedback(data);

            client.ReceiveDataAsync(clientDataRX);

        }

        //-------------------------------------//
        //    Function | clientDataTX
        // Description | Called asynchronously by TCP client on send. Sends the next
        //               message in MessageQueue if available.
        //-------------------------------------//

        internal static void clientDataTX(TCPClient _cli, int _bytes) {

            if (MessageQueue.Count > 0) {
                if(MessageQueue[0] != "")
                    client.SendDataAsync(Encoding.ASCII.GetBytes(MessageQueue[0]), MessageQueue[0].Length, clientDataTX);
                MessageQueue.RemoveAt(0);
            } else {
                waitForTx = false;
            }

        }

        //-------------------------------------//
        //    Function | reconnectTimerHandler
        // Description | If TCP client hasn't connected yet, try again and reset timer 
        //               for next attempt.
        //-------------------------------------//

        internal static void reconnectTimerHandler(object o) {

            if (okToConnect && client.ClientStatus != SocketStatus.SOCKET_STATUS_CONNECTED) {
                ServerConnect();
            }

        }

        //===================// Get / Set //===================//

        //-------------------------------------//
        //    Property | serverConnected
        // Description | Stores TCP client connection status in bool and triggers
        //				 ConnectionStatusEvent delegate for S+ update
        //-------------------------------------//

        public static bool serverConnected {

        	get { 
        		return _serverConnected; 
        	}

        	internal set {
        		_serverConnected = value;
        		ConnectionStatusEvent((ushort)(value == true ? 1 : 0));
        	}

        }

    } // End DIS_Client class

    public delegate void DelegateUshort (ushort value);
    public delegate void DelegateString (SimplSharpString str);
    public delegate void DelegateUshortString (ushort value1, ushort value2, SimplSharpString str);

}
