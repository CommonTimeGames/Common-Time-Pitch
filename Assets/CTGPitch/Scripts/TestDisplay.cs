using UnityEngine;
using System.Collections;

public class TestDisplay : MonoBehaviour {

	private CTGPitchTracker tracker;

	// Use this for initialization
	void Start () {
		tracker = GameObject.Find("CTGPitchTracker").GetComponent<CTGPitchTracker>();
	}
	
	// Update is called once per frame
	void Update () {
	
	}

	void OnGUI(){
		if(tracker.CurrentNote != null){
			GUI.Label(new Rect(10,10,100,100), "Current Pitch: " + tracker.CurrentNote.Pitch.ToString("F"));
			GUI.Label(new Rect(10,50,100,100), "Current Note: " + tracker.CurrentNote.Name);
			GUI.Label(new Rect(10,90,100,100), "Accuracy: " + tracker.CurrentNote.Accuracy.ToString("F") + " cents");
		}
	}
}
