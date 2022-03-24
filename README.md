# MusicBoxVR

Jamming in VR

## Contribute

- Install Unity v.2020.3.31f1
- Add Modules for all "Android Build Support"
- Clone repository locally
- Create new branch
- Open project from Unity Hub and select "Add project from disk"
- Commit and changes to new local branch
- Push local branch to Github and open a Pull Request

### Build

- Build server side for Windows. Disable Client script from scene. Upload to AWS:
```
aws gamelift upload-build --name MusicBoxVR-server --build-version 0.0.1 --build-root . --operating-system WINDOWS_2012 --region us-east-1
```
- Launch fleet, may take 15-20 minutes to reach Active state:
```
launch path=C:\game\MusicBoxVR.exe
launch parameters="-logFile C:\game\logs\server.log -isProd -batchmode -nographics"
IP port range=7000-8000
IP address range=0.0.0.0/0
```

### Debug

Remotely access AWS server logs following:
https://docs.aws.amazon.com/gamelift/latest/developerguide/fleets-remote-access.html

Access Oculus logs:
```
cd "C:\Program Files\Unity\Hub\Editor\2020.3.31f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools"
./adb devices
./adb -s <deviceId> logcat | grep -i unity
```
