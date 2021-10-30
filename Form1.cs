using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows;
using System.Threading;
using NAudio.CoreAudioApi;
using System.Diagnostics;
using NAudio.CoreAudioApi.Interfaces;
using Timer = System.Timers.Timer;
using System.Runtime.InteropServices;

namespace AudioSessionMonitorExample
{
    public partial class Form1 : Form {
        private static Random rnd = new Random();
        private static double audioValueMax = 0;
        private static double audioValueLast = 0;
        private static int audioCount = 0;
        private static int RATE = 48000;
        private static int BUFFER_SAMPLES = 1024;
        private static int timer_ms = 25;
        private static Form1 form;

        List<SoundDevice> soundDeviceList;

        private SessionCollection sessions;

        public Form1() {
            InitializeComponent();
        }

        private void AudioSessions() {
            textBox1.Text = "";
            soundDeviceList = new List<SoundDevice>();
            var enumerator = new MMDeviceEnumerator();
            foreach (var wasapi in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.All)) {
                try {
                    if (wasapi.DeviceFriendlyName.Contains(textBoxDeviceName.Text) || wasapi.FriendlyName.Contains(textBoxDeviceName.Text)) {
                        textBox2.Text = $"{wasapi.DataFlow} - {wasapi.FriendlyName} - {wasapi.DeviceFriendlyName} - {wasapi.State} - {wasapi.ID} - {wasapi.InstanceId}\r\n";
                        sessions = wasapi.AudioSessionManager.Sessions;
                        if (sessions == null) return;
                        //var sessionList = new List<AudioSessionControl>(sessions.Count);
                        for (int i = 0; i < sessions.Count; i++) {
                            var session = sessions[i];
                            if (session.IsSystemSoundsSession && ProcessExists(session.GetProcessID) && session.GetProcessID == 3860) {
                                textBox1.Text += $"SystemSoundSession - {session.DisplayName}\r\n\t\t{session.GetSessionIdentifier}\r\n\t\tVolume relative to device: {session.SimpleAudioVolume.Volume}\r\n\t\tPeak Volume: {session.AudioMeterInformation.MasterPeakValue}\r\n\r\n";
                                break;
                            }
                        }
                        int j = 0;
                        for (int i = 0; i < sessions.Count; i++) {
                            var session = sessions[i];
                            if (!session.IsSystemSoundsSession && ProcessExists(session.GetProcessID) && session.GetProcessID == 3860) {
                                textBox1.Text += $"SoundSession - {session.DisplayName}\r\n\t\t{session.GetSessionIdentifier}\r\n\t\tVolume relative to device: {session.SimpleAudioVolume.Volume}\r\n\t\tPeak Volume: {session.AudioMeterInformation.MasterPeakValue}\r\n\r\n";
                                soundDeviceList.Add(new SoundDevice(session, j));
                                j++;
                            }
                        }
                        break;
                    }
                }
                catch (Exception ex) {
                    Console.WriteLine(ex.Message);
                }
            }
        }

        bool ProcessExists(uint processId) {
            try {
                var process = Process.GetProcessById((int)processId);
                return true;
            }
            catch (ArgumentException) {
                return false;
            }
        }

        private void InitializeMicrophone() {
            var waveIn = new WaveInEvent();
            waveIn.DeviceNumber = 0; // change this to select different sound inputs
            waveIn.WaveFormat = new NAudio.Wave.WaveFormat(RATE, 1); // 1 for mono
            waveIn.DataAvailable += OnDataAvailable; // this function must exist
            waveIn.BufferMilliseconds = (int)((double)BUFFER_SAMPLES / (double)RATE * 1000.0);
            waveIn.StartRecording();
        }

        private void OnDataAvailable(object sender, WaveInEventArgs args) {
            float max = 0;

            // interpret as 16 bit audio
            for (int index = 0; index < args.BytesRecorded; index += 2) {
                short sample = (short)((args.Buffer[index + 1] << 8) |
                                        args.Buffer[index + 0]);
                var sample32 = sample / 32768f; // to floating point
                if (sample32 < 0) sample32 = -sample32; // absolute value 
                if (sample32 > max) max = sample32; // is this the max value?
            }

            // calculate what fraction this peak is of previous peaks
            if (max > audioValueMax) {
                audioValueMax = (double)max;
            }
            audioValueLast = max;
            audioCount += 1;
            double frac = audioValueLast / audioValueMax;
            try {
                BeginInvoke(new Action(() => progressBar1.Value = (int)(max * 100)));
            }
            catch (Exception ex) {
                Console.WriteLine(ex.Message);
            }
        }

