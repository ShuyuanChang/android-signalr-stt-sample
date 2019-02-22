package com.example.michi.videosample;


import android.content.Intent;
import android.os.SystemClock;
import android.support.design.widget.TextInputEditText;
import android.support.design.widget.TextInputLayout;
import android.support.v4.app.ActivityCompat;
import android.support.v7.app.AppCompatActivity;
import android.os.Bundle;
import android.util.Log;
import android.view.View;
import android.widget.TextView;
import java.io.IOException;
import com.microsoft.cognitiveservices.speech.ResultReason;
import com.microsoft.cognitiveservices.speech.SpeechConfig;
import com.microsoft.cognitiveservices.speech.SpeechRecognitionResult;
import com.microsoft.cognitiveservices.speech.SpeechRecognizer;

import java.nio.ByteBuffer;
import java.util.concurrent.ExecutionException;
import java.util.concurrent.Future;
import static android.Manifest.permission.*;
import android.media.AudioFormat;
import android.media.AudioRecord;
import android.media.MediaRecorder;
import com.microsoft.signalr.*;

//http://audiorecordandroid.blogspot.com/
//SignalR java client: https://docs.microsoft.com/en-us/aspnet/core/signalr/java-client?view=aspnetcore-2.2

public class MainActivity extends AppCompatActivity {
    // Replace below with your own subscription key
    private static String speechSubscriptionKey = "98bba22d547b4147a941d8074aaad9c7";
    // Replace below with your own service region (e.g., "westus").
    private static String serviceRegion = "eastasia";
    private static boolean isRecording = false;
    private static final int RECORDER_SAMPLERATE = 16000;
    private static final int RECORDER_CHANNELS = AudioFormat.CHANNEL_IN_MONO;
    private static final int RECORDER_AUDIO_ENCODING = AudioFormat.ENCODING_PCM_16BIT;
    private Thread recordingThread = null;
    private final short numberOfChannels = 1;
    private static final int BufferElements2Rec = 1024; // want to play 2048 (2K) since 2 bytes we use only 1024
    private static final int BytesPerElement = 2; // 2 bytes in 16bit format
    private static HubConnection hubConnection = null;
    private String MyLanguage = "en-US";
    private String TargetLanguage = "zh-Hant";
    private String UserName = "michael";
    AudioRecord recorder = null;
    String SIGNALR_SERVER = "http://michidevvm.eastasia.cloudapp.azure.com:5000/translator";
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_main);
        int requestCode = 5; // unique code for the permission request
        ActivityCompat.requestPermissions(MainActivity.this, new String[]{RECORD_AUDIO, INTERNET}, requestCode);

        // Disable QVA
        if(true) {
            Intent intent = new Intent("q2w");
            intent.putExtra("message", "StopWackUpService");
            sendBroadcast(intent);
        }
    }
    private void TerminateRecorder(){
        if(recorder != null){
            recorder.stop();
            recorder.release();
            recorder = null;
        }
    }
    private TextView textView = null;
    private void InitSignalRConnection() {
        TerminateSignalRconnection();
        textView = (TextView) this.findViewById(R.id.hello); // 'hello' is the ID of your text view
        hubConnection = HubConnectionBuilder.create(SIGNALR_SERVER).build();
        hubConnection.on("ServerMessages", (message) -> {
            runOnUiThread(new Runnable() {
                public void run()
                {
                    System.out.println("[Message]" + message);
                    //textView.setText(textView.getText() + "\r\n" + message);
                    if(message == null || message == ""){
                        return;
                    }
                    textView.setText(message);
                }
            });
        }, String.class);
        hubConnection.start().blockingAwait();
        hubConnection.send("RegisterAttendeeAsync", UserName, MyLanguage, TargetLanguage);
        System.out.println("Registerred !!!!!!!");
    }

    private void TerminateSignalRconnection(){
        if(hubConnection != null) {
            hubConnection.stop();
            hubConnection = null;
        }
    }

    private void writeAudioDataToFile() {
        InitSignalRConnection();
        hubConnection.setKeepAliveInterval(1000 * 10);

        byte [] header = GetWaveHeader();
        if(header == null){
            System.out.println("===========================HEADER FAILED===========================");
        }
        if(hubConnection.getConnectionState() != HubConnectionState.CONNECTED) {
            System.out.println("......Reconnecting...");
            hubConnection.start().blockingAwait();
        }
        hubConnection.send("RegisterAttendeeAsync", UserName, MyLanguage, TargetLanguage);
        System.out.println("sending WAVE HEADER......");
        hubConnection.send("ReceiveAudioJavaAsync", UserName, header);
        int minBufSize = AudioRecord.getMinBufferSize(RECORDER_SAMPLERATE, RECORDER_CHANNELS, RECORDER_AUDIO_ENCODING);
        System.out.println(">>> Size:" +minBufSize);
        while (isRecording) {
            byte buffer [] = new byte[minBufSize];
            if(recorder.read( buffer,0, minBufSize) > -1){
                if(hubConnection.getConnectionState() != HubConnectionState.CONNECTED) {
                    System.out.println(".........Reconnecting...");
                    hubConnection.start().blockingAwait();
                    hubConnection.send("RegisterAttendeeAsync", UserName, MyLanguage,TargetLanguage);
                }
                //System.out.println("........sending audio data.........");
                hubConnection.send("ReceiveAudioJavaAsync", UserName,  buffer);
            }
            buffer = null;
            SystemClock.sleep(1);
        }
    }
    private void InitRecorder() {
        TerminateRecorder();
        recorder = new AudioRecord(MediaRecorder.AudioSource.MIC,
                RECORDER_SAMPLERATE, RECORDER_CHANNELS,
                RECORDER_AUDIO_ENCODING, BufferElements2Rec * BytesPerElement);
    }
    private void startRecording() {
        InitRecorder();
        recorder.startRecording();
        isRecording = true;
        recordingThread = new Thread(new Runnable() {
            public void run() {
                System.out.println("Starting recorder thread......");
                writeAudioDataToFile();
            }
        }, "AudioRecorder Thread");
        //InitSignalRConnection();
        recordingThread.start();
    }
    private void stopRecording() {
        // stops the recording activity
        if (null != recorder) {
            isRecording = false;
            TerminateRecorder();
            recordingThread = null;
        }
    }

    public void onTerminateButtonClicked(View v) {
        stopRecording();
    }
    private byte[] GetWaveHeader(){

        WaveHeader header = new WaveHeader( WaveHeader.FORMAT_PCM, (short)1, RECORDER_SAMPLERATE, (short)16, 0);
        try {
            return header.toByteArray();
        }catch (IOException exp){
            System.out.println("Unable to construct WAVEHEADER!!!");
            return  null;
        }
    }
    public void onSpeechButtonClicked(View v) {
        System.out.println("Start  recording...");
        startRecording();
    }
}
