# AudioSessionMonitorExample
Example to test monitoring of svchost audio sessions for a device and controlling them independently.
It only monitors sessions of the chosen device with ID 3860 (process name "svchost.exe")
It also monitors the mic, just because I needed to test it

To try it: enter a device name in the textbox, press the "Initialize" button and see what it recognizes in the big textbox underneath.
The sessions are updated manually by pressing the "update" button, or after editing the volume of the latest session in the automatically created list (with one of the two last buttons "set volume to 0.5/1.0")

An audio meter for every identified session will appear on the upper right section