        private void Form1_Load(object sender, EventArgs e) {
            form = this;
            InitializeMicrophone();
            SendMessage(progressBar1.Handle,
                0x400 + 16, //WM_USER + PBM_SETSTATE
                0x0003, //PBST_PAUSED
                0);

            //SendMessage(progressBar1.Handle,
            //      0x400 + 16, //WM_USER + PBM_SETSTATE
            //      0x0002, //PBST_ERROR
            //      0);
        }

        private void UpdateSessions() {
            textBox1.Text = "";
            if (soundDeviceList == null || soundDeviceList.Count == 0)
                return;
            foreach (SoundDevice sd in soundDeviceList) {
                AudioSessionControl session = sd.GetSession;
                textBox1.Text += $"SoundSession - {session.DisplayName}\r\n\t\t{session.GetSessionIdentifier}\r\n\t\tVolume relative to device: {session.SimpleAudioVolume.Volume}\r\n\t\tPeak Volume: {session.AudioMeterInformation.MasterPeakValue}\r\n\r\n";
            }
        }


        private void buttonInitialize_Click(object sender, EventArgs e) {
            if (!string.IsNullOrWhiteSpace(textBoxDeviceName.Text))
                AudioSessions();
        }

        private void buttonUpdate_Click(object sender, EventArgs e) {
            UpdateSessions();
        }

        private void buttonSet05_Click(object sender, EventArgs e) {
            if (soundDeviceList == null || soundDeviceList.Count == 0)
                return;
            soundDeviceList.Last().GetSession.SimpleAudioVolume.Volume = 0.5f;
            UpdateSessions();
        }

        private void buttonSet1_Click(object sender, EventArgs e) {
            if (soundDeviceList == null || soundDeviceList.Count == 0)
                return;
            soundDeviceList.Last().GetSession.SimpleAudioVolume.Volume = 1.0f;
            UpdateSessions();
        }

        public class SoundDevice : IAudioSessionEventsHandler {
            private AudioSessionControl _session;
            public AudioSessionControl GetSession => _session;
            private Timer _timer;
            private ProgressBar _pbar;
            private Label _label;
            private int _offset;

            public SoundDevice(AudioSessionControl sess, int offset) {
                _session = sess;
                _session.RegisterEventClient(this);
                _offset = offset;
                _pbar = new ProgressBar();
                _pbar.Location = new Point(10 + (160 * _offset), 30);
                _pbar.Size = new Size(_pbar.Width + 40, _pbar.Height);
                _pbar.Maximum = 100;
                _pbar.Minimum = 0;
                _pbar.Visible = true;
                _label = new Label();
                _label.Location = new Point(10 + (160 * _offset), 10);
                _label.Size = new Size(_label.Width + 40, _label.Height);
                _label.Text = _session.DisplayName;
                form.panel1.Controls.Add(_pbar);
                form.panel1.Controls.Add(_label);
                SendMessage(_pbar.Handle,
                    0x400 + 16, //WM_USER + PBM_SETSTATE
                    0x0003, //PBST_PAUSED
                    0);
                _timer = new System.Timers.Timer(timer_ms);
                _timer.Elapsed += Timer_Elapsed;
                _timer.AutoReset = false;
                _timer.Start();
            }

            private void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e) {
                try {
                    form.BeginInvoke(new Action(() => _pbar.Value = (int)(_session.AudioMeterInformation.MasterPeakValue * 100)));
                }
                catch (Exception ex) {
                    Console.WriteLine(ex.Message);
                }
                _timer.Start(); // trigger next timer
            }

            public void OnChannelVolumeChanged(uint channelCount, IntPtr newVolumes, uint channelIndex) {
                throw new NotImplementedException();
            }

            public void OnDisplayNameChanged(string displayName) {
                throw new NotImplementedException();
            }

            public void OnGroupingParamChanged(ref Guid groupingId) {
                throw new NotImplementedException();
            }

            public void OnIconPathChanged(string iconPath) {
                throw new NotImplementedException();
            }

            public void OnSessionDisconnected(AudioSessionDisconnectReason disconnectReason) {
                throw new NotImplementedException();
            }

            public void OnStateChanged(AudioSessionState state) {
                throw new NotImplementedException();
            }

            public void OnVolumeChanged(float volume, bool isMuted) {
                Console.WriteLine($"Vol changed to \"Listening device\" {_session.DisplayName}: " + volume.ToString());
            }

            public void Dispose() {
                if (_session != null) {
                    _session.UnRegisterEventClient(this);
                    // I think Dispose calls UnRegisterEventClient anyway.. But belt and braces
                    _session.Dispose();
                    _session = null;   // null obj
                    GC.Collect(); // Force GC collection
                }
            }
        }
        
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern uint SendMessage(IntPtr hWnd,
            uint Msg,
            uint wParam,
            uint lParam);
    }
}
