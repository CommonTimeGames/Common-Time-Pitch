using UnityEngine;
using System.Collections;
using System.Linq;

public class TestDisplay : MonoBehaviour {

	private CTGPitchTracker tracker;

	public GUIStyle NoteLabelStyle;
	public GUIStyle LabelStyle;

	private static readonly float DETECTION_TIMEOUT = 2f;

	private static readonly int REALTIME_DETECTION_MODE = 0;
	private static readonly int EVENT_DETECTION_MODE = 1;

	private int currentMode = REALTIME_DETECTION_MODE;
	private string lastDetectedNote = "--";
	private float lastNoteTimer = 0f;
	private string [] lastNotes = new string[5];
	private int currentIndex = 0;

	private string noteText = "";
	private string frequencyText = "";
	private string accuracyText = "";

	// Use this for initialization
	void Start () {
		tracker = GameObject.Find("CTGPitchTracker").GetComponent<CTGPitchTracker>();
		tracker.NoteDetected += onNoteDetected;
	}
	
	// Update is called once per frame
	void Update () {
		/* Here, we update the display
		 * only every 5 frames so that
		 * the text is a bit easier to read.
		 * You can still query tracker.CurrentNote
		 * as often as you wish. */
		if(currentMode == REALTIME_DETECTION_MODE){
			if(Time.frameCount % 5 == 0){
				noteText = tracker.CurrentNote.Name;
				frequencyText = tracker.CurrentNote.Pitch.ToString("F");
				accuracyText = tracker.CurrentNote.Accuracy.ToString("F");
			}
		}
		else if(currentMode == EVENT_DETECTION_MODE){
			lastNoteTimer += Time.deltaTime;

			if(lastNoteTimer >= DETECTION_TIMEOUT){
				lastDetectedNote = "--";
			}
		}

	}

	void OnGUI(){
		if(GUI.Button(new Rect(Screen.width/2 - 110, 10, 100, 50), "Realtime Mode")){
			currentMode = REALTIME_DETECTION_MODE;

		}
		if(GUI.Button(new Rect(Screen.width/2 , 10, 100, 50), "Event Mode")){
			currentMode = EVENT_DETECTION_MODE;
		}

		if(currentMode == REALTIME_DETECTION_MODE){
			GUI.Label(new Rect(Screen.width/2 - 50, Screen.height/2 - 50, 50, 50), 
			          noteText, NoteLabelStyle);

			GUI.Label(new Rect(Screen.width/2 - 150, Screen.height/2 + 50, 100, 100), 
			          "Frequency: " 
			          	+ frequencyText
			          	+ " Hz", LabelStyle);

			GUI.Label(new Rect(Screen.width/2 - 150, Screen.height/2 + 100, 100, 100), 
			          "Accuracy: " 
			          + accuracyText
			          + " cents", LabelStyle);
		}
		else{
			GUI.Label(new Rect(Screen.width/2 - 50, Screen.height/2 - 50, 50, 50), 
			          lastDetectedNote, NoteLabelStyle);

			GUI.Label(new Rect(Screen.width/2 - 100, Screen.height/2 + 50, 100, 100),
			          "Last 5 Notes: ", LabelStyle);

			GUI.Label(new Rect(Screen.width/2-100, Screen.height/2 + 100, 100, 100),
			          string.Join(", ", lastNotes), LabelStyle);
		
		}

	}

	void onNoteDetected(string noteName){
		if(currentMode == EVENT_DETECTION_MODE){
			Debug.Log ("Note detected: " + noteName);
			lastNotes[currentIndex] = noteName;
			currentIndex = ++currentIndex % lastNotes.Length;
			lastDetectedNote = noteName;
			lastNoteTimer = 0;
		}
	}
}
