using System;
using UnityEngine;
using System.Collections;
using System.Runtime.InteropServices;
using System.Threading;

public class CTGPitchTracker : MonoBehaviour {

	public Note CurrentNote { get; set; }

	public delegate void NoteDelegate(string noteName);
	public static event NoteDelegate NoteDetected;

	private static readonly int MAX_BUFFER_SIZE = 4096;
	private static readonly int SAMPLE_RATE = 44100;
	private static readonly double TIME_THRESHOLD = 0.25;

	private bool hasMicrophone;

	/* For native functions only */
	private Thread nativeThread;

	/* For Unity Audio processing */
	private AudioClip buffer;
	private float [] sampleBuffer;
	private double [] doubleSampleBuffer;
	private DyWavePitchTracker tracker;
	private int lastPosition;
	private int currentPosition;

	private string lastNote;
	private float noteDeltaTime;

	void Start () {

		/* Check to see if the device has any microphones */

		hasMicrophone = (Microphone.devices != null && Microphone.devices.Length > 0);

		if(!hasMicrophone){

			/* Stop initialization if 
			 * no microphones
			 * were found. */
			Debug.LogWarning("This device does not appear to have a microphone. Disabling script!");
			enabled = false;
			return;
		}
		if(usingNativePlugin()){
			/* Initialize native code
			 * if on Android/iOS device */
			Debug.Log("Using native device audio.");
			CTGPitchTrackerInit();
			startNativeThread();
		}
		else{
			/* On all other platforms, use Unity Audio.
			 * This should only be done for testing
			 * purposes, as latency is usually
			 * much higher. */

			sampleBuffer = new float[MAX_BUFFER_SIZE];
			doubleSampleBuffer = new double[MAX_BUFFER_SIZE];		
			lastPosition = currentPosition = 0;
			tracker = new DyWavePitchTracker();

			Debug.Log("Using Unity Audio; latency may be higher.");
			buffer = Microphone.Start("", true, 1, SAMPLE_RATE);
		}

		lastNote = "";
		noteDeltaTime = 0;
	}

	public double GetCurrentPitch(){
		if(usingNativePlugin()){
			return CTGGetCurrentPitch();
		}

		currentPosition = Microphone.GetPosition("");
			
		buffer.GetData(sampleBuffer, lastPosition);
		Array.Copy(sampleBuffer, doubleSampleBuffer, sampleBuffer.Length);
		var samplesRead = (currentPosition-lastPosition);
			
		if(samplesRead < 0){
			samplesRead += SAMPLE_RATE;
		}
			
		lastPosition = currentPosition;	
			
			//Debug.Log ("Last mic pos.: " + lastPosition + ", Current mic pos.: " + currentPosition + ". Samples read: " + samplesRead);
		return tracker.computePitch(doubleSampleBuffer, lastPosition, samplesRead);
	}
	
	// Update is called once per frame
	void Update () {

		var currentPitch = GetCurrentPitch();
		var note = Notation.GetNoteName((float)currentPitch);
		var accuracy = Notation.GetNoteAccuracy(note, (float)currentPitch);

		CurrentNote = CurrentNote ?? new Note ();
		CurrentNote.Pitch = currentPitch;
		CurrentNote.Name = note;
		CurrentNote.Accuracy = accuracy;

		if(currentPitch > 0 && lastNote == note){
			
			noteDeltaTime += Time.deltaTime;
			
			if(noteDeltaTime >= TIME_THRESHOLD && NoteDetected != null){
				NoteDetected(note);
			}
		}
		else{
			noteDeltaTime = 0;
			lastNote = note;
		}
	}

	/* Stop processing sound when the
	 * application goes into the background
	 * (phone call, etc). */
	void OnApplicationPause(bool pauseStatus){
		Debug.Log("OnApplicationPause: " + pauseStatus);
		
		if(pauseStatus){
			if(Application.platform 
			   == RuntimePlatform.IPhonePlayer){
				CTGPitchTrackerDestroy();
			}
			else{
				stopNativeThread();
			}	
		}
		else{
			if(Application.platform == RuntimePlatform.IPhonePlayer){
				CTGPitchTrackerInit();
				CTGPitchTrackerStart();
			}
			else{
				startNativeThread();
			}
		}
	}
	
	void OnApplicationQuit(){
		Debug.Log ("OnApplicationQuit()");
		if(usingNativePlugin()){
			CTGPitchTrackerDestroy();
		}
	}
	
	void OnDestroy(){
		Debug.Log ("OnDestroy()");
		stopNativeThread();
	}

	private bool usingNativePlugin(){
		return Application.platform == RuntimePlatform.IPhonePlayer
			|| Application.platform == RuntimePlatform.Android;
	}

	private void startNativeThread(){
		if(!usingNativePlugin()) { return; }
		
		Debug.Log ("startNativeThread()");
		stopNativeThread();	
		nativeThread = new Thread(CTGPitchTrackerStart);
		nativeThread.Start();		
	}
	
	private void stopNativeThread(){
		if(!usingNativePlugin()) { return; }
		
		Debug.Log ("stopNativeThread()");
		
		if(nativeThread != null){
			CTGPitchTrackerStop();
			nativeThread.Join();
			nativeThread = null;
		}
	}

	/* Native methods. These should only be
	 * called when running on an actual
	 * Android/iOS device. */

	#if UNITY_IPHONE
	[DllImport ("__Internal")]
	private static extern void CTGPitchTrackerInit();
	
	[DllImport ("__Internal")]
	private static extern void CTGPitchTrackerStart();
	
	[DllImport ("__Internal")]
	private static extern void CTGPitchTrackerStop();
	
	[DllImport ("__Internal")]
	private static extern void CTGPitchTrackerDestroy();
	
	[DllImport ("__Internal")]
	private static extern double CTGGetCurrentPitch();

	#elif UNITY_ANDROID

	[DllImport ("CTGPitchTracker")]
	private static extern void CTGPitchTrackerInit();
	
	[DllImport ("CTGPitchTracker")]
	private static extern void CTGPitchTrackerStart();
	
	[DllImport ("CTGPitchTracker")]
	private static extern void CTGPitchTrackerStop();
	
	[DllImport ("CTGPitchTracker")]
	private static extern void CTGPitchTrackerDestroy();
	
	[DllImport ("CTGPitchTracker")]
	private static extern double CTGGetCurrentPitch();

	#else

	private static void CTGPitchTrackerInit(){ Debug.Log("CTGPitchTrackerInit"); }
	
	private static void CTGPitchTrackerStart(){ Debug.Log("CTGPitchTrackerStart"); }
	
	private static void CTGPitchTrackerStop(){ Debug.Log("CTGPitchTrackerStop"); }
	
	private static void CTGPitchTrackerDestroy() { Debug.Log("CTGPitchTrackerDestroy"); }
	
	private static double CTGGetCurrentPitch() { Debug.Log("CTGPitchTrackerGetCurrentPitch"); return 0;}

	#endif
}
