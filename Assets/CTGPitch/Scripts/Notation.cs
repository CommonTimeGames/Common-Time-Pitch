using UnityEngine;
using System.Collections;

public class Notation {

	public static readonly string [] SHARP_NOTES = 
		{"C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B",
		 "C2", "C#2", "D2", "D#2", "E2", "F2", "F#2", "G2", "G#2", "A2", "A#2", "B2"};
	
	public static readonly string [] FLAT_NOTES =
		{"C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B"};
	
	public static readonly float [] FREQUENCIES =
		{16.35f, 17.32f, 18.35f, 19.45f, 20.60f, 21.83f, 23.12f, 24.50f, 25.96f, 27.50f, 29.14f, 30.87f};
	
	public static readonly int CHROMATIC_NOTE_COUNT = SHARP_NOTES.Length;
	
	public static readonly float CONCERT_A = 440.0f;
	public static readonly float HIGHEST_FREQUENCY = 4978.03f;
	public static readonly float LOWEST_FREQUENCY = 16.35f;
	public static readonly string NO_NOTE = "--";
	
	public static string GetNoteName(float frequency, float concertA, bool useSharpNotes){
		
		if(frequency < 16.35 || frequency > 4978.03f){
			return "--";
		}
		

		float numSteps = Mathf.RoundToInt((12 * (Mathf.Log(frequency/concertA) / Mathf.Log(2))));
		int index = (int) ((9 + numSteps) % 12);
		
		if(index < 0){
			index += 12;
		}
		
		return useSharpNotes ? SHARP_NOTES[index] : FLAT_NOTES[index];
	}
	
	public static string GetNoteName(float frequency){
		return GetNoteName(frequency, CONCERT_A, true);
	}
	
	public static string [] GenerateScale(string rootNote, bool useSharpNotes, params int [] noteDistances){
		string [] noteSet = useSharpNotes ? SHARP_NOTES : FLAT_NOTES;
		int noteIndex = getNoteIndex(rootNote, noteSet);
		string [] result = new string[noteDistances.Length + 1];
		
		result[0] = rootNote;
		
		for(int i = 1; i < result.Length; i++){
			result[i] = noteSet[(noteIndex + noteDistances[i-1]) % 24];
		}
		
		return result;
	}
	
	private static int getNoteIndex(string noteName, string [] noteSet){
		int noteIndex = -1;
		
		for(int i = 0; i < noteSet.Length; i++){
			if(string.Compare(noteName, noteSet[i], true) == 0){
				noteIndex = i;
			}
		}
		
		return noteIndex;
	}

	public static double GetNoteAccuracy(string targetNote, float detectedFrequency){
		int noteIndex = -1;
		
		if(targetNote == Notation.NO_NOTE){
			return 0;
		}
		
		if((noteIndex = getNoteIndex(targetNote, SHARP_NOTES)) < 0 
		   && (noteIndex = getNoteIndex(targetNote, FLAT_NOTES)) < 0){
			Debug.LogError("Note " + targetNote + " not defined!");
		}
		
		float currentFrequency;
		float closestFrequency;
		currentFrequency = closestFrequency = FREQUENCIES[noteIndex%12];
		
		while(currentFrequency <= HIGHEST_FREQUENCY){
			if(Mathf.Abs((detectedFrequency - currentFrequency)) < 
			   Mathf.Abs((detectedFrequency-closestFrequency))){
				closestFrequency = currentFrequency;
			}
			currentFrequency *= 2;
		}
		
		return 1200 * Mathf.Log10(detectedFrequency/closestFrequency) / Mathf.Log10(2);
	}
	
}
