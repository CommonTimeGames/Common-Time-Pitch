using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

public class CTGPitchTracker : MonoBehaviour {

	public Note CurrentNote { get; set; }

	public delegate void NoteDelegate(string noteName);
	public event NoteDelegate NoteDetected;

	private static CTGPitchTracker _instance;

	private static readonly int DB_THRESHOLD = -40;
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

	private double lastPitch;
	private string lastNoteCalculated;
	private string currrentDetectedNote;
	private float noteDeltaTime;

	void Awake(){
		/* Check if any instances of the
		 * pitch tracker have been initialized;
		 * quit if this is the case */
		
		if(_instance == null){
			_instance = this;
		}
		else{
			Debug.LogError("Only one instance of CTGPitchTracker may be active in the scene!");
			enabled = false;
			return;
		}
	}

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

		lastNoteCalculated = currrentDetectedNote = "";
		noteDeltaTime = 0;
		lastPitch = 0;
	}

	public double GetCurrentPitch(){
		/* On iOS/Android, just return
		 * the pitch calculated on the
		 * native audio thread. */
		if(usingNativePlugin()){
			return CTGGetCurrentPitch();
		}

		currentPosition = Microphone.GetPosition("");

		/* No new samples were available... */
		if(lastPosition == currentPosition){
			return lastPitch;
		}

		var samplesRead = (currentPosition-lastPosition);

		/* Sample position could wrap around
		 * to the beginning. */
		if(samplesRead < 0){
			samplesRead += SAMPLE_RATE;
		}

		buffer.GetData(sampleBuffer, lastPosition);
		Array.Copy(sampleBuffer, doubleSampleBuffer, Math.Min(samplesRead, sampleBuffer.Length));

		//Debug.Log ("Last mic pos.: " + lastPosition + ", Current mic pos.: " + currentPosition + ". Samples read: " + samplesRead);
		lastPosition = currentPosition;

		/* Calculate signal level in dB
		 * to determine if there is a 
		 * sound to be processed in the
		 * first place. */
		var sum = 0.0;
		for(int i = 0; i < samplesRead && i < sampleBuffer.Length; i++){
			sum += sampleBuffer[i] * sampleBuffer[i];
		}

		var rms = Math.Sqrt(sum/samplesRead);
		var decibel = 20 * Math.Log10(rms);

		//Debug.Log ("RMS val: " + rms + ", dB val: " + decibel);

		if(decibel < DB_THRESHOLD){
			lastPitch = 0;
		} else{
			lastPitch = tracker.computePitch(doubleSampleBuffer, 0, Math.Min(samplesRead, doubleSampleBuffer.Length));
		}

		return lastPitch;
	}
	
	// Update is called once per frame
	void Update () {

		var currentPitch = GetCurrentPitch();
		var currentAccuracy = Notation.GetNoteAccuracy((float)currentPitch);
		var note = Notation.GetNoteName((float)currentPitch);

		CurrentNote = CurrentNote ?? new Note ();
		CurrentNote.Pitch = currentPitch;
		CurrentNote.Name = note;
		CurrentNote.Accuracy = currentAccuracy;


		currrentDetectedNote = currentPitch > 0 ? currrentDetectedNote : "";

		if(currentPitch > 0 && lastNoteCalculated == note){
			noteDeltaTime += Time.deltaTime;
			
			if(noteDeltaTime >= TIME_THRESHOLD 
			   && currrentDetectedNote != note
			   && NoteDetected != null){
				NoteDetected(note);
				currrentDetectedNote = note;
			}
		}
		else{
			noteDeltaTime = 0;
			lastNoteCalculated = note;
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

	private float getSmoothAverage(float [] vals){
		float minVal = float.MaxValue;
		float maxVal = 0;
		float sum = 0;

		foreach(var val in vals){
			minVal = Mathf.Min(val, minVal);
			maxVal = Mathf.Max(val, maxVal);
			sum += val;
		}

		return (sum - minVal - maxVal) / (vals.Length - 2);
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

	public static CTGPitchTracker Tracker
	{
		get
		{
			return _instance;
		}
	}
}
