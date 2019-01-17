Signalr voice recognition backend
=================================
-	Reference
	-	https://stackoverflow.com/questions/15349987/stream-live-android-audio-to-server
	-	https://github.com/msimecek/Sample-Continuous-S2T
	-	https://github.com/msimecek/Sample-Continuous-S2T/tree/master/VideoAudioStreamerAPI

Overview
========

In this sample I created a SignalR backend that receives audio data from and android device. The server streams audio data to Azure Speech API to translate voice to text and send back to Android device.

To simplify backend design. I have a collection that holds Speech API client for each preferred language instead of each attendees, so that in the future this can be extended to a online conference translation service.
