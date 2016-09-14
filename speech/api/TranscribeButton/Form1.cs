using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TranscribeButton
{
    public partial class TranscribeForm : Form
    {
        public TranscribeForm()
        {
            InitializeComponent();
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            Transcription.Text = $"Key Down: {e.KeyValue}";   
        }

        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            Transcription.Text = $"Key Up: {e.KeyValue}";
        }

    }
}
