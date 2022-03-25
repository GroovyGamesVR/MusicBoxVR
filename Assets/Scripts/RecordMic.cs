using FMODUnity;
using UnityEngine;
using System.Collections;
using System;
using System.Text;
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
    private FMOD.SOUND_FORMAT soundFormat = FMOD.SOUND_FORMAT.PCM16;

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
    private IntPtr recvPtr1 = Marshal.AllocHGlobal(65535);
    private IntPtr recvPtr2 = Marshal.AllocHGlobal(65535);
    private Byte[] soundData = new Byte[200000];
    private uint lastrecordpos = 0;
    private uint recordpos = 0;
    private uint soundLength = 0;
    private FMOD.Sound recvSound;
    private int samplePos = 0;
    private uint nextPlaybackPos = 0;
    private uint playbackPos = 0;

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
        exinfo.format = soundFormat;
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
        exinfo2.cbsize = Marshal.SizeOf(typeof(FMOD.CREATESOUNDEXINFO));
        exinfo2.numchannels = NumOfChannels;
        exinfo2.format = soundFormat;
        exinfo2.defaultfrequency = SampleRate;
        exinfo2.length = exinfo.length;
        FMOD_ERRCHECK(RuntimeManager.CoreSystem.createSound(exinfo2.userdata, FMOD.MODE.LOOP_NORMAL | FMOD.MODE.OPENUSER, ref exinfo2, out recvSound));
        FMOD_ERRCHECK(RuntimeManager.CoreSystem.playSound(recvSound, channelGroup, true, out channel));
    }

    /* FIXME
     * Instead of shifting array to beginning, should make a proper circular buffer.
     */
    void shiftArrayStart(ref Byte[] inArray, uint newStart)
    {
        Byte[] tmpData = new Byte[inArray.Length];
        Array.Copy(inArray, newStart, tmpData, 0, (inArray.Length - newStart));
        inArray = tmpData;
    }

    void Update()
    {
        // Playback audio (Rx)
        InsertDataIntoPlayback();
        // Store audio (Tx)
        StoreMicInToBuffer();
    }

    /**
     * If there is new data in the receive buffer to play, load
     * it into the channel to play back.
     * If there is no new data in the receive buffer, check to
     * see if there is still some data loaded in the channel
     * that hasn't been played yet.
     * If there is still some, play it.
     * If there is nothing ready to play and nothing in the receive
     * buffer, pause the player until new data comes (packet loss).
     * 
     * FIXME: This is very static-sounding...
     */
    private void InsertDataIntoPlayback()
    {
        // Load playback buffer with data (Rx server)
        uint len1, len2;
        channel.setPaused(true);
        FMOD_ERRCHECK(channel.getPosition(out playbackPos, FMOD.TIMEUNIT.PCMBYTES));
        if (samplePos <= 0)
        {
            // Nothing new to play
            if (playbackPos < nextPlaybackPos)
            {
                // Already have data in buffer ahead to play, resume playing
                //Debug.Log("Nothing new to play, but buffer loaded, keep playing. (playbackPos=" + playbackPos + ", nextPlayPos=" + nextPlaybackPos + ")");
                channel.setPaused(false);
            }
            else
            {
                //Debug.Log("Nothing new to play and buffer is empty, stopping playback. (playbackPos=" + playbackPos + ", nextPlayPos=" + nextPlaybackPos + ")");
            }
            return;
        }
        //Debug.Log("playbackPos = " + playbackPos + ", samplePos=" + samplePos);
        FMOD_ERRCHECK(recvSound.@lock(playbackPos, (uint)samplePos, out recvPtr1, out recvPtr2, out len1, out len2));
        //Debug.Log("len1=" + len1 + ", len2=" + len2);
        if (len1 > 0)
        {
            Marshal.Copy(soundData, 0, recvPtr1, (int)len1);
            // shift whatevers left to the start
            shiftArrayStart(ref soundData, len1);
            samplePos -= (int)len1;
        }
        if (len2 > 0)
        {
            Marshal.Copy(soundData, 0, recvPtr2, (int)len2);
            // shift whatevers left to the start
            shiftArrayStart(ref soundData, len2);
            samplePos -= (int)len2;
        }
        nextPlaybackPos = playbackPos + len1 + len2;
        channel.setPaused(false);
        FMOD_ERRCHECK(recvSound.unlock(recvPtr1, recvPtr2, len1, len2));
    }

    /**
     * Pull data record from mic and save off into a data buffer
     */
    private void StoreMicInToBuffer() {
        // Load mic data into buffer (TX to server)
        RuntimeManager.CoreSystem.update();
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
 
            // sound buffer is circular. If the amount requested to lock (offset+length)
            // goes off the end of the buffer and loops back to the beginning,
            // ptr1/len1 holds the data from offset-buffer end.
            // ptr2/len2 holds the next data from buffer looped back at the beginning.
            FMOD_ERRCHECK(sound.@lock(lastrecordpos * (uint)exinfo.numchannels * sizeof(short), (uint)blocklength * (uint)exinfo.numchannels * sizeof(short), out ptr1, out ptr2, out len1, out len2));
            //Debug.Log("Read len1=" + len1 + ", len2=" + len2);
            if (len1 > 0)
            {
                if (len1 + samplePos > soundData.Length)
                {
                    Debug.LogError("Sound buffer full, dropping samples!");
                    if (len1 > samplePos)
                    {
                        samplePos = 0;
                    }
                    else
                    {
                        // Remove oldest sound first
                        shiftArrayStart(ref soundData, len1);
                        samplePos -= (int)len1;
                    }
                }
                //Debug.Log("Copying " + len1 + " bytes to position " + samplePos);
                Marshal.Copy(ptr1, soundData, samplePos, (int)len1);
                samplePos += (int)len1;
            }
            if (len2 > 0)
            {
                if (len2 + samplePos > soundData.Length)
                {
                    Debug.LogError("Sound buffer full, dropping samples!");
                    if (len2 > samplePos)
                    {
                        samplePos = 0;
                    }
                    else
                    {
                        // Remove oldest sound first
                        shiftArrayStart(ref soundData, len2);
                        samplePos -= (int)len2;
                    }
                }
                //Debug.Log("Copying " + len2 + " bytes to position " + samplePos);
                Marshal.Copy(ptr2, soundData, samplePos, (int)len2);
                samplePos += (int)len2;
            }
            FMOD_ERRCHECK(sound.unlock(ptr1, ptr2, len1, len2));
        }
        lastrecordpos = recordpos;
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