using FMODUnity;
using UnityEngine;
using System.Collections;
using System;
using System.Runtime.InteropServices;

public class RecordMic : MonoBehaviour
{
    //public variables
    [Header("Choose A Microphone")]
    public int RecordingDeviceIndex = 0;
    [TextArea] public string RecordingDeviceName = null;
    [Header("How Long In Seconds Before Recording Plays")]

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

    private IntPtr buffer = Marshal.AllocHGlobal(65535);
    private uint length = 65535;
    private uint read = 0;
    private Byte[] soundData = new Byte[65535];

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
        exinfo.cbsize = Marshal.SizeOf(typeof(FMOD.CREATESOUNDEXINFO));
        exinfo.numchannels = NumOfChannels;
        exinfo.format = FMOD.SOUND_FORMAT.PCM16;
        exinfo.defaultfrequency = SampleRate;
        exinfo.length = (uint)SampleRate * sizeof(short) * (uint)NumOfChannels;


        //Step 4: Create an FMOD Sound "object". This is what will hold our voice as it is recorded.
        var res = RuntimeManager.CoreSystem.createSound(exinfo.userdata, FMOD.MODE.LOOP_NORMAL | FMOD.MODE.OPENUSER | FMOD.MODE.OPENONLY | FMOD.MODE.CREATESTREAM,
            ref exinfo, out sound);
        FMOD_ERRCHECK(res);


        //Step 5: Start recording through our chosen device into our Sound object.
        //RuntimeManager.CoreSystem.recordStart(RecordingDeviceIndex, sound, true);
        //channel.setPaused(false);
        //RuntimeManager.CoreSystem.playSound(sound, channelGroup, true, out channel);
        //sound.readData(buffer, length, out read);
        //Debug.Log("Read " + read + " sound bytes");
        //StartCoroutine(Wait());
    }

    //IEnumerator Wait()
    //{
    //    yield return new WaitForSeconds(0);
    //    RuntimeManager.CoreSystem.playSound(sound, channelGroup, true, out channel);
    //    channel.setPaused(false);
    //   Debug.Log("Ready To Play");
    //}

    void Update()
    {
        FMOD_ERRCHECK(sound.readData(buffer, length, out read));
        // For PCM16
        int sampleCount = (((int)read * 8) / 16) / NumOfChannels; 
        Debug.Log("Read " + read + " sound bytes (" + sampleCount + " samples)");
        if (read > 0)
        {
            //Debug.Log("Copying");
            Marshal.Copy(buffer, soundData, 0, sampleCount);
            //FIXME: Free eventually...
            //Marshal.FreeHGlobal(buffer);
        }

        // send off here...
        // simulate receiving..
        if (read > 0)
        {
            FMOD.Sound recvSound;
            Debug.Log("Creating Stream");
            FMOD_ERRCHECK(RuntimeManager.CoreSystem.createStream(soundData, FMOD.MODE.OPENMEMORY | FMOD.MODE.OPENRAW, ref exinfo, out recvSound));
            Debug.Log("Playing Sound");
            FMOD_ERRCHECK(RuntimeManager.CoreSystem.playSound(recvSound, channelGroup, true, out channel));
            channel.setPaused(false);
            //Debug.Log("Releasing");
            //FMOD_ERRCHECK(recvSound.release());
        }
    }

    void FMOD_ERRCHECK(FMOD.RESULT res)
    {
        if (res != FMOD.RESULT.OK)
        {
            Debug.Log("FMOD_Unity ERROR: " + res + " - " + FMOD.Error.String(res));
        }
    }
}