using FMODUnity;
using UnityEngine;
using System.Collections;

public class RecordMic : MonoBehaviour
{
    //public variables
    [Header("Choose A Microphone")]
    public int RecordingDeviceIndex = 0;
    [TextArea] public string RecordingDeviceName = null;
    [Header("How Long In Seconds Before Recording Plays")]
    public float Latency = 0;
    [Header("Choose A Key To Play/Pause/Add Reverb To Recording")]

    //FMOD Objects
    private FMOD.Sound sound;
    private FMOD.CREATESOUNDEXINFO exinfo;
    private FMOD.Channel channel;
    private FMOD.ChannelGroup channelGroup;

    //How many recording devices are plugged in for us to use.
    private int numOfDriversConnected = 0;
    private int numofDrivers = 0;

    //Info about the device we're recording with.
    private System.Guid MicGUID;
    private int SampleRate = 0;
    private FMOD.SPEAKERMODE FMODSpeakerMode;
    private int NumOfChannels = 0;
    private FMOD.DRIVER_STATE driverState;

    void Start()
    {
        //Step 1: Check to see if any recording devices (or drivers) are plugged in and available for us to use.


        RuntimeManager.CoreSystem.getRecordNumDrivers(out numofDrivers, out numOfDriversConnected);

        if (numOfDriversConnected == 0)
            Debug.Log("Hey! Plug a Microhpone in ya dummy!!!");
        else
            Debug.Log("You have " + numOfDriversConnected + " microphones available to record with.");


        //Step 2: Get all of the information we can about the recording device (or driver) that we're
        //        going to use to record with.
        RuntimeManager.CoreSystem.getRecordDriverInfo(RecordingDeviceIndex, out RecordingDeviceName, 50,
            out MicGUID, out SampleRate, out FMODSpeakerMode, out NumOfChannels, out driverState);


        //Next we want to create an "FMOD Sound Object", but to do that, we first need to use our 
        //FMOD.CREATESOUNDEXINFO variable to hold and pass information such as the sample rate we're
        //recording at and the num of channels we're recording with into our Sound object.


        //Step 3: Store relevant information into FMOD.CREATESOUNDEXINFO variable.


        exinfo.cbsize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(FMOD.CREATESOUNDEXINFO));
        exinfo.numchannels = NumOfChannels;
        exinfo.format = FMOD.SOUND_FORMAT.PCM16;
        exinfo.defaultfrequency = SampleRate;
        exinfo.length = (uint)SampleRate * sizeof(short) * (uint)NumOfChannels;


        //Step 4: Create an FMOD Sound "object". This is what will hold our voice as it is recorded.


        RuntimeManager.CoreSystem.createSound(exinfo.userdata, FMOD.MODE.LOOP_NORMAL | FMOD.MODE.OPENUSER,
            ref exinfo, out sound);


        //Step 5: Start recording through our chosen device into our Sound object.


        RuntimeManager.CoreSystem.recordStart(RecordingDeviceIndex, sound, true);


        // Step 6: Start a Corutine that will tell our Sound object to play after a ceratin amount of time.


        StartCoroutine(Wait());
    }


    IEnumerator Wait()
    {
        yield return new WaitForSeconds(Latency);
        RuntimeManager.CoreSystem.playSound(sound, channelGroup, true, out channel);
        channel.setPaused(false);
        Debug.Log("Ready To Play");
    }


    void Update()
    {

    }
}