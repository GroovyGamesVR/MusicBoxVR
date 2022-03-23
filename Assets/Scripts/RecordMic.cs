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
    private FMOD.CREATESOUNDEXINFO exinfo, exinfo2;
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

    private IntPtr ptr1 = Marshal.AllocHGlobal(65535);
    private IntPtr ptr2 = Marshal.AllocHGlobal(65535);
    private Byte[] soundData = new Byte[200000];
    private uint lastrecordpos = 0;
    private uint recordpos = 0;
    private uint soundLength = 0;
    private FMOD.Sound recvSound;
    private int samplePos = 0;

    void Start()
    {
        // Check for input devices
        RuntimeManager.CoreSystem.getRecordNumDrivers(out numofDrivers, out numOfDriversConnected);
        Debug.Log("Found " + numOfDriversConnected + " microphones");

        // Get device info
        RuntimeManager.CoreSystem.getRecordDriverInfo(RecordingDeviceIndex, out RecordingDeviceName, 50,
            out MicGUID, out SampleRate, out FMODSpeakerMode, out NumOfChannels, out driverState);
        Debug.Log("SampleRate=" + SampleRate);
        Debug.Log("NumOfChannels=" + NumOfChannels);

        // Store relevant information into FMOD.CREATESOUNDEXINFO variable.
        exinfo.cbsize = Marshal.SizeOf(typeof(FMOD.CREATESOUNDEXINFO));
        exinfo.numchannels = NumOfChannels;
        exinfo.format = FMOD.SOUND_FORMAT.PCM16;
        exinfo.defaultfrequency = SampleRate;
        exinfo.length = ((uint)SampleRate * sizeof(short) * (uint)NumOfChannels);
 
        // Create an FMOD Sound "object". This is what will hold our voice as it is recorded.
        var res = RuntimeManager.CoreSystem.createSound(exinfo.userdata, FMOD.MODE.LOOP_NORMAL | FMOD.MODE.OPENUSER,
            ref exinfo, out sound);
        FMOD_ERRCHECK(res);

        // Start recording
        FMOD_ERRCHECK(RuntimeManager.CoreSystem.recordStart(RecordingDeviceIndex, sound, true));
        FMOD_ERRCHECK(sound.getLength(out soundLength, FMOD.TIMEUNIT.PCM));

        //Setup receive stream to playback sound
        Debug.Log("Creating Stream");
        exinfo2.cbsize = Marshal.SizeOf(typeof(FMOD.CREATESOUNDEXINFO));
        exinfo2.numchannels = NumOfChannels;
        exinfo2.format = FMOD.SOUND_FORMAT.PCM16;
        exinfo2.defaultfrequency = SampleRate;
        exinfo2.pcmreadcallback = PCMREADCALLBACK;
        exinfo2.length = exinfo.length;
        FMOD_ERRCHECK(RuntimeManager.CoreSystem.createSound((string)null, FMOD.MODE.LOOP_NORMAL | FMOD.MODE.CREATESTREAM | FMOD.MODE.OPENUSER, ref exinfo2, out recvSound));
        FMOD_ERRCHECK(RuntimeManager.CoreSystem.playSound(recvSound, channelGroup, false, out channel));
        channel.setMode(FMOD.MODE.LOOP_NORMAL);
        //channel.setPosition((uint)samplePos, FMOD.TIMEUNIT.PCMBYTES);
    }

    private uint samplesToBytes(int sampleCnt)
    {
        // PCM16
        return (uint)(sampleCnt * 16 * NumOfChannels / 8);
    }
    private int bytesToSamples(uint bytes)
    {
        // PCM16
        return (((int)bytes * 8) / 16) / NumOfChannels;
    }

    private FMOD.RESULT PCMREADCALLBACK(IntPtr soundraw, IntPtr data, uint sizebytes)
    {
        //Debug.Log("PCMREADCALLBACK: sizebytes=" + sizebytes);
        if (samplePos == 0)
        {
            // nothing recorded yet
            return FMOD.RESULT.OK;
        }
        if (sizebytes > samplePos)
        {
            // Copy everything
            //Debug.Log("PCMREADCALLBACK: Copying all " + samplePos + " bytes");
            Marshal.Copy(soundData, 0, data, samplePos);
            samplePos = 0;
        }
        else
        {
            // Only copy what fits
            //Debug.Log("PCMREADCALLBACK: Copying " + sizebytes + " bytes");
            Marshal.Copy(soundData, 0, data, (int)sizebytes);
            // shift whatevers left to the start
            shiftArrayStart(ref soundData, sizebytes);
            samplePos -= (int)sizebytes;
            //Debug.Log("PCMREADCALLBACK: New samplePos=" + samplePos);
        }
        return FMOD.RESULT.OK;
    }

    void shiftArrayStart(ref Byte[] inArray, uint newStart)
    {
        Byte[] tmpData = new Byte[inArray.Length];
        Array.Copy(inArray, newStart, tmpData, 0, (inArray.Length - newStart));
        inArray = tmpData;
    }

    void Update()
    {
        // Load mic data into buffer (TX to server)
        FMOD_ERRCHECK(RuntimeManager.CoreSystem.getRecordPosition(RecordingDeviceIndex, out recordpos));
        if (recordpos != lastrecordpos)
        {
            int blocklength;
            uint len1, len2;
            blocklength = (int)recordpos - (int)lastrecordpos;
            if (blocklength < 0)
            {
                blocklength += (int)soundLength;
            }
            FMOD_ERRCHECK(sound.@lock(lastrecordpos * (uint)exinfo.numchannels * 2, (uint)blocklength * (uint)exinfo.numchannels * 2, out ptr1, out ptr2, out len1, out len2));
            //Debug.Log("Read len1=" + len1 + ", len2=" + len2);
            if (len1 > 0)
            {
                if (len1 + samplePos > soundData.Length)
                {
                    Debug.LogError("Sound buffer full, dropping samples!");
                    // Remove old sound
                    shiftArrayStart(ref soundData, len1);
                }
                //Debug.Log("Copying " + len1 + " bytes to position " + samplePos);
                Marshal.Copy(ptr1, soundData, samplePos, (int)len1);
                samplePos += (int)len1;
                //datalength += fwrite(ptr1, 1, len1, fptr);
            }
            // if (len2 > 0)
            //{
            //    datalength += fwrite(ptr2, 1, len2, fptr);
            //}
            FMOD_ERRCHECK(sound.unlock(ptr1, ptr2, len1, len2));
        }
        lastrecordpos = recordpos;
        RuntimeManager.CoreSystem.update();
    }

    void OnDestroy()
    {
        sound.release();
        recvSound.release();
    }

    void FMOD_ERRCHECK(FMOD.RESULT res)
    {
        if (res != FMOD.RESULT.OK)
        {
            Debug.LogError("FMOD_Unity ERROR: " + res + " - " + FMOD.Error.String(res));
        }
    }
}