/*
**        File | RTS.cs 
**      Author | Ryan French
** Description | ...
*/

using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using Crestron.SimplSharp;

namespace DIS_Client {

	//-------------------------------------//
    //       Class | RTS
    // Description | ...
    //-------------------------------------//

    public static class RTS {

    	//===================// Members //===================//

    	internal static bool waitingForDisData;
    	internal static bool _meetingInProgress;

    		// Queue

    	internal static int activeSpeaker;

    	internal static List<int> requestQueue;

    		// S+ Delegates

    	public static RTSQueueDelegate delegateListUpdate { get; set; }
    	public static RTSButtonState buttonStateUpdate { get; set; }
    	public static DelegateString currentSpeakerUpdate { get; set; }
    	public static DelegateString nextSpeakerUpdate { get; set; }
    	public static DelegateUshort meetingInProgressUpdate { get; set; }

    	//===================// Constructor //===================//

    	static RTS() {

    		activeSpeaker = 0;
    		requestQueue  = new List<int>();

    	}

    	//===================// Methods //===================//

    	//-------------------------------------//
        //    Function | BeginMeeting
        // Description | ...
        //-------------------------------------//

    	public static void BeginMeeting() {

    		meetingInProgress = true;
    		waitingForDisData = true;
    		activeSpeaker = 0;
    		requestQueue  = new List<int>();
    		UpdateOutput();
    		Core.EnableConnect();

    	}

    	//-------------------------------------//
        //    Function | EndMeeting
        // Description | ...
        //-------------------------------------//

    	public static void EndMeeting() {

    		Core.DisableConnect();
    		activeSpeaker = 0;
    		requestQueue  = new List<int>();
    		UpdateOutput();
    		meetingInProgress = false;
    		waitingForDisData = false;

    	}

    	//-------------------------------------//
        //    Function | ServerConnectedCallback
        // Description | ...
        //-------------------------------------//

    	public static void ServerConnectedCallback() {

    		if(waitingForDisData) {
    			Core.RequestDelegateInfo();
    			waitingForDisData = false;
    		}

    	}

    	//-------------------------------------//
        //    Function | RetryInfoRequest
        // Description | If errors reported from server, allows moderator to
        //				 retry information request from connected server.
        //-------------------------------------//

    	public static void RetryInfoRequest() {

    		if(Core.serverConnected)
    			Core.RequestDelegateInfo();

    	}

    	//-------------------------------------//
        //    Function | RequestToSpeak
        // Description | ...
        //-------------------------------------//

    	public static void RequestToSpeak(ushort _num) {

    		if(!meetingInProgress)
    			return;

    		int num = _num; // Cast as int

    		if(requestQueue.Contains(num)) {

    			requestQueue.Remove(num);
    		
    		} else {

    			requestQueue.Add(num);
    		}

    		UpdateOutput();
    		
    	}

    	//-------------------------------------//
        //    Function | NextSpeaker
        // Description | ...
        //-------------------------------------//

    	public static void NextSpeaker() {

    		if(!meetingInProgress)
    			return;

    		int l = requestQueue.Count;

    		if(l == 0 && activeSpeaker == 0) {

    			return;

    		} else if(l == 0 && activeSpeaker != 0) {

    			currentSpeakerUpdate("");
    			activeSpeaker = 0;

    		} else if(l > 0) {

    			activeSpeaker = requestQueue[0];
    			string del;

    			requestQueue.RemoveAt(0);

    			if(Core.delegateSeatOccupied[activeSpeaker-1])
    				del = Core.delegateNames[activeSpeaker-1];
    			else
    				del = "[Unregistered]";

    			currentSpeakerUpdate(del);

    		}

    		UpdateOutput();

    	}

    	//-------------------------------------//
        //    Function | UpdateOutput
        // Description | ...
        //-------------------------------------//

    	internal static void UpdateOutput() {

    		int l = requestQueue.Count;
    		string del;

    		// Send list of queued names
    		for(int i = 0; i < l; i++) {

    			if(Core.delegateSeatOccupied[requestQueue[i]-1])
    				del = Core.delegateNames[requestQueue[i]-1];
    			else
    				del = "[Unregistered]";

    			delegateListUpdate((ushort)(i+1), 1, (ushort)requestQueue[i], del);

    		}

    		// Clear out empty positions
    		for(int i = l; i < 24; i++) {

    			delegateListUpdate((ushort)(i+1), 0, 0, "");

    		}

    		// Update Next Delegate output
    		if(l > 0) {
    			if(Core.delegateSeatOccupied[requestQueue[0]-1])
    				del = Core.delegateNames[requestQueue[0]-1];
    			else
    				del = "[Unregistered]";
    			nextSpeakerUpdate(del);
    		} else
    			nextSpeakerUpdate("");

    		// Update Button Feedback
    		for(int i = 1; i <= 24; i++) {

    			if(!requestQueue.Contains(i) && activeSpeaker != i) {
    				buttonStateUpdate((ushort)i, 0);
    			} else if(requestQueue.Contains(i)) {
    				buttonStateUpdate((ushort)i, 1);
    			} else if (activeSpeaker == i) {
    				buttonStateUpdate((ushort)i, 2);
    			}

    		}

    	}

    	//===================// Event Handlers //===================//

    	//===================// Get / Set //===================//

    	internal static bool meetingInProgress {

    		get {
    			return _meetingInProgress;
    		}

    		set {
    			_meetingInProgress = value;
    			meetingInProgressUpdate((ushort)(value ? 1 : 0));
    		}

    	}

    }

    public delegate void RTSQueueDelegate (ushort position, ushort enabled, ushort seatNumber, SimplSharpString name);
    public delegate void RTSButtonState (ushort seatNumber, ushort state);

}