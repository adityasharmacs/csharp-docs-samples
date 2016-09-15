using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using NAudio.Wave;

namespace TranscribeButton
{
    enum WhatsHappening
    {
        Initializing,
        Nothing,
        Recording,
        WaitingForFinalData,
        Transcribing,
    };
    public partial class TranscribeForm : Form
    {
        WhatsHappening _whatsHappening = WhatsHappening.Nothing;       
        private WaveIn _waveIn;
        private List<byte> _buffer = new List<byte>();

        public TranscribeForm()
        {
            InitializeComponent();
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyValue == 32 && _whatsHappening == WhatsHappening.Nothing)
            {
                _whatsHappening = WhatsHappening.Recording;
                _buffer.Clear();
                Transcription.Text = "Recording...";
            }
            else
            {
                Transcription.Text = $"Key Down: {e.KeyValue}";
            }
        }

        private void WaveIn_OnDataAvailable(object sender, WaveInEventArgs e)
        {
            if (_whatsHappening == WhatsHappening.Recording)
                _buffer.AddRange(e.Buffer.Take(e.BytesRecorded));
        }

        private void WaveIn_OnRecordingStopped(object sender, EventArgs e)
        {
        }

        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyValue == 32 && _whatsHappening == WhatsHappening.Recording)
            {
                _whatsHappening = WhatsHappening.Transcribing;
                Transcription.Text = "Transcribing...";
            }
            else
            {
                Transcription.Text = $"Key Up: {e.KeyValue}";
            }
        }

        private void InvokeAction(Action a)
        {
            Delegate d = a;
            Invoke(d);
        }

        private void OnLoad(object sender, EventArgs e)
        {
            for (int i = 0; i < WaveIn.DeviceCount; ++i)
            {
                DeviceList.Items.Add(WaveIn.GetCapabilities(i).ProductName);
                DeviceList.SelectedIndex = 0;
            }
                InvokeAction(() => { Transcription.Text = "Initializing Audio..."; });
                _whatsHappening = WhatsHappening.Initializing;
                _waveIn = new WaveIn()
                {                    
                    DeviceNumber = DeviceList.SelectedIndex,
                    WaveFormat = new WaveFormat(sampleRate: 8000, channels: 1),
                };
                _waveIn.DataAvailable += WaveIn_OnDataAvailable;
                _buffer.Clear();
                _whatsHappening = WhatsHappening.Nothing;
                _waveIn.StartRecording();
                InvokeAction(() => { Transcription.Text = "Ready.  Hold down the spacebar to record."; });           
        }
    }
}